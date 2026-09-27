using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;

namespace HorrorTools.MacLessBuilder.Tests
{
    public class PipelineRunnerTests
    {
        [Test]
        public void CreateZip_UsesForwardSlashesAndKeepsEmptyFolders()
        {
            string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "xcode");
            Directory.CreateDirectory(Path.Combine(source, "Classes"));
            Directory.CreateDirectory(Path.Combine(source, "Data", "Raw"));
            File.WriteAllText(Path.Combine(source, "Classes", "Main.mm"), "int main() {}");
            string zipPath = Path.Combine(root, "xcode.zip");
            try
            {
                PipelineRunner.CreateZip(source, zipPath);

                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    CollectionAssert.AreEquivalent(
                        new[] { "Classes/Main.mm", "Data/Raw/" },
                        archive.Entries.Select(e => e.FullName));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestCase("/[Bb]uilds/", ExpectedResult = true)]
        [TestCase("Builds/", ExpectedResult = true)]
        [TestCase("  /Builds  ", ExpectedResult = true)]
        [TestCase("# Builds", ExpectedResult = false)]
        [TestCase("/Builds/iOS/", ExpectedResult = false)]
        [TestCase("*.apk", ExpectedResult = false)]
        public bool IsBuildsIgnored_MatchesOnlyWholeFolderRules(string line)
        {
            return PipelineRunner.IsBuildsIgnored(new[] { "/[Ll]ibrary/", line });
        }

        [Test]
        public void RunMinutes_RoundsUpToWholeMinutes()
        {
            var run = new GitHubClient.WorkflowRun { run_started_at = "2026-09-27T05:00:00Z", updated_at = "2026-09-27T05:10:01Z" };

            Assert.AreEqual(11, PipelineRunner.RunMinutes(run));
        }

        [Test]
        public void RunMinutes_MissingTimestamps_ReturnsZero()
        {
            Assert.AreEqual(0, PipelineRunner.RunMinutes(new GitHubClient.WorkflowRun()));
        }
    }
}
