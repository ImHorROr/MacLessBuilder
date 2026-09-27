using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HorrorTools.MacLessBuilder
{
    // Thin wrapper over the GitHub REST API, authenticated with the token GitHubAuth saved.
    internal static class GitHubClient
    {
        public const string TokenTarget = "IosCloudBuild/GitHub";
        private const string ApiUrl = "https://api.github.com/";
        private const int ProgressIntervalMs = 100;

        public static bool HasToken => !string.IsNullOrEmpty(CredentialStore.Read(TokenTarget));

        public static async Task DownloadAsync(string url, string filePath, Action<float> progress, CancellationToken token)
        {
            // Artifact URLs redirect to signed storage that rejects the GitHub Authorization header,
            // so the redirect is followed by hand without it.
            string location;
            using (UnityWebRequest request = CreateRequest(UnityWebRequest.kHttpVerbGET, url))
            {
                request.redirectLimit = 0;
                await ExecuteAsync(request, null, token);
                location = request.GetResponseHeader("Location");
                if (string.IsNullOrEmpty(location))
                {
                    ThrowIfFailed(request);
                    throw new InvalidOperationException("GitHub returned no download location for the artifact.");
                }
            }

            var fileHandler = new DownloadHandlerFile(filePath) { removeFileOnAbort = true };
            using (var request = new UnityWebRequest(location, UnityWebRequest.kHttpVerbGET, fileHandler, null))
            {
                await ExecuteAsync(request, progress, token);
                ThrowIfFailed(request);
            }
        }

        public static async Task ExecuteAsync(
            UnityWebRequest request,
            Action<float> progress = null,
            CancellationToken token = default)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    request.Abort();
                    token.ThrowIfCancellationRequested();
                }

                progress?.Invoke(request.uploadHandler != null ? request.uploadProgress : request.downloadProgress);
                await Task.Delay(ProgressIntervalMs);
            }
        }

        public static async Task<T[]> GetArrayAsync<T>(string path)
        {
            // JsonUtility cannot parse a top-level array, so it is wrapped in an object first.
            string json = await SendAsync(UnityWebRequest.kHttpVerbGET, path);
            return JsonUtility.FromJson<ArrayWrapper<T>>($"{{\"items\":{json}}}").items;
        }

        public static async Task<T> GetAsync<T>(string path, CancellationToken token = default)
        {
            return JsonUtility.FromJson<T>(await SendAsync(UnityWebRequest.kHttpVerbGET, path, null, token));
        }

        public static async Task<string> SendAsync(
            string method,
            string path,
            string json = null,
            CancellationToken token = default)
        {
            using (UnityWebRequest request = CreateRequest(method, path))
            {
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                await ExecuteAsync(request, null, token);
                ThrowIfFailed(request);
                return request.downloadHandler.text;
            }
        }

        public static async Task<T> SendAsync<T>(string method, string path, string json)
        {
            return JsonUtility.FromJson<T>(await SendAsync(method, path, json));
        }

        public static void ThrowIfFailed(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            string message = request.responseCode == 403 && request.GetResponseHeader("x-ratelimit-remaining") == "0"
                ? "GitHub rate limit reached. Try again in a few minutes."
                : ReadMessage(request) ?? request.error;
            // AbsolutePath only: signed storage URLs carry credentials in their query string.
            throw new RequestException(
                request.responseCode,
                $"{request.method} {new Uri(request.url).AbsolutePath} failed ({request.responseCode}): {message}");
        }

        public static async Task UploadAsync(string url, string filePath, Action<float> progress, CancellationToken token)
        {
            using (UnityWebRequest request = CreateRequest(UnityWebRequest.kHttpVerbPOST, url))
            {
                // Streams from disk, so a multi-GB export is never held in memory.
                request.uploadHandler = new UploadHandlerFile(filePath);
                request.SetRequestHeader("Content-Type", "application/zip");
                await ExecuteAsync(request, progress, token);
                ThrowIfFailed(request);
            }
        }

        private static UnityWebRequest CreateRequest(string method, string pathOrUrl)
        {
            string token = CredentialStore.Read(TokenTarget);
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Sign in to GitHub first (Setup tab).");
            }

            string url = pathOrUrl.StartsWith("https://") ? pathOrUrl : ApiUrl + pathOrUrl;
            var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            return request;
        }

        private static string ReadMessage(UnityWebRequest request)
        {
            if (request.downloadHandler is not DownloadHandlerBuffer buffer || string.IsNullOrEmpty(buffer.text))
            {
                return null;
            }

            try
            {
                var error = JsonUtility.FromJson<ErrorBody>(buffer.text);
                if (string.IsNullOrEmpty(error.message))
                {
                    return null;
                }

                return error.errors is { Length: > 0 } && !string.IsNullOrEmpty(error.errors[0].message)
                    ? $"{error.message} {error.errors[0].message}"
                    : error.message;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        internal class RequestException : Exception
        {
            public long StatusCode { get; }

            public RequestException(long statusCode, string message) : base(message)
            {
                StatusCode = statusCode;
            }
        }

        // DTO field names mirror GitHub's JSON because JsonUtility maps fields by name.
#pragma warning disable IDE1006, CS0649
        [Serializable]
        internal class ArrayWrapper<T>
        {
            public T[] items;
        }

        [Serializable]
        internal class Artifact
        {
            public long id;
            public string name;
            public string archive_download_url;
        }

        [Serializable]
        internal class ArtifactList
        {
            public Artifact[] artifacts;
        }

        [Serializable]
        internal class CreateReleaseBody
        {
            public string tag_name;
            public string name;
            public string body;
            public bool prerelease;
        }

        [Serializable]
        internal class CreateRepoBody
        {
            public string name;
            public string description;
            public bool @private;
            public bool auto_init;
        }

        [Serializable]
        internal class DispatchBody
        {
            public string @ref;
            public DispatchInputs inputs;
        }

        [Serializable]
        internal class DispatchInputs
        {
            public string asset_name;
            public string request_id;
            public string use_pods;
        }

        [Serializable]
        internal class ErrorBody
        {
            public string message;
            public ErrorDetail[] errors;
        }

        // Separate from ErrorBody: a self-referencing field trips Unity's serialization depth limit.
        [Serializable]
        internal class ErrorDetail
        {
            public string message;
        }

        [Serializable]
        internal class FileBody
        {
            public string message;
            public string content;
        }

        [Serializable]
        internal class FileContent
        {
            public string sha;
            public string content;
        }

        // Separate type so creating a file sends no "sha" field at all.
        [Serializable]
        internal class FileUpdateBody : FileBody
        {
            public string sha;
        }

        [Serializable]
        internal class Release
        {
            public long id;
            public ReleaseAsset[] assets;
        }

        [Serializable]
        internal class ReleaseAsset
        {
            public long id;
            public string name;
        }

        [Serializable]
        internal class Repo
        {
            public string name;
            public string full_name;
            public string default_branch;
            public string html_url;
            public bool @private;
            public RepoPermissions permissions;
        }

        [Serializable]
        internal class RepoPermissions
        {
            public bool push;
        }

        [Serializable]
        internal class RunList
        {
            public WorkflowRun[] workflow_runs;
        }

        [Serializable]
        internal class UsageItem
        {
            public string product;
            public string sku;
            public string unitType;
            public float grossQuantity;
        }

        [Serializable]
        internal class UsageSummary
        {
            public UsageItem[] usageItems;
        }

        [Serializable]
        internal class User
        {
            public string login;
            public UserPlan plan;
        }

        [Serializable]
        internal class UserPlan
        {
            public string name;
        }

        [Serializable]
        internal class WorkflowRun
        {
            public long id;
            public string display_title;
            public string status;
            public string conclusion;
            public string html_url;
            public string run_started_at;
            public string updated_at;
        }
#pragma warning restore IDE1006, CS0649
    }
}
