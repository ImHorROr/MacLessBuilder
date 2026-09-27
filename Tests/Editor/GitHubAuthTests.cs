using NUnit.Framework;

namespace HorrorTools.MacLessBuilder.Tests
{
    public class GitHubAuthTests
    {
        [Test]
        public void MissingScopes_AllGranted_ReturnsNone()
        {
            CollectionAssert.IsEmpty(GitHubAuth.MissingScopes("gist, repo, workflow"));
        }

        [Test]
        public void MissingScopes_ReportsEachMissingScope()
        {
            CollectionAssert.AreEqual(new[] { "workflow" }, GitHubAuth.MissingScopes("repo, gist"));
            CollectionAssert.AreEqual(new[] { "repo", "workflow" }, GitHubAuth.MissingScopes(string.Empty));
        }

        [Test]
        public void MissingScopes_NoHeader_TrustsFineGrainedToken()
        {
            CollectionAssert.IsEmpty(GitHubAuth.MissingScopes(null));
        }

        [Test]
        public void ParseGitCredential_ReadsPasswordLine()
        {
            const string output = "protocol=https\r\nhost=github.com\r\nusername=someone\r\npassword=gho_abc=123\r\n";

            Assert.AreEqual("gho_abc=123", GitHubAuth.ParseGitCredential(output));
        }

        [Test]
        public void ParseGitCredential_NoOrEmptyPassword_ReturnsNull()
        {
            Assert.IsNull(GitHubAuth.ParseGitCredential("protocol=https\nhost=github.com\n"));
            Assert.IsNull(GitHubAuth.ParseGitCredential("password=\n"));
        }
    }
}
