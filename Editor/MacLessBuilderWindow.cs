using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace HorrorTools.MacLessBuilder
{
    internal partial class MacLessBuilderWindow : EditorWindow
    {
        private const int BuildRefreshMs = 1000;
        private const string ITunesStoreUrl = "ms-windows-store://pdp/?productid=9PB2MZ1ZMB1S";
        private const int SetupRefreshMs = 3000;
        private const int UsbmuxdPort = 27015;
        private static readonly PipelineStep[] DisplayedSteps =
        {
            PipelineStep.Export, PipelineStep.Zip, PipelineStep.Upload, PipelineStep.Dispatch,
            PipelineStep.Queued, PipelineStep.Building, PipelineStep.Download, PipelineStep.Ready,
        };

        private CancellationTokenSource _actionCancellation;
        private Button _buildButton;
        [NonSerialized] private string _busyMessage;
        private Button _cancelButton;
        [NonSerialized] private string _error;
        [NonSerialized] private UsageEstimator.Estimate? _estimate;
        private Button _helpButton;
        private Tab _helpTab;
        [NonSerialized] private string _historyKey;
        private VisualElement _historyRoot;
        private TextField _ipaField;
        private HelpBox _minutesBox;
        [NonSerialized] private string _minutesText;
        [NonSerialized] private GitHubClient.Repo _repo;
        private Button _runLogButton;
        [NonSerialized] private string _setupKey;
        private VisualElement _setupRoot;
        private HelpBox _statusBox;
        private VisualElement _stepsRoot;
        private TabView _tabs;
        [NonSerialized] private bool _usageFromLedger;
        [NonSerialized] private float? _usedLinuxMinutes;
        [NonSerialized] private GitHubClient.User _user;
        [NonSerialized] private string _userCode;
        private WorkflowInstaller.Status? _workflow;

        private static MacLessBuilderSettings Settings => MacLessBuilderSettings.instance;

        private bool CanBuild => _repo != null
            && _repo.permissions.push
            && _workflow == WorkflowInstaller.Status.Current
            && EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        private static void AddCheck(VisualElement parent, string text, bool ok, params VisualElement[] actions)
        {
            VisualElement row = CreateRow(ok ? "TestPassed" : "TestFailed", text);
            foreach (VisualElement action in actions)
            {
                row.Add(action);
            }

            parent.Add(row);
        }

        private static void AddWarning(VisualElement parent, string text)
        {
            parent.Add(new HelpBox(text, HelpBoxMessageType.Warning) { style = { marginLeft = 20 } });
        }

        private static VisualElement CreateClientIdRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 20, marginTop = 2 } };
            var field = new TextField("OAuth App client id")
            {
                value = GitHubAuth.ClientId,
                isDelayed = true,
                tooltip = "Only for Sign in with GitHub. GitHub > Settings > Developer settings > OAuth Apps > New OAuth App, with \"Enable Device Flow\" ticked.",
                style = { flexGrow = 1, flexShrink = 1 },
            };
            field.RegisterValueChangedCallback(e => GitHubAuth.ClientId = e.newValue);
            row.Add(field);
            row.Add(new Button(() => Application.OpenURL(GitHubAuth.NewOAuthAppUrl)) { text = "Create OAuth App" });
            return row;
        }

        private static VisualElement CreateHelpTab()
        {
            var root = new ScrollView();
            string imagesDir = PackagePaths.HelpImages;
            foreach (HelpPages.Page page in HelpPages.Pages)
            {
                var foldout = new Foldout { text = page.Title, value = false };
                for (int i = 0; i < page.Steps.Length; i++)
                {
                    foldout.Add(CreateWrappedLabel($"{i + 1}. {page.Steps[i]}"));
                    string imagePath = Path.Combine(imagesDir, $"{page.ImagePrefix}-{i + 1}.png");
                    if (File.Exists(imagePath))
                    {
                        var texture = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
                        texture.LoadImage(File.ReadAllBytes(imagePath));
                        foldout.Add(new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, style = { maxHeight = 400 } });
                    }
                }

                root.Add(foldout);
            }

            var troubleshooting = new Foldout { text = "Troubleshooting", value = false };
            foreach (HelpPages.Row row in HelpPages.Troubleshooting)
            {
                troubleshooting.Add(CreateWrappedLabel($"<b>{row.Symptom}</b>\n{row.Cause}. Fix: {row.Fix}."));
            }

            root.Add(troubleshooting);
            return root;
        }

        private static async Task CreateRepoAsync()
        {
            string name = Regex.Replace(PlayerSettings.productName, "[^A-Za-z0-9._-]+", "-").Trim('-') + "-ios-builds";
            var body = new GitHubClient.CreateRepoBody
            {
                name = name,
                description = "Cloud iOS builds made by the MacLess Builder Unity tool.",
                @private = true,
                // Releases need at least one commit to hang their tag on.
                auto_init = true,
            };
            Settings.SelectRepo(await GitHubClient.SendAsync<GitHubClient.Repo>(
                UnityWebRequest.kHttpVerbPOST, "user/repos", JsonUtility.ToJson(body)));
        }

        private static VisualElement CreateRow(string iconName, string text)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            row.Add(new Image
            {
                image = EditorGUIUtility.IconContent(iconName).image,
                style = { width = 16, height = 16, marginRight = 4, flexShrink = 0 },
            });
            row.Add(new Label(text) { style = { flexGrow = 1, flexShrink = 1, whiteSpace = WhiteSpace.Normal } });
            return row;
        }

        private static VisualElement CreateStepRow(PipelineState state, PipelineStep step, PipelineStep current)
        {
            if (step < current || state.Step == PipelineStep.Ready)
            {
                return CreateRow("TestPassed", step.ToString());
            }

            if (step > current)
            {
                return CreateRow("TestNormal", step.ToString());
            }

            if (state.Step == PipelineStep.Failed)
            {
                return CreateRow("TestFailed", step.ToString());
            }

            TimeSpan elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - state.StepStartedTicks);
            string progress = step is PipelineStep.Upload or PipelineStep.Download ? $"  {state.Progress:P0}" : string.Empty;
            return CreateRow($"WaitSpin{DateTime.Now.Second % 12:00}", $"{step}  {elapsed:m\\:ss}{progress}");
        }

        private static Label CreateWrappedLabel(string text)
        {
            return new Label(text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6, marginLeft = 4 } };
        }

        private static bool IsAppleDeviceServiceRunning()
        {
            // usbmuxd is served by Apple Mobile Device Service, which both iTunes and the Apple Devices app install.
            try
            {
                using (var client = new TcpClient())
                {
                    return client.ConnectAsync("127.0.0.1", UsbmuxdPort).Wait(500);
                }
            }
            catch (AggregateException)
            {
                return false;
            }
        }

        [MenuItem("Tools/MacLess Builder")]
        private static void Open()
        {
            GetWindow<MacLessBuilderWindow>("MacLess Builder");
        }

        private static void SwitchToIos()
        {
            if (EditorUtility.DisplayDialog(
                    "Switch to iOS",
                    "Switching the build target reimports assets and can take several minutes on large projects.",
                    "Switch",
                    "Cancel"))
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            }
        }

        private void CreateGUI()
        {
            // Credential Manager, usbmuxd on Windows and the helper exe are Windows-only, and a Mac has Xcode anyway.
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                rootVisualElement.Add(new HelpBox(
                    "MacLess Builder runs in the Windows Editor. On a Mac, build for iOS directly with Xcode.",
                    HelpBoxMessageType.Info));
                return;
            }

            _setupRoot = new ScrollView();
            var setupTab = new Tab("Setup");
            setupTab.Add(_setupRoot);

            var buildTab = new Tab("Build");
            buildTab.Add(CreateBuildTab());

            _helpTab = new Tab("Help");
            _helpTab.Add(CreateHelpTab());

            _tabs = new TabView();
            _tabs.Add(setupTab);
            _tabs.Add(buildTab);
            _tabs.Add(_helpTab);
            // Above the tabs, so a 2FA prompt is visible whichever tab started the sign-in.
            rootVisualElement.Add(CreateTwoFactorBox());
            rootVisualElement.Add(_tabs);

            rootVisualElement.schedule.Execute(RefreshSetup).Every(SetupRefreshMs);
            rootVisualElement.schedule.Execute(RefreshBuild).Every(BuildRefreshMs);
            RunAction("Checking GitHub...", () => Task.CompletedTask);
            _ = RefreshDevicesAsync();
        }

        private void OnDisable()
        {
            _actionCancellation?.Cancel();
            _helperCancellation?.Cancel();
        }

        private bool ConfirmCost()
        {
            string message;
            if (_estimate is not { } estimate)
            {
                message = "The remaining free build minutes could not be checked. A build takes roughly 15-20 macOS minutes, "
                    + "and GitHub bills minutes beyond your monthly allowance if a payment method is set.";
            }
            else if (estimate.Cost > 0)
            {
                message = $"This build will probably use about {estimate.BuildMinutes} macOS minutes, but only "
                    + $"{estimate.RemainingMacMinutes} free minutes are left this month. GitHub may charge about ${estimate.Cost:0.00}. "
                    + "If no payment method is on your GitHub account, GitHub blocks the build instead of charging.";
            }
            else if (_usageFromLedger && estimate.UsedFraction >= 0.8f)
            {
                message = $"Builds made with this tool have used about {estimate.UsedFraction:P0} of this month's free macOS minutes, "
                    + "and usage from other repositories is not counted, so this build may be billed.";
            }
            else
            {
                return true;
            }

            switch (EditorUtility.DisplayDialogComplex("MacLess Builder", message, "Build anyway", "Cancel", "Open GitHub billing"))
            {
                case 0:
                    return true;
                case 2:
                    Application.OpenURL(UsageEstimator.BillingUrl);
                    return false;
                default:
                    return false;
            }
        }

        private Button CreateActionButton(string text, string busyMessage, Func<Task> action)
        {
            return new Button(() => RunAction(busyMessage, action)) { text = text };
        }

        private VisualElement CreateBuildTab()
        {
            var root = new ScrollView();

            _minutesBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _minutesBox.Add(new Button(() => Application.OpenURL(UsageEstimator.BillingUrl)) { text = "Billing" });
            root.Add(_minutesBox);
            root.Add(CreateSettings());

            var development = new Toggle("Development build") { value = Settings.DevelopmentBuild };
            development.RegisterValueChangedCallback(e =>
            {
                Settings.DevelopmentBuild = e.newValue;
                Settings.SaveChanges();
            });
            root.Add(development);

            _buildButton = new Button(StartBuild) { text = "Build & Send to Cloud", style = { height = 32 } };
            root.Add(_buildButton);

            _stepsRoot = new VisualElement { style = { marginTop = 6, marginBottom = 6 } };
            root.Add(_stepsRoot);

            _statusBox = new HelpBox();
            root.Add(_statusBox);

            var runButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _runLogButton = new Button(() => Application.OpenURL(PipelineState.instance.RunUrl)) { text = "View run log" };
            _cancelButton = new Button(PipelineRunner.CancelBuild) { text = "Cancel build" };
            _helpButton = new Button(() => _tabs.activeTab = _helpTab) { text = "Troubleshooting" };
            runButtons.Add(_runLogButton);
            runButtons.Add(_cancelButton);
            runButtons.Add(_helpButton);
            root.Add(runButtons);

            var ipaRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            _ipaField = new TextField("IPA") { isDelayed = true, style = { flexGrow = 1, flexShrink = 1 } };
            _ipaField.RegisterValueChangedCallback(e =>
            {
                Settings.IpaPath = e.newValue;
                Settings.SaveChanges();
            });
            ipaRow.Add(_ipaField);
            ipaRow.Add(new Button(() =>
            {
                string path = EditorUtility.OpenFilePanel("Select IPA", "Builds/iOS", "ipa");
                if (!string.IsNullOrEmpty(path))
                {
                    _ipaField.value = path;
                }
            }) { text = "Browse" });
            ipaRow.Add(new Button(() => EditorUtility.RevealInFinder(Settings.IpaPath)) { text = "Show" });
            root.Add(ipaRow);

            root.Add(CreateInstallPanel());

            root.Add(new Label("History") { style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold } });
            _historyRoot = new VisualElement();
            root.Add(_historyRoot);
            return root;
        }

        private VisualElement CreateSettings()
        {
            var foldout = new Foldout { text = "Settings", value = false };
            var included = new IntegerField("Included minutes (0 = from plan)")
            {
                value = Settings.IncludedMinutesOverride,
                tooltip = "Monthly Actions allowance in Linux minutes. GitHub's defaults: Free 2,000, Pro 3,000.",
            };
            included.RegisterValueChangedCallback(e =>
            {
                Settings.IncludedMinutesOverride = Math.Max(0, e.newValue);
                Settings.SaveChanges();
                UpdateEstimate();
            });
            var price = new FloatField("macOS price per minute ($)") { value = Settings.MacPricePerMinute };
            price.RegisterValueChangedCallback(e =>
            {
                Settings.MacPricePerMinute = Math.Max(0, e.newValue);
                Settings.SaveChanges();
                UpdateEstimate();
            });
            var anisette = new TextField("Anisette server (empty = default)")
            {
                value = Settings.AnisetteUrl,
                isDelayed = true,
                tooltip = "Apple's sign-in needs device data from an anisette server. Public servers go down now and then; paste another one here.",
            };
            anisette.RegisterValueChangedCallback(e =>
            {
                Settings.AnisetteUrl = e.newValue?.Trim();
                Settings.SaveChanges();
            });
            foldout.Add(included);
            foldout.Add(price);
            foldout.Add(anisette);
            return foldout;
        }

        private async Task PickRepoAsync(Rect anchor)
        {
            try
            {
                // ponytail: first 100 repos by last update; page through if someone owns more than that.
                GitHubClient.Repo[] repos = await GitHubClient.GetArrayAsync<GitHubClient.Repo>(
                    "user/repos?affiliation=owner&sort=updated&per_page=100");
                var menu = new GenericDropdownMenu();
                foreach (GitHubClient.Repo repo in repos.Where(r => r.permissions.push))
                {
                    string label = repo.@private ? repo.full_name : repo.full_name + " (public)";
                    menu.AddItem(label, repo.full_name == Settings.RepoFullName, () =>
                    {
                        Settings.SelectRepo(repo);
                        RunAction("Checking repository...", () => Task.CompletedTask);
                    });
                }

#if UNITY_6000_3_OR_NEWER
                menu.DropDown(anchor, rootVisualElement, DropdownMenuSizeMode.Auto);
#else
                // Unity 6.0 only has this overload; 6.3 marks it obsolete.
                menu.DropDown(anchor, rootVisualElement, false);
#endif
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        private void RefreshBuild()
        {
            PipelineState state = PipelineState.instance;
            _buildButton.SetEnabled(CanBuild && !state.IsActive);
            _cancelButton.style.display = state.IsActive ? DisplayStyle.Flex : DisplayStyle.None;
            _runLogButton.style.display = string.IsNullOrEmpty(state.RunUrl) ? DisplayStyle.None : DisplayStyle.Flex;
            _helpButton.style.display = state.Step == PipelineStep.Failed ? DisplayStyle.Flex : DisplayStyle.None;
            _minutesBox.text = _minutesText;
            _minutesBox.style.display = _minutesText == null ? DisplayStyle.None : DisplayStyle.Flex;

            (string message, HelpBoxMessageType type) = state.Step switch
            {
                PipelineStep.Failed => (state.Error, HelpBoxMessageType.Error),
                PipelineStep.Ready => ("IPA ready. Plug in your iPhone and click Install on iPhone.", HelpBoxMessageType.Info),
                _ when !CanBuild && !state.IsActive => ("Finish the Setup tab first.", HelpBoxMessageType.Warning),
                _ => (null, HelpBoxMessageType.None),
            };
            _statusBox.text = message;
            _statusBox.messageType = type;
            _statusBox.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;

            _stepsRoot.Clear();
            if (state.Step != PipelineStep.Idle)
            {
                PipelineStep current = state.Step == PipelineStep.Failed ? state.FailedStep : state.Step;
                foreach (PipelineStep step in DisplayedSteps)
                {
                    _stepsRoot.Add(CreateStepRow(state, step, current));
                }
            }

            if (_ipaField.panel?.focusController?.focusedElement != _ipaField)
            {
                _ipaField.SetValueWithoutNotify(Settings.IpaPath);
            }

            RefreshHistory();
            RefreshInstallPanel();
        }

        private async Task RefreshChecksAsync()
        {
            _user = null;
            _repo = null;
            _workflow = null;
            try
            {
                if (!GitHubClient.HasToken)
                {
                    return;
                }

                _user = await GitHubClient.GetAsync<GitHubClient.User>("user");
                if (!Settings.HasRepo)
                {
                    return;
                }

                GitHubClient.Repo repo = await GitHubClient.GetAsync<GitHubClient.Repo>($"repos/{Settings.RepoFullName}");
                Settings.SelectRepo(repo);
                _workflow = await WorkflowInstaller.CheckAsync(Settings.RepoFullName);
                _repo = repo;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                await RefreshUsageAsync();
            }
        }

        private void RefreshHistory()
        {
            string key = string.Join("|", Settings.History.Select(r => r.Date + r.Result));
            if (key == _historyKey)
            {
                return;
            }

            // A finished build changes both the history and the minutes used.
            if (_historyKey != null)
            {
                _ = RefreshUsageAsync();
            }

            _historyKey = key;
            _historyRoot.Clear();
            foreach (MacLessBuilderSettings.BuildRecord record in Settings.History)
            {
                string runMinutes = record.RunMinutes > 0 ? $" ({record.RunMinutes} min on the runner)" : string.Empty;
                VisualElement row = CreateRow(
                    record.Result == MacLessBuilderSettings.BuildRecord.Succeeded ? "TestPassed" : "TestFailed",
                    $"{record.Date}   {record.Result}   {record.Minutes} min{runMinutes}");
                if (!string.IsNullOrEmpty(record.RunUrl))
                {
                    row.Add(new Button(() => Application.OpenURL(record.RunUrl)) { text = "Log" });
                }

                if (!string.IsNullOrEmpty(record.IpaPath) && File.Exists(record.IpaPath))
                {
                    row.Add(new Button(() =>
                    {
                        Settings.IpaPath = record.IpaPath;
                        Settings.SaveChanges();
                        _ipaField.SetValueWithoutNotify(record.IpaPath);
                        _ = InstallAsync(false);
                    }) { text = "Reinstall" });
                }

                _historyRoot.Add(row);
            }
        }

        private void RefreshSetup()
        {
            bool deviceSupport = IsAppleDeviceServiceRunning();
            bool hasToken = GitHubClient.HasToken;
            bool iosSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);
            bool isIos = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            bool deviceSdk = PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK;
            bool hasScenes = EditorBuildSettings.scenes.Any(s => s.enabled);
            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            // Rebuilding only on change keeps buttons from flickering under the mouse.
            string key = string.Join("|", deviceSupport, hasToken, iosSupported, isIos, deviceSdk, hasScenes, bundleId, SideloadKey,
                _user?.login, _repo?.full_name, _workflow, _busyMessage, _error, _userCode, Settings.RepoFullName);
            if (key == _setupKey)
            {
                return;
            }

            _setupKey = key;
            _setupRoot.Clear();

            if (_busyMessage != null)
            {
                var busy = new HelpBox(_busyMessage, HelpBoxMessageType.Info);
                busy.Add(new Button(() => _actionCancellation?.Cancel()) { text = "Cancel" });
                _setupRoot.Add(busy);
            }

            if (_userCode != null)
            {
                _setupRoot.Add(new Label(_userCode)
                {
                    style = { fontSize = 28, unityFontStyleAndWeight = FontStyle.Bold, alignSelf = Align.Center },
                });
            }

            if (_error != null)
            {
                _setupRoot.Add(new HelpBox(_error, HelpBoxMessageType.Error));
            }

            var checks = new VisualElement();
            checks.SetEnabled(_busyMessage == null);
            _setupRoot.Add(checks);

            if (_user != null)
            {
                AddCheck(checks, $"GitHub connected as {_user.login}", true, new Button(SignOut) { text = "Sign out" });
            }
            else if (hasToken)
            {
                AddCheck(
                    checks,
                    "GitHub connected",
                    false,
                    CreateActionButton("Re-check", "Checking GitHub...", () => Task.CompletedTask),
                    new Button(SignOut) { text = "Sign out" });
            }
            else
            {
                AddCheck(
                    checks,
                    $"GitHub connected (asks for scopes: {GitHubAuth.Scopes})",
                    false,
                    CreateActionButton("Sign in with GitHub", "Starting GitHub sign-in...", SignInAsync),
                    CreateActionButton("Use Git's login", "Asking Git Credential Manager for its GitHub login...", GitHubAuth.SignInWithGitAsync));
                checks.Add(CreateClientIdRow());
            }

            if (_user != null)
            {
                bool repoOk = _repo != null && _repo.permissions.push;
                Button pick = null;
                pick = new Button(() => _ = PickRepoAsync(pick.worldBound)) { text = Settings.HasRepo ? "Change" : "Pick existing" };
                if (repoOk)
                {
                    AddCheck(checks, $"Repository {_repo.full_name}", true, pick);
                }
                else
                {
                    AddCheck(
                        checks,
                        "Repository with push access selected",
                        false,
                        CreateActionButton("Create private repo", "Creating repository...", CreateRepoAsync),
                        pick);
                }
                if (_repo != null && !_repo.@private)
                {
                    AddWarning(checks, "This repository is public: anyone can download the exported Xcode project and the IPA.");
                }

                if (repoOk && _workflow == WorkflowInstaller.Status.Current)
                {
                    AddCheck(checks, "iOS workflow installed", true);
                }
                else if (repoOk)
                {
                    bool outdated = _workflow == WorkflowInstaller.Status.Outdated;
                    AddCheck(
                        checks,
                        outdated ? "iOS workflow is out of date" : "iOS workflow installed",
                        false,
                        CreateActionButton(
                            outdated ? "Upgrade" : "Init iOS workflow",
                            "Committing the workflow...",
                            () => WorkflowInstaller.InstallAsync(Settings.RepoFullName)));
                }
            }

            if (!iosSupported)
            {
                AddCheck(checks, "iOS Build Support module installed (add it in Unity Hub > Installs > Add modules)", false);
            }
            else
            {
                AddCheck(checks, "Build target is iOS", isIos, new Button(SwitchToIos) { text = "Switch to iOS" });
            }

            if (isIos && !deviceSdk)
            {
                AddWarning(checks, "Player Settings > iOS > Target SDK is Simulator SDK; the cloud build targets real devices.");
            }

            if (!hasScenes)
            {
                AddWarning(checks, "No scenes are enabled in Build Profiles.");
            }

            if (string.IsNullOrEmpty(bundleId) || bundleId == "com.Company.ProductName")
            {
                AddWarning(checks, "Set a bundle identifier in Player Settings > iOS and keep it between builds: a free Apple ID can only create a few App IDs per week.");
            }

            checks.Add(new Label("Needed only to install on a phone") { style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold } });
            AddCheck(
                checks,
                "Apple device support (iTunes or Apple Devices)",
                deviceSupport,
                new Button(() => Application.OpenURL(ITunesStoreUrl)) { text = "Get iTunes" });
            AddSideloadChecks(checks);
        }

        private async Task RefreshUsageAsync()
        {
            _usedLinuxMinutes = null;
            if (_user != null && _repo != null && _repo.@private)
            {
                DateTime now = DateTime.UtcNow;
                try
                {
                    var summary = await GitHubClient.GetAsync<GitHubClient.UsageSummary>(
                        $"users/{_user.login}/settings/billing/usage/summary?year={now.Year}&month={now.Month}&product=actions");
                    _usedLinuxMinutes = UsageEstimator.LinuxEquivalentMinutes(summary.usageItems ?? Array.Empty<GitHubClient.UsageItem>());
                    _usageFromLedger = false;
                }
                catch (Exception ex) when (ex is GitHubClient.RequestException or ArgumentException)
                {
                    // The billing endpoint can refuse this token; fall back to this tool's own build history.
                    _usedLinuxMinutes = UsageEstimator.LedgerLinuxMinutes(Settings.History, DateTime.Now);
                    _usageFromLedger = true;
                }
            }

            UpdateEstimate();
        }

        private void RunAction(string busyMessage, Func<Task> action)
        {
            _ = RunActionAsync(busyMessage, action);
        }

        private async Task RunActionAsync(string busyMessage, Func<Task> action)
        {
            if (_busyMessage != null)
            {
                return;
            }

            _busyMessage = busyMessage;
            _error = null;
            _actionCancellation = new CancellationTokenSource();
            RefreshSetup();
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _busyMessage = null;
                _userCode = null;
                _actionCancellation.Dispose();
                _actionCancellation = null;
            }

            await RefreshChecksAsync();
            RefreshSetup();
        }

        private Task SignInAsync()
        {
            return GitHubAuth.SignInAsync((userCode, verificationUri) =>
            {
                _userCode = userCode;
                _busyMessage = $"Enter this code at {verificationUri} (copied to the clipboard), then approve the access.";
                EditorGUIUtility.systemCopyBuffer = userCode;
                Application.OpenURL(verificationUri);
                RefreshSetup();
            }, _actionCancellation.Token);
        }

        private void SignOut()
        {
            GitHubAuth.SignOut();
            RunAction("Signing out...", () => Task.CompletedTask);
        }

        private void StartBuild()
        {
            if (!_repo.@private || ConfirmCost())
            {
                PipelineRunner.StartBuild();
            }
        }

        private void UpdateEstimate()
        {
            _estimate = null;
            if (_repo == null)
            {
                _minutesText = null;
                return;
            }

            if (!_repo.@private)
            {
                _minutesText = "Unlimited build minutes: public repositories run on GitHub's standard runners for free.";
                return;
            }

            if (_usedLinuxMinutes == null)
            {
                _minutesText = "Checking build minutes...";
                return;
            }

            // GitHub only returns the plan to tokens with the "user" scope, which this tool does not ask for.
            string planName = _user?.plan?.name;
            string plan = string.IsNullOrEmpty(planName)
                ? "Free (assumed)"
                : char.ToUpperInvariant(planName[0]) + planName.Substring(1);
            int included = Settings.IncludedMinutesOverride > 0 ? Settings.IncludedMinutesOverride : UsageEstimator.IncludedMinutes(planName);
            UsageEstimator.Estimate estimate = UsageEstimator.Calculate(
                included, _usedLinuxMinutes.Value, UsageEstimator.AverageBuildMinutes(Settings.History), Settings.MacPricePerMinute);
            _estimate = estimate;

            DateTime reset = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
            _minutesText = $"{plan} plan, private repo. About {estimate.RemainingMacMinutes} "
                + $"macOS minutes left this month (resets {reset:MMM d}). Average build {estimate.BuildMinutes} min, "
                + $"so about {estimate.FreeBuildsLeft} more free builds."
                + (_usageFromLedger
                    ? "\nEstimated from builds made with this tool only; usage from other repositories is not included."
                    : string.Empty);
        }
    }
}
