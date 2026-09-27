using NUnit.Framework;

namespace HorrorTools.MacLessBuilder.Tests
{
    // The workflow template and the C# pipeline must agree on names, or builds fail only on GitHub.
    public class WorkflowInstallerTests
    {
        private string _template;

        [SetUp]
        public void SetUp()
        {
            _template = WorkflowInstaller.LoadTemplate();
        }

        [Test]
        public void Template_StartsWithVersionHeader()
        {
            StringAssert.StartsWith("# macless-builder v", _template);
        }

        [Test]
        public void Template_MatchesPipelineNames()
        {
            StringAssert.Contains(PipelineRunner.ReleaseTag, _template);
            StringAssert.Contains($"name: {PipelineRunner.ArtifactName}", _template);
        }

        [Test]
        public void Template_DeclaresEveryDispatchInput()
        {
            foreach (string input in new[] { "asset_name:", "request_id:", "use_pods:" })
            {
                StringAssert.Contains(input, _template);
            }

            // The run name carries the request id so the tool can find the run it dispatched.
            StringAssert.Contains("run-name: ios-build ${{ inputs.request_id }}", _template);
        }

        [Test]
        public void Template_HasUnixLineEndings()
        {
            StringAssert.DoesNotContain("\r", _template);
        }
    }
}
