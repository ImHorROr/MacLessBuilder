using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using HelperEvent = HorrorTools.MacLessBuilder.SideloadHelper.HelperEvent;

namespace HorrorTools.MacLessBuilder.Tests
{
    public class SideloadHelperTests
    {
        [TestCase("Developer error 7460: Maximum number of certificates reached", ExpectedResult = true)]
        [TestCase("Failed to retrieve certificate identity: Maximum number of certificates reached", ExpectedResult = true)]
        [TestCase("Failed to sign the app", ExpectedResult = false)]
        [TestCase(null, ExpectedResult = false)]
        public bool IsCertificateLimit_MatchesIsideloadError(string message)
        {
            return SideloadHelper.IsCertificateLimit(message);
        }

        [TestCase("Apple ID sign-in failed: -20101 Incorrect", "rejected the Apple ID")]
        [TestCase("Apple ID sign-in failed: Anisette provisioning timed out", "anisette")]
        [TestCase("No 2FA code provided, aborting", "cancelled")]
        [TestCase("Failed to install the app on the device: MaxAppsExceeded", "3 sideloaded apps")]
        [TestCase("Developer error 9401: You have exceeded the maximum App ID limit", "App IDs")]
        public void Explain_TurnsKnownErrorsIntoAdvice(string message, string expectedFragment)
        {
            StringAssert.Contains(expectedFragment, SideloadHelper.Explain(message));
        }

        [Test]
        public void Explain_UnknownError_KeepsOriginalText()
        {
            Assert.AreEqual("Something new broke", SideloadHelper.Explain("Something new broke"));
        }

        [Test]
        public void HelperEvent_ParsesProtocolLines()
        {
            var device = JsonUtility.FromJson<HelperEvent>(
                "{\"event\":\"device\",\"udid\":\"00008110-X\",\"name\":\"Yazan's iPhone\",\"ios\":\"18.6\",\"devMode\":\"off\"}");
            var progress = JsonUtility.FromJson<HelperEvent>("{\"event\":\"progress\",\"stage\":\"login\",\"percent\":-1}");
            var twoFactor = JsonUtility.FromJson<HelperEvent>(
                "{\"event\":\"need_2fa\",\"sms\":true,\"retry\":false,\"lastError\":null}");

            Assert.AreEqual(HelperEvent.Device, device.@event);
            Assert.AreEqual("Yazan's iPhone", device.name);
            Assert.AreEqual("off", device.devMode);
            Assert.AreEqual(-1, progress.percent);
            Assert.AreEqual(HelperEvent.NeedTwoFactor, twoFactor.@event);
            Assert.IsTrue(twoFactor.sms);
        }

        [Test]
        public void Quote_WrapsArgumentsWithSpacesAndQuotes()
        {
            Assert.AreEqual("\"C:\\My Builds\\game.ipa\"", SideloadHelper.Quote("C:\\My Builds\\game.ipa"));
            Assert.AreEqual("\"a\\\"b\"", SideloadHelper.Quote("a\"b"));
        }

        [Test]
        public async Task Helper_VersionCommand_RoundTripsThroughTheProtocol()
        {
            Assume.That(File.Exists(SideloadHelper.ExePath), "sideload-helper.exe has not been built.");

            using (SideloadHelper helper = SideloadHelper.Start(null, "version"))
            {
                HelperEvent result = await helper.RunAsync(null, CancellationToken.None);

                Assert.AreEqual("version", result.@event);
                Assert.IsNotEmpty(result.version);
            }
        }

        [Test]
        public async Task Helper_BadArguments_ReportsProtocolError()
        {
            Assume.That(File.Exists(SideloadHelper.ExePath), "sideload-helper.exe has not been built.");

            using (SideloadHelper helper = SideloadHelper.Start(null, "install", "--email", "someone@example.com"))
            {
                HelperEvent result = await helper.RunAsync(null, CancellationToken.None);

                Assert.AreEqual(HelperEvent.Error, result.@event);
                Assert.AreEqual("Missing --ipa.", result.message);
            }
        }
    }
}
