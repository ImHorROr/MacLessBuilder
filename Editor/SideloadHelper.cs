using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace HorrorTools.MacLessBuilder
{
    // Runs Editor/Bin/sideload-helper.exe (built from Helper~ on isideload) and relays its JSON-line events on the main thread.
    // The Apple ID password goes to the helper's stdin, never its arguments, which other processes can read.
    internal sealed class SideloadHelper : IDisposable
    {
        public const string AppleIdTarget = "IosCloudBuild/AppleID";
        private const int PollIntervalMs = 100;
        private readonly ConcurrentQueue<HelperEvent> _events = new ConcurrentQueue<HelperEvent>();
        private readonly Process _process;
        private readonly StringBuilder _stderr = new StringBuilder();

        public delegate void HelperEventHandler(HelperEvent helperEvent);

        public static string ExePath => PackagePaths.HelperExe;

        private SideloadHelper(Process process)
        {
            _process = process;
        }

        public static string Explain(string message)
        {
            string text = message ?? string.Empty;
            bool Has(string value) => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

            if (IsCertificateLimit(text))
            {
                return "Your Apple ID already has the maximum number of development certificates.";
            }

            if (Has("-20101") || Has("incorrect") || Has("invalid credentials") || Has("-22406"))
            {
                return "Apple rejected the Apple ID email or password. Enter them again on the Setup tab.";
            }

            if (Has("No 2FA"))
            {
                return "Sign-in was cancelled before the two-factor code was entered.";
            }

            if (Has("anisette") || Has("provisioning socket") || Has("WS connect failed"))
            {
                return "Apple's sign-in data server (anisette) did not respond. Try again in a few minutes, or set another anisette server under Settings.";
            }

            if (Has("MaxAppsExceeded") || Has("maximum number of apps"))
            {
                return "A free Apple ID allows about 3 sideloaded apps per device. Delete an older sideloaded app from the phone and try again.";
            }

            if (Has("App ID") && (Has("limit") || Has("maximum")))
            {
                return "This Apple ID created too many new App IDs this week. Keep the same bundle identifier between builds, or wait for the weekly reset.";
            }

            if (Has("DeveloperMode") || Has("developer mode"))
            {
                return "Developer Mode is off on the iPhone. See Help > Turn on Developer Mode.";
            }

            if (Has("PasswordProtected") || Has("device is locked"))
            {
                return "Unlock the iPhone and try again.";
            }

            return text;
        }

        public static bool IsCertificateLimit(string message)
        {
            return message != null && (message.Contains("7460") || message.Contains("Maximum number of certificates"));
        }

        public static SideloadHelper Start(string stdinFirstLine, params string[] arguments)
        {
            if (!File.Exists(ExePath))
            {
                throw new FileNotFoundException("sideload-helper.exe is missing from Editor/Bin. Build it from Helper~ (see the README).", ExePath);
            }

            var startInfo = new ProcessStartInfo(ExePath, string.Join(" ", arguments.Select(Quote)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Mono's default stdin encoding writes a UTF-8 BOM, which would corrupt the password line.
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            var helper = new SideloadHelper(new Process { StartInfo = startInfo });
            helper._process.OutputDataReceived += (_, e) => helper.OnOutput(e.Data);
            helper._process.ErrorDataReceived += (_, e) => helper.OnError(e.Data);
            helper._process.Start();
            helper._process.BeginOutputReadLine();
            helper._process.BeginErrorReadLine();
            if (stdinFirstLine != null)
            {
                helper.Send(stdinFirstLine);
            }

            return helper;
        }

        internal static string Quote(string argument)
        {
            // ponytail: enough for emails, UDIDs and paths; a path ending in a backslash would need doubling.
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            _process.Dispose();
        }

        public async Task<HelperEvent> RunAsync(HelperEventHandler onEvent, CancellationToken token)
        {
            HelperEvent last = null;
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    Dispose();
                    token.ThrowIfCancellationRequested();
                }

                // Read before draining, so lines written just before exit are not missed.
                bool exited = _process.HasExited;
                if (exited)
                {
                    // The no-timeout overload also waits for the redirected streams to reach end of file.
                    _process.WaitForExit();
                }

                while (_events.TryDequeue(out HelperEvent helperEvent))
                {
                    last = helperEvent;
                    onEvent?.Invoke(helperEvent);
                }

                if (exited)
                {
                    break;
                }

                await Task.Delay(PollIntervalMs);
            }

            if (_process.ExitCode == 0 || last?.@event == HelperEvent.Error)
            {
                return last ?? new HelperEvent { @event = HelperEvent.Done };
            }

            string stderr;
            lock (_stderr)
            {
                stderr = _stderr.ToString().Trim();
            }

            return new HelperEvent
            {
                @event = HelperEvent.Error,
                message = stderr.Length > 0 ? stderr : $"The sideload helper stopped with exit code {_process.ExitCode}.",
            };
        }

        public void Send(string line)
        {
            _process.StandardInput.WriteLine(line);
            _process.StandardInput.Flush();
        }

        private void OnError(string line)
        {
            if (line == null)
            {
                return;
            }

            lock (_stderr)
            {
                _stderr.AppendLine(line);
            }
        }

        private void OnOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            try
            {
                // JsonUtility is safe to call off the main thread.
                _events.Enqueue(JsonUtility.FromJson<HelperEvent>(line));
            }
            catch (ArgumentException)
            {
                OnError(line);
            }
        }

        // One line of the helper's protocol. Field names mirror its JSON because JsonUtility maps by name.
#pragma warning disable IDE1006, CS0649
        [Serializable]
        internal class HelperEvent
        {
            public const string Device = "device";
            public const string Done = "done";
            public const string Error = "error";
            public const string LoggedIn = "logged_in";
            public const string NeedTwoFactor = "need_2fa";
            public const string Progress = "progress";

            public string @event;
            public string message;
            public string stage;
            public int percent = -1;
            public string udid;
            public string name;
            public string ios;
            public string devMode;
            public string error;
            public string device;
            public string version;
            public bool sms;
            public bool retry;
            public string lastError;
        }
#pragma warning restore IDE1006, CS0649
    }
}
