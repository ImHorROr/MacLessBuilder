using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HorrorTools.MacLessBuilder
{
    // Commits the ios-build workflow through the Contents API, so the user needs no local git.
    internal static class WorkflowInstaller
    {
        public const string FileName = "ios-build.yml";
        private const string CommitMessage = "Install MacLess Builder workflow";
        private const string RepoPath = ".github/workflows/" + FileName;
        private const string TemplatePath = "Editor/Templates/ios-build.yml.txt";

        public enum Status
        {
            Missing,
            Outdated,
            Current,
        }

        public static async Task<Status> CheckAsync(string repo)
        {
            GitHubClient.FileContent file = await GetRemoteAsync(repo);
            if (file == null)
            {
                return Status.Missing;
            }

            // The template's first line carries its version, so any template change reads as outdated.
            string remote = Encoding.UTF8.GetString(Convert.FromBase64String(file.content));
            return FirstLine(remote) == FirstLine(LoadTemplate()) ? Status.Current : Status.Outdated;
        }

        public static async Task InstallAsync(string repo)
        {
            GitHubClient.FileContent existing = await GetRemoteAsync(repo);
            string content = Convert.ToBase64String(Encoding.UTF8.GetBytes(LoadTemplate()));
            string json = existing == null
                ? JsonUtility.ToJson(new GitHubClient.FileBody { message = CommitMessage, content = content })
                : JsonUtility.ToJson(new GitHubClient.FileUpdateBody { message = CommitMessage, content = content, sha = existing.sha });
            await GitHubClient.SendAsync(UnityWebRequest.kHttpVerbPUT, $"repos/{repo}/contents/{RepoPath}", json);
        }

        internal static string LoadTemplate()
        {
            return File.ReadAllText(Path.Combine(PackagePaths.Root, TemplatePath)).Replace("\r\n", "\n");
        }

        private static string FirstLine(string text)
        {
            int end = text.IndexOf('\n');
            return (end < 0 ? text : text.Substring(0, end)).TrimEnd('\r');
        }

        private static async Task<GitHubClient.FileContent> GetRemoteAsync(string repo)
        {
            try
            {
                return await GitHubClient.GetAsync<GitHubClient.FileContent>($"repos/{repo}/contents/{RepoPath}");
            }
            catch (GitHubClient.RequestException ex) when (ex.StatusCode == 404)
            {
                return null;
            }
        }
    }
}
