using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HelperEvent = HorrorTools.MacLessBuilder.SideloadHelper.HelperEvent;

namespace HorrorTools.MacLessBuilder
{
    // Apple ID, device list, two-factor prompt and install, all through sideload-helper.exe.
    internal partial class MacLessBuilderWindow
    {
        private const int ExpiryWarningDays = 2;
        private const int FreeSigningDays = 7;

        [NonSerialized] private bool _appleIdFormVisible;
        private DropdownField _deviceDropdown;
        [NonSerialized] private List<HelperEvent> _devices;
        [NonSerialized] private string _devicesError;
        [NonSerialized] private bool _devicesLoading;
        [NonSerialized] private string _formEmail;
        [NonSerialized] private string _formPassword;
        [NonSerialized] private bool _formRemember = true;
        [NonSerialized] private SideloadHelper _helper;
        private CancellationTokenSource _helperCancellation;
        private HelpBox _installBox;
        private Button _installButton;
        private HelpBox _lastInstallBox;
        [NonSerialized] private string _sessionEmail;
        [NonSerialized] private string _sessionPassword;
        private VisualElement _twoFactorBox;
        private TextField _twoFactorCode;
        private Label _twoFactorLabel;

        private static string SavedAppleId => CredentialStore.ReadUserName(SideloadHelper.AppleIdTarget);

        private IEnumerable<HelperEvent> ConnectedDevices => _devices?.Where(d => d.error == null) ?? Enumerable.Empty<HelperEvent>();

        private HelperEvent SelectedDevice => ConnectedDevices.FirstOrDefault(d => d.udid == Settings.DeviceUdid)
            ?? ConnectedDevices.FirstOrDefault();

        private string SideloadKey => string.Join("|", SavedAppleId, _sessionEmail, _appleIdFormVisible, _devicesLoading, _devicesError,
            _helper != null, _devices == null ? "?" : string.Join(",", _devices.Select(d => d.udid + d.devMode + d.error)));

        private static string DeviceLabel(HelperEvent device)
        {
            return $"{device.name} (iOS {device.ios})";
        }

        private static HelperEvent Failure(string message)
        {
            return new HelperEvent { @event = HelperEvent.Error, message = message };
        }

        private static string StageText(string stage)
        {
            return stage switch
            {
                "login" => "Signing in to Apple",
                "device" => "Checking the iPhone",
                "register" => "Registering the iPhone with Apple",
                "sign" => "Signing the app",
                "transfer" => "Copying to the iPhone",
                "install" => "Installing",
                _ => stage,
            };
        }

        private static string[] WithAnisette(IEnumerable<string> arguments)
        {
            List<string> list = arguments.ToList();
            if (!string.IsNullOrEmpty(Settings.AnisetteUrl))
            {
                list.Add("--anisette");
                list.Add(Settings.AnisetteUrl);
            }

            return list.ToArray();
        }

        private void AddSideloadChecks(VisualElement checks)
        {
            Button Recheck() => new Button(() => _ = RefreshDevicesAsync()) { text = _devicesLoading ? "Checking..." : "Re-check" };
            Button HelpButton() => new Button(() => _tabs.activeTab = _helpTab) { text = "Help" };

            string savedEmail = SavedAppleId;
            if (savedEmail != null || _sessionEmail != null)
            {
                string label = savedEmail != null ? $"Apple ID saved: {savedEmail}" : $"Apple ID for this session: {_sessionEmail}";
                AddCheck(checks, label, true, new Button(ForgetAppleId) { text = "Forget" });
            }
            else
            {
                AddCheck(checks, "Apple ID entered (a free one works)", false, new Button(() =>
                {
                    _appleIdFormVisible = !_appleIdFormVisible;
                    RefreshSetup();
                }) { text = "Enter Apple ID" });
                if (_appleIdFormVisible)
                {
                    checks.Add(CreateAppleIdForm());
                }
            }

            if (!File.Exists(SideloadHelper.ExePath))
            {
                AddCheck(checks, "sideload-helper.exe is in the package (build it from Helper~ into Editor/Bin, see the README)", false);
                return;
            }

            if (_devicesError != null)
            {
                AddCheck(checks, _devicesError, false, Recheck(), HelpButton());
            }
            else if (!ConnectedDevices.Any() && (_devices == null || _devices.Count == 0))
            {
                string text = _devices == null
                    ? "iPhone connected and trusted"
                    : "No iPhone found. Plug it in with a data cable, unlock it and tap Trust.";
                AddCheck(checks, text, false, Recheck(), HelpButton());
            }

            foreach (HelperEvent device in _devices ?? new List<HelperEvent>())
            {
                if (device.error != null)
                {
                    AddCheck(checks, SideloadHelper.Explain(device.error), false, Recheck(), HelpButton());
                    continue;
                }

                AddCheck(checks, $"{DeviceLabel(device)} connected and trusted", true, Recheck());
                if (device.devMode == "off")
                {
                    // Not a blocker: the app installs, it just will not open until Developer Mode is on.
                    AddCheck(checks, $"Developer Mode is off on {device.name}", false, HelpButton());
                }
                else if (device.devMode == "on")
                {
                    AddCheck(checks, $"Developer Mode is on for {device.name}", true);
                }
            }
        }

        private VisualElement CreateAppleIdForm()
        {
            var form = new VisualElement { style = { marginLeft = 20, marginBottom = 6 } };
            var email = new TextField("Apple ID email") { value = _formEmail };
            email.RegisterValueChangedCallback(e => _formEmail = e.newValue);
            var password = new TextField("Password") { value = _formPassword, isPasswordField = true };
            password.RegisterValueChangedCallback(e => _formPassword = e.newValue);
            var remember = new Toggle("Remember on this PC") { value = _formRemember };
            remember.RegisterValueChangedCallback(e => _formRemember = e.newValue);
            form.Add(email);
            form.Add(password);
            form.Add(remember);
            form.Add(CreateWrappedLabel(
                "The password goes only to Apple and is stored only in Windows Credential Manager on this PC. "
                + "A free Apple ID works; a separate one just for sideloading keeps your main account's certificates untouched."));

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(CreateActionButton("Save & verify", "Signing in to Apple...", SaveAppleIdAsync));
            buttons.Add(new Button(() =>
            {
                _formPassword = null;
                _appleIdFormVisible = false;
                RefreshSetup();
            }) { text = "Cancel" });
            form.Add(buttons);
            return form;
        }

        private VisualElement CreateInstallPanel()
        {
            var panel = new VisualElement { style = { marginTop = 10 } };
            var deviceRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _deviceDropdown = new DropdownField("iPhone") { style = { flexGrow = 1, flexShrink = 1 } };
            _deviceDropdown.RegisterValueChangedCallback(e =>
            {
                HelperEvent chosen = ConnectedDevices.FirstOrDefault(d => DeviceLabel(d) == e.newValue);
                if (chosen != null)
                {
                    Settings.DeviceUdid = chosen.udid;
                    Settings.SaveChanges();
                }
            });
            deviceRow.Add(_deviceDropdown);
            deviceRow.Add(new Button(() => _ = RefreshDevicesAsync()) { text = "Refresh" });
            panel.Add(deviceRow);

            _installButton = new Button(() => _ = InstallAsync(false)) { text = "Install on iPhone", style = { height = 26 } };
            panel.Add(_installButton);
            _installBox = new HelpBox { style = { display = DisplayStyle.None } };
            panel.Add(_installBox);
            _lastInstallBox = new HelpBox { style = { display = DisplayStyle.None } };
            panel.Add(_lastInstallBox);
            UpdateDeviceDropdown();
            return panel;
        }

        private VisualElement CreateTwoFactorBox()
        {
            _twoFactorBox = new VisualElement
            {
                style =
                {
                    display = DisplayStyle.None,
                    backgroundColor = new Color(1f, 0.75f, 0.2f, 0.15f),
                    paddingLeft = 6, paddingRight = 6, paddingTop = 6, paddingBottom = 6,
                    marginLeft = 4, marginRight = 4, marginTop = 4,
                },
            };
            _twoFactorLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, unityFontStyleAndWeight = FontStyle.Bold } };
            _twoFactorCode = new TextField("Code") { maxLength = 12 };
            _twoFactorCode.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    SendTwoFactor(_twoFactorCode.value.Trim());
                }
            });

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            buttons.Add(new Button(() => SendTwoFactor(_twoFactorCode.value.Trim())) { text = "Submit" });
            buttons.Add(new Button(() => SendTwoFactor("!devices")) { text = "Send to my devices" });
            buttons.Add(new Button(() => SendTwoFactor("!resend")) { text = "Resend" });
            buttons.Add(new Button(() => SendTwoFactor("!abort")) { text = "Cancel" });

            _twoFactorBox.Add(_twoFactorLabel);
            _twoFactorBox.Add(_twoFactorCode);
            _twoFactorBox.Add(buttons);
            return _twoFactorBox;
        }

        private void ForgetAppleId()
        {
            CredentialStore.Delete(SideloadHelper.AppleIdTarget);
            _sessionEmail = null;
            _sessionPassword = null;
            RefreshSetup();
        }

        private void HandleTwoFactor(HelperEvent helperEvent)
        {
            if (helperEvent.@event == HelperEvent.NeedTwoFactor)
            {
                string prompt = helperEvent.sms
                    ? "Apple ID two-factor: enter the code Apple texted to your trusted phone number."
                    : "Apple ID two-factor: enter the 6-digit code shown on your other Apple devices.";
                _twoFactorLabel.text = (helperEvent.retry ? "That did not work. " : string.Empty) + prompt
                    + (string.IsNullOrEmpty(helperEvent.lastError) ? string.Empty : "\n" + helperEvent.lastError);
                _twoFactorCode.value = string.Empty;
                _twoFactorBox.style.display = DisplayStyle.Flex;
                _twoFactorCode.Focus();
            }
            else if (helperEvent.@event is HelperEvent.LoggedIn or HelperEvent.Done or HelperEvent.Error)
            {
                _twoFactorBox.style.display = DisplayStyle.None;
            }
        }

        private async Task InstallAsync(bool revokeCertificates)
        {
            string savedEmail = SavedAppleId;
            string email = savedEmail ?? _sessionEmail;
            string password = savedEmail != null ? CredentialStore.Read(SideloadHelper.AppleIdTarget) : _sessionPassword;
            if (email == null || string.IsNullOrEmpty(password))
            {
                ShowInstallStatus("Enter your Apple ID on the Setup tab first.", HelpBoxMessageType.Error);
                return;
            }

            if (!File.Exists(Settings.IpaPath))
            {
                ShowInstallStatus($"No IPA at '{Settings.IpaPath}'. Build one, or browse to an existing IPA.", HelpBoxMessageType.Error);
                return;
            }

            if (SelectedDevice == null)
            {
                await RefreshDevicesAsync();
            }

            HelperEvent device = SelectedDevice;
            if (device == null)
            {
                ShowInstallStatus(_devicesError ?? "No iPhone found. Plug it in with a data cable, unlock it, tap Trust, then click Refresh.", HelpBoxMessageType.Error);
                return;
            }

            var arguments = new List<string> { "install", "--email", email, "--ipa", Path.GetFullPath(Settings.IpaPath), "--udid", device.udid };
            if (revokeCertificates)
            {
                arguments.Add("--revoke-certs");
            }

            ShowInstallStatus("Starting...", HelpBoxMessageType.Info);
            HelperEvent result = await RunHelperAsync(helperEvent =>
            {
                if (helperEvent.@event == HelperEvent.Progress)
                {
                    string percent = helperEvent.percent >= 0 ? $" {helperEvent.percent}%" : "...";
                    ShowInstallStatus(StageText(helperEvent.stage) + percent, HelpBoxMessageType.Info);
                }
            }, password, WithAnisette(arguments));

            if (result.@event == HelperEvent.Done)
            {
                Settings.LastInstallDevice = device.name;
                Settings.LastInstallTicks = DateTime.Now.Ticks;
                Settings.SaveChanges();
                ShowInstallStatus(
                    $"Installed on {device.name}. The first time, trust the developer on the phone: Help > Trust the app the first time.",
                    HelpBoxMessageType.Info);
            }
            else if (!revokeCertificates
                     && SideloadHelper.IsCertificateLimit(result.message)
                     && EditorUtility.DisplayDialog(
                         "Certificate limit reached",
                         "This Apple ID already has the maximum number of development certificates. Revoke them and create a new one?\n\n"
                         + "Apps signed with the revoked certificates, for example by iloader or Xcode on another computer, stop opening until they are reinstalled.",
                         "Revoke and install",
                         "Cancel"))
            {
                await InstallAsync(true);
            }
            else
            {
                ShowInstallStatus(SideloadHelper.Explain(result.message), HelpBoxMessageType.Error);
            }
        }

        private async Task RefreshDevicesAsync()
        {
            if (_devicesLoading || _helper != null || !File.Exists(SideloadHelper.ExePath))
            {
                return;
            }

            _devicesLoading = true;
            UpdateDeviceDropdown();
            var found = new List<HelperEvent>();
            HelperEvent result = await RunHelperAsync(helperEvent =>
            {
                if (helperEvent.@event == HelperEvent.Device)
                {
                    found.Add(helperEvent);
                }
            }, null, new[] { "devices" });
            _devices = found;
            _devicesError = result.@event == HelperEvent.Error ? SideloadHelper.Explain(result.message) : null;
            _devicesLoading = false;
            UpdateDeviceDropdown();
        }

        private void RefreshInstallPanel()
        {
            _installButton.SetEnabled(_helper == null);
            _installButton.text = _helper != null && !_devicesLoading ? "Working..." : "Install on iPhone";

            if (Settings.LastInstallTicks <= 0)
            {
                return;
            }

            var installed = new DateTime(Settings.LastInstallTicks);
            DateTime expires = installed.AddDays(FreeSigningDays);
            double daysLeft = (expires - DateTime.Now).TotalDays;
            _lastInstallBox.text = daysLeft <= 0
                ? $"The build installed on {Settings.LastInstallDevice} on {installed:MMM d} has expired. Click Reinstall in the History list; no new cloud build is needed."
                : $"Installed on {Settings.LastInstallDevice} on {installed:MMM d, HH:mm}. Free Apple ID apps stop opening after {FreeSigningDays} days (around {expires:MMM d}).";
            _lastInstallBox.messageType = daysLeft <= ExpiryWarningDays ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            _lastInstallBox.style.display = DisplayStyle.Flex;
        }

        private async Task<HelperEvent> RunHelperAsync(SideloadHelper.HelperEventHandler onEvent, string stdinFirstLine, string[] arguments)
        {
            if (_helper != null)
            {
                return Failure("Another install or sign-in is still running.");
            }

            _helperCancellation = new CancellationTokenSource();
            try
            {
                using (_helper = SideloadHelper.Start(stdinFirstLine, arguments))
                {
                    return await _helper.RunAsync(helperEvent =>
                    {
                        HandleTwoFactor(helperEvent);
                        onEvent?.Invoke(helperEvent);
                    }, _helperCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return Failure("Cancelled.");
            }
            catch (Exception ex) when (ex is IOException or Win32Exception)
            {
                return Failure(ex.Message);
            }
            finally
            {
                _helper = null;
                _twoFactorBox.style.display = DisplayStyle.None;
                _helperCancellation.Dispose();
                _helperCancellation = null;
            }
        }

        private async Task SaveAppleIdAsync()
        {
            string email = _formEmail?.Trim();
            string password = _formPassword;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Enter the Apple ID email and password.");
            }

            HelperEvent result;
            // The busy box's Cancel button should also stop a sign-in waiting for its 2FA code.
            using (_actionCancellation.Token.Register(() => _helperCancellation?.Cancel()))
            {
                result = await RunHelperAsync(null, password, WithAnisette(new[] { "login", "--email", email }));
            }

            if (result.@event != HelperEvent.Done)
            {
                throw new InvalidOperationException(SideloadHelper.Explain(result.message));
            }

            if (_formRemember)
            {
                CredentialStore.Write(SideloadHelper.AppleIdTarget, email, password);
            }
            else
            {
                _sessionEmail = email;
                _sessionPassword = password;
            }

            _formPassword = null;
            _appleIdFormVisible = false;
        }

        private void SendTwoFactor(string answer)
        {
            if (string.IsNullOrEmpty(answer))
            {
                return;
            }

            _helper?.Send(answer);
            _twoFactorBox.style.display = DisplayStyle.None;
        }

        private void ShowInstallStatus(string text, HelpBoxMessageType type)
        {
            _installBox.text = text;
            _installBox.messageType = type;
            _installBox.style.display = DisplayStyle.Flex;
        }

        private void UpdateDeviceDropdown()
        {
            if (_deviceDropdown == null)
            {
                return;
            }

            _deviceDropdown.choices = ConnectedDevices.Select(DeviceLabel).ToList();
            HelperEvent selected = SelectedDevice;
            string placeholder = _devicesLoading ? "Checking..." : "No iPhone found";
            _deviceDropdown.SetValueWithoutNotify(selected != null ? DeviceLabel(selected) : placeholder);
        }
    }
}
