using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Networking;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace HorrorTools.MacLessBuilder
{
    // Export > Zip > Upload > Dispatch > Queued/Building > Download. Each step reads and writes PipelineState,
    // so a domain reload or editor restart re-enters the current step instead of losing the build.
    [InitializeOnLoad]
    internal static class PipelineRunner
    {
        internal const string ArtifactName = "ios-ipa";
        internal const string ReleaseTag = "ios-build-input";
        private const string LogPrefix = "[MacLess Builder] ";
        private const long MaxReleaseAssetBytes = 2L * 1024 * 1024 * 1024;
        private const string OutputDir = "Builds/iOS";
        // Unity refuses to build into Library/, so the export and its zip live next to the downloaded IPAs.
        private const string WorkDir = OutputDir + "/Work";
        private const string XcodeFolder = "xcode";
        private static readonly TimeSpan FindRunInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan FindRunTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan RunPollInterval = TimeSpan.FromSeconds(15);
        private static CancellationTokenSource _cancellation;

        private static MacLessBuilderSettings Settings => MacLessBuilderSettings.instance;

        static PipelineRunner()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += Resume;
            }
        }

        public static void CancelBuild()
        {
            PipelineState state = PipelineState.instance;
            if (!state.IsActive)
            {
                return;
            }

            _cancellation?.Cancel();
            _cancellation = null;
            if (state.RunId != 0)
            {
                _ = CancelRunAsync(state.RunId);
            }

            Finish(state, "Cancelled.");
        }

        public static void StartBuild()
        {
            PipelineState state = PipelineState.instance;
            if (state.IsActive)
            {
                return;
            }

            Directory.CreateDirectory(WorkDir);
            IgnoreBuildsFolder();
            state.Begin(Guid.NewGuid().ToString("N").Substring(0, 8));
            // Deferred so the Xcode export does not run inside the UI click that started it.
            EditorApplication.delayCall += Run;
        }

        internal static void CreateZip(string sourceDir, string zipPath)
        {
            sourceDir = Path.GetFullPath(sourceDir);
            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                // Entry names need forward slashes, or unzip on the macOS runner creates files named "Classes\Main.mm".
                foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    archive.CreateEntryFromFile(file, ToEntryName(sourceDir, file), CompressionLevel.Fastest);
                }

                foreach (string directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        archive.CreateEntry(ToEntryName(sourceDir, directory) + "/");
                    }
                }
            }
        }

        internal static bool IsBuildsIgnored(IEnumerable<string> gitIgnoreLines)
        {
            return gitIgnoreLines.Any(line => line.Trim().Trim('/') is "Builds" or "[Bb]uilds");
        }

        internal static int RunMinutes(GitHubClient.WorkflowRun run)
        {
            // ponytail: run start to last update also counts time queued for a macOS runner, so this slightly
            // overestimates billed minutes; read the job timings instead if the estimate runs noticeably high.
            const DateTimeStyles styles = DateTimeStyles.RoundtripKind;
            if (!DateTime.TryParse(run.run_started_at, CultureInfo.InvariantCulture, styles, out DateTime started)
                || !DateTime.TryParse(run.updated_at, CultureInfo.InvariantCulture, styles, out DateTime finished))
            {
                return 0;
            }

            return Math.Max(0, (int)Math.Ceiling((finished - started).TotalMinutes));
        }

        private static async Task CancelRunAsync(long runId)
        {
            try
            {
                await GitHubClient.SendAsync(
                    UnityWebRequest.kHttpVerbPOST,
                    $"repos/{Settings.RepoFullName}/actions/runs/{runId}/cancel");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "Could not cancel the GitHub run: " + ex.Message);
            }
        }

        private static async Task<PipelineStep> DispatchAsync(PipelineState state)
        {
            var body = new GitHubClient.DispatchBody
            {
                @ref = Settings.DefaultBranch,
                inputs = new GitHubClient.DispatchInputs
                {
                    asset_name = Path.GetFileName(state.ZipPath),
                    request_id = state.RequestId,
                    use_pods = state.UsesPods ? "true" : "false",
                },
            };
            await GitHubClient.SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                $"repos/{Settings.RepoFullName}/actions/workflows/{WorkflowInstaller.FileName}/dispatches",
                JsonUtility.ToJson(body));
            return PipelineStep.Queued;
        }

        private static async Task<PipelineStep> DownloadAsync(PipelineState state, CancellationToken token)
        {
            string repo = Settings.RepoFullName;
            var list = await GitHubClient.GetAsync<GitHubClient.ArtifactList>(
                $"repos/{repo}/actions/runs/{state.RunId}/artifacts", token);
            GitHubClient.Artifact artifact = list.artifacts.FirstOrDefault(a => a.name == ArtifactName)
                ?? throw new InvalidOperationException($"The run finished without an '{ArtifactName}' artifact.");

            string zipPath = Path.Combine(WorkDir, "ipa.zip");
            await GitHubClient.DownloadAsync(artifact.archive_download_url, zipPath, p => state.Progress = p, token);
            string outputDir = Path.Combine(OutputDir, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            ZipFile.ExtractToDirectory(zipPath, outputDir);
            File.Delete(zipPath);
            Settings.IpaPath = Path.Combine(outputDir, "game.ipa").Replace('\\', '/');
            Settings.SaveChanges();

            try
            {
                await GitHubClient.SendAsync(UnityWebRequest.kHttpVerbDELETE, $"repos/{repo}/actions/artifacts/{artifact.id}");
            }
            catch (GitHubClient.RequestException ex)
            {
                // The IPA is already on disk; the artifact expires after a day anyway.
                Debug.LogWarning(LogPrefix + "Could not delete the artifact from GitHub: " + ex.Message);
            }

            return PipelineStep.Ready;
        }

        private static Task<PipelineStep> ExecuteAsync(PipelineState state, CancellationToken token)
        {
            return state.Step switch
            {
                PipelineStep.Export => Task.FromResult(Export(state)),
                PipelineStep.Zip => ZipAsync(state),
                PipelineStep.Upload => UploadAsync(state, token),
                PipelineStep.Dispatch => DispatchAsync(state),
                PipelineStep.Queued or PipelineStep.Building => MonitorAsync(state, token),
                PipelineStep.Download => DownloadAsync(state, token),
                _ => throw new InvalidOperationException($"Step {state.Step} cannot run."),
            };
        }

        private static PipelineStep Export(PipelineState state)
        {
            string xcodePath = Path.Combine(WorkDir, XcodeFolder);
            if (Directory.Exists(xcodePath))
            {
                Directory.Delete(xcodePath, true);
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = xcodePath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = Settings.DevelopmentBuild ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            // BuildPlayer logs invalid-argument errors itself and returns no report at all.
            if (report == null)
            {
                throw new InvalidOperationException("Xcode export failed before it started. See the Console for Unity's error.");
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                string error = report.steps
                    .SelectMany(s => s.messages)
                    .FirstOrDefault(m => m.type is LogType.Error or LogType.Exception)
                    .content;
                throw new InvalidOperationException("Xcode export failed: " + (error ?? report.summary.result.ToString()));
            }

            state.UsesPods = File.Exists(Path.Combine(xcodePath, "Podfile"));
            return PipelineStep.Zip;
        }

        private static async Task FindRunAsync(PipelineState state, CancellationToken token)
        {
            // The dispatch call returns no run id, so the run is matched by the request id in its run-name.
            DateTime deadline = DateTime.UtcNow + FindRunTimeout;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(FindRunInterval, token);
                var runs = await GitHubClient.GetAsync<GitHubClient.RunList>(
                    $"repos/{Settings.RepoFullName}/actions/runs?event=workflow_dispatch&per_page=10", token);
                GitHubClient.WorkflowRun run = runs.workflow_runs.FirstOrDefault(r => r.display_title.Contains(state.RequestId));
                if (run != null)
                {
                    state.RunId = run.id;
                    state.RunUrl = run.html_url;
                    state.SaveChanges();
                    return;
                }
            }

            throw new TimeoutException("The workflow run never appeared on GitHub. Check that Actions are enabled on the repository.");
        }

        private static void Finish(PipelineState state, string error)
        {
            if (error == null)
            {
                state.SetStep(PipelineStep.Ready);
                Debug.Log(LogPrefix + "IPA ready: " + Settings.IpaPath);
            }
            else
            {
                state.Fail(error);
                Debug.LogError($"{LogPrefix}Build failed at {state.FailedStep}: {error}");
            }

            Settings.AddHistory(new MacLessBuilderSettings.BuildRecord
            {
                Date = DateTime.Now.ToString(MacLessBuilderSettings.BuildRecord.DateFormat),
                Minutes = (int)Math.Ceiling(TimeSpan.FromTicks(DateTime.UtcNow.Ticks - state.StartedTicks).TotalMinutes),
                RunMinutes = state.RunMinutes,
                Result = error == null ? MacLessBuilderSettings.BuildRecord.Succeeded : $"Failed at {state.FailedStep}",
                RunUrl = state.RunUrl,
                IpaPath = error == null ? Settings.IpaPath : null,
            });
        }

        private static async Task<GitHubClient.Release> GetOrCreateInputReleaseAsync(string repo)
        {
            try
            {
                return await GitHubClient.GetAsync<GitHubClient.Release>($"repos/{repo}/releases/tags/{ReleaseTag}");
            }
            catch (GitHubClient.RequestException ex) when (ex.StatusCode == 404)
            {
                var body = new GitHubClient.CreateReleaseBody
                {
                    tag_name = ReleaseTag,
                    name = "iOS build input",
                    body = "Managed by the MacLess Builder Unity tool. Holds the latest exported Xcode project.",
                    prerelease = true,
                };
                return await GitHubClient.SendAsync<GitHubClient.Release>(
                    UnityWebRequest.kHttpVerbPOST, $"repos/{repo}/releases", JsonUtility.ToJson(body));
            }
        }

        private static void IgnoreBuildsFolder()
        {
            const string gitIgnore = ".gitignore";
            if (!File.Exists(gitIgnore) || IsBuildsIgnored(File.ReadAllLines(gitIgnore)))
            {
                return;
            }

            File.AppendAllText(gitIgnore, "\n/Builds/\n");
            Debug.Log(LogPrefix + "Added /Builds/ to .gitignore so downloaded IPAs stay out of version control.");
        }

        private static async Task<PipelineStep> MonitorAsync(PipelineState state, CancellationToken token)
        {
            if (state.RunId == 0)
            {
                await FindRunAsync(state, token);
            }

            var run = await GitHubClient.GetAsync<GitHubClient.WorkflowRun>(
                $"repos/{Settings.RepoFullName}/actions/runs/{state.RunId}", token);
            if (run.status != "completed")
            {
                await Task.Delay(RunPollInterval, token);
                return run.status == "in_progress" ? PipelineStep.Building : PipelineStep.Queued;
            }

            // Failed runs are billed too, so this is recorded before the conclusion check.
            state.RunMinutes = RunMinutes(run);
            state.SaveChanges();
            if (run.conclusion != "success")
            {
                throw new InvalidOperationException($"The cloud build finished as '{run.conclusion}'. Open the run log for details.");
            }

            return PipelineStep.Download;
        }

        private static void Resume()
        {
            if (PipelineState.instance.IsActive)
            {
                Run();
            }
        }

        private static void Run()
        {
            if (_cancellation != null)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _ = RunAsync(_cancellation);
        }

        private static async Task RunAsync(CancellationTokenSource cancellation)
        {
            PipelineState state = PipelineState.instance;
            CancellationToken token = cancellation.Token;
            try
            {
                while (state.IsActive)
                {
                    PipelineStep next = await ExecuteAsync(state, token);
                    token.ThrowIfCancellationRequested();
                    if (next == PipelineStep.Ready)
                    {
                        Finish(state, null);
                    }
                    else
                    {
                        state.SetStep(next);
                    }
                }
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Debug.LogException(ex);
                Finish(state, ex.Message);
            }
            catch (Exception)
            {
                // Cancelled: CancelBuild already recorded the result.
            }
            finally
            {
                if (_cancellation == cancellation)
                {
                    _cancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private static string ToEntryName(string root, string path)
        {
            return path.Substring(root.Length + 1).Replace('\\', '/');
        }

        private static async Task<PipelineStep> UploadAsync(PipelineState state, CancellationToken token)
        {
            string repo = Settings.RepoFullName;
            GitHubClient.Release release = await GetOrCreateInputReleaseAsync(repo);
            // One build runs at a time, so older exports (and a half-finished upload of this one) can all go.
            foreach (GitHubClient.ReleaseAsset asset in release.assets.Where(a => a.name.StartsWith("xcode-")))
            {
                await GitHubClient.SendAsync(UnityWebRequest.kHttpVerbDELETE, $"repos/{repo}/releases/assets/{asset.id}");
            }

            string assetName = Uri.EscapeDataString(Path.GetFileName(state.ZipPath));
            await GitHubClient.UploadAsync(
                $"https://uploads.github.com/repos/{repo}/releases/{release.id}/assets?name={assetName}",
                state.ZipPath,
                p => state.Progress = p,
                token);
            return PipelineStep.Dispatch;
        }

        private static async Task<PipelineStep> ZipAsync(PipelineState state)
        {
            foreach (string oldZip in Directory.GetFiles(WorkDir, "xcode-*.zip"))
            {
                File.Delete(oldZip);
            }

            string zipPath = Path.Combine(WorkDir, $"xcode-{state.RequestId}.zip");
            string sourceDir = Path.Combine(WorkDir, XcodeFolder);
            await Task.Run(() => CreateZip(sourceDir, zipPath));
            if (new FileInfo(zipPath).Length > MaxReleaseAssetBytes)
            {
                throw new InvalidOperationException("The zipped Xcode project is over GitHub's 2 GB release asset limit.");
            }

            state.ZipPath = zipPath;
            return PipelineStep.Upload;
        }
    }
}
