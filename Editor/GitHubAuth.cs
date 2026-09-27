using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace HorrorTools.MacLessBuilder
{
    // Two ways in: GitHub's OAuth device flow (the user types a short code on github.com, no secret ships
    // with the tool), or the GitHub login the user's own Git Credential Manager already holds.
    internal static class GitHubAuth
    {
        public const string NewOAuthAppUrl = "https://github.com/settings/applications/new";
        public const string Scopes = "repo workflow";
        private const string ClientIdPrefKey = "IosCloudBuild.GitHubClientId";
        // Fill this in before sharing the package so users skip pasting a client id; the Setup tab can still override it.
        private const string DefaultClientId = "";
        private const string DeviceCodeUrl = "https://github.com/login/device/code";
        private const string PasswordPrefix = "password=";
        private const string TokenUrl = "https://github.com/login/oauth/access_token";
        private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(3);

        // Client id of a GitHub OAuth App with "Enable Device Flow" ticked. Not a secret, so EditorPrefs is fine.
        public static string ClientId
        {
            get => EditorPrefs.GetString(ClientIdPrefKey, DefaultClientId);
            set => EditorPrefs.SetString(ClientIdPrefKey, value?.Trim() ?? string.Empty);
        }

        public static async Task SignInAsync(Action<string, string> showUserCode, CancellationToken token)
        {
            if (string.IsNullOrEmpty(ClientId))
            {
                throw new InvalidOperationException(
                    "Paste the client id of your GitHub OAuth App (with Device Flow enabled) into the Setup tab first.");
            }

            DeviceCode code = await PostFormAsync<DeviceCode>(DeviceCodeUrl, new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = Scopes,
            });
            showUserCode(code.user_code, code.verification_uri);

            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = code.device_code,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            };
            int interval = code.interval;
            DateTime expiry = DateTime.UtcNow.AddSeconds(code.expires_in);
            while (DateTime.UtcNow < expiry)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
                TokenResponse response = await PostFormAsync<TokenResponse>(TokenUrl, form);
                if (!string.IsNullOrEmpty(response.access_token))
                {
                    CredentialStore.Write(GitHubClient.TokenTarget, "GitHub", response.access_token);
                    return;
                }

                switch (response.error)
                {
                    case "authorization_pending":
                        break;
                    case "slow_down":
                        interval = response.interval > interval ? response.interval : interval + 5;
                        break;
                    default:
                        throw new InvalidOperationException(
                            string.IsNullOrEmpty(response.error_description) ? response.error : response.error_description);
                }
            }

            throw new TimeoutException("The GitHub sign-in code expired. Try again.");
        }

        public static void SignOut()
        {
            CredentialStore.Delete(GitHubClient.TokenTarget);
        }

        public static async Task SignInWithGitAsync()
        {
            string token = await Task.Run(ReadGitCredential);
            await VerifyScopesAsync(token);
            CredentialStore.Write(GitHubClient.TokenTarget, "GitHub", token);
        }

        internal static string[] MissingScopes(string grantedScopesHeader)
        {
            // Fine-grained tokens send no scope header; missing permissions then surface on first use.
            if (grantedScopesHeader == null)
            {
                return Array.Empty<string>();
            }

            string[] granted = grantedScopesHeader.Split(',').Select(s => s.Trim()).ToArray();
            return Scopes.Split(' ').Where(s => !granted.Contains(s)).ToArray();
        }

        internal static string ParseGitCredential(string output)
        {
            string password = output.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .FirstOrDefault(line => line.StartsWith(PasswordPrefix));
            return string.IsNullOrEmpty(password) || password.Length == PasswordPrefix.Length
                ? null
                : password.Substring(PasswordPrefix.Length);
        }

        private static async Task<T> PostFormAsync<T>(string url, Dictionary<string, string> form)
        {
            using (UnityWebRequest request = UnityWebRequest.Post(url, form))
            {
                request.SetRequestHeader("Accept", "application/json");
                await GitHubClient.ExecuteAsync(request);
                GitHubClient.ThrowIfFailed(request);
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
        }

        // "git credential fill" is Git's public API for credential helpers: Git Credential Manager returns its
        // stored GitHub login, or opens its own browser sign-in when it has none.
        private static string ReadGitCredential()
        {
            var startInfo = new ProcessStartInfo("git", "credential fill")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                // Mono's default stdin encoding writes a UTF-8 BOM, which makes Git miss the "protocol=" line.
                StandardInputEncoding = new UTF8Encoding(false),
            };
            // Without a credential helper Git would otherwise wait on a terminal prompt nobody can answer.
            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            Process git;
            try
            {
                git = Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                throw new InvalidOperationException("Git is not installed or not on PATH. Use Sign in with GitHub instead.");
            }

            using (git)
            {
                git.StandardInput.Write("protocol=https\nhost=github.com\n\n");
                git.StandardInput.Close();
                Task<string> output = git.StandardOutput.ReadToEndAsync();
                Task<string> error = git.StandardError.ReadToEndAsync();
                if (!git.WaitForExit((int)GitTimeout.TotalMilliseconds))
                {
                    git.Kill();
                    throw new TimeoutException("Git Credential Manager did not return a GitHub login in time.");
                }

                if (git.ExitCode != 0)
                {
                    throw new InvalidOperationException("Git has no GitHub login: " + error.Result.Trim());
                }

                return ParseGitCredential(output.Result)
                    ?? throw new InvalidOperationException("Git returned no GitHub login. Use Sign in with GitHub instead.");
            }
        }

        private static async Task VerifyScopesAsync(string token)
        {
            using (UnityWebRequest request = UnityWebRequest.Get("https://api.github.com/user"))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                await GitHubClient.ExecuteAsync(request);
                GitHubClient.ThrowIfFailed(request);

                string[] missing = MissingScopes(request.GetResponseHeader("X-OAuth-Scopes"));
                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Git's GitHub login lacks the {string.Join(", ", missing)} scope. Use Sign in with GitHub instead.");
                }
            }
        }

#pragma warning disable IDE1006, CS0649
        [Serializable]
        private class DeviceCode
        {
            public string device_code;
            public string user_code;
            public string verification_uri;
            public int expires_in;
            public int interval;
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string error;
            public string error_description;
            public int interval;
        }
#pragma warning restore IDE1006, CS0649
    }
}
