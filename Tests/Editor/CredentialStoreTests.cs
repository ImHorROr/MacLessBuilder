using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HorrorTools.MacLessBuilder.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor)]
    public class CredentialStoreTests
    {
        private const string Target = "IosCloudBuild/UnitTest";

        [TearDown]
        public void TearDown()
        {
            CredentialStore.Delete(Target);
        }

        [Test]
        public void WriteThenRead_RoundTripsUnicode()
        {
            CredentialStore.Write(Target, "tester", "s3cret-é-ع");

            Assert.AreEqual("s3cret-é-ع", CredentialStore.Read(Target));
        }

        [Test]
        public void Delete_RemovesSecret()
        {
            CredentialStore.Write(Target, "tester", "value");
            CredentialStore.Delete(Target);

            Assert.IsNull(CredentialStore.Read(Target));
        }

        [Test]
        public void Read_MissingTarget_ReturnsNull()
        {
            Assert.IsNull(CredentialStore.Read(Target + "/Missing"));
        }
    }
}
