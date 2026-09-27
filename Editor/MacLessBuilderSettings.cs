using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorTools.MacLessBuilder
{
    // UserSettings/ stays out of version control, so teammates never share a repo choice or history.
    // Secrets are not stored here; see CredentialStore.
    // Storage keys (this file, Credential Manager targets, EditorPrefs) keep the tool's first name, IosCloudBuild,
    // so sign-ins and settings survive the rename to MacLess Builder.
    [FilePath("UserSettings/IosCloudBuild.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class MacLessBuilderSettings : ScriptableSingleton<MacLessBuilderSettings>
    {
        private const int MaxHistory = 10;

        [field: SerializeField] public string RepoOwner { get; private set; }
        [field: SerializeField] public string RepoName { get; private set; }
        [field: SerializeField] public string DefaultBranch { get; private set; }
        [field: SerializeField] public bool DevelopmentBuild { get; set; } = true;
        [field: SerializeField] public string IpaPath { get; set; }
        [field: SerializeField] public List<BuildRecord> History { get; private set; } = new List<BuildRecord>();
        // GitHub changes allowances and prices, so both stay editable. 0 means "from the GitHub plan".
        [field: SerializeField] public int IncludedMinutesOverride { get; set; }
        [field: SerializeField] public float MacPricePerMinute { get; set; } = 0.062f;
        // Empty uses isideload's default; public anisette servers go down, so this stays swappable.
        [field: SerializeField] public string AnisetteUrl { get; set; }
        [field: SerializeField] public string DeviceUdid { get; set; }
        [field: SerializeField] public string LastInstallDevice { get; set; }
        [field: SerializeField] public long LastInstallTicks { get; set; }

        public bool HasRepo => !string.IsNullOrEmpty(RepoOwner) && !string.IsNullOrEmpty(RepoName);

        public string RepoFullName => $"{RepoOwner}/{RepoName}";

        public void AddHistory(BuildRecord record)
        {
            History.Insert(0, record);
            if (History.Count > MaxHistory)
            {
                History.RemoveRange(MaxHistory, History.Count - MaxHistory);
            }

            SaveChanges();
        }

        public void SaveChanges()
        {
            Save(true);
        }

        public void SelectRepo(GitHubClient.Repo repo)
        {
            string[] parts = repo.full_name.Split('/');
            RepoOwner = parts[0];
            RepoName = parts[1];
            DefaultBranch = repo.default_branch;
            SaveChanges();
        }

        [Serializable]
        internal class BuildRecord
        {
            public const string DateFormat = "yyyy-MM-dd HH:mm";
            public const string Succeeded = "Succeeded";

            [field: SerializeField] public string Date { get; set; }
            [field: SerializeField] public int Minutes { get; set; }
            // Billed macOS runner minutes, as opposed to Minutes, the whole pipeline including export and upload.
            [field: SerializeField] public int RunMinutes { get; set; }
            [field: SerializeField] public string Result { get; set; }
            [field: SerializeField] public string RunUrl { get; set; }
            [field: SerializeField] public string IpaPath { get; set; }
        }
    }
}
