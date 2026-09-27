using System;
using UnityEditor;
using UnityEngine;

namespace HorrorTools.MacLessBuilder
{
    // Saved to Library/ after every change: switching target and exporting both reload the domain,
    // and PipelineRunner resumes from here instead of from an async method that died with the old domain.
    [FilePath("Library/IosCloudBuild/PipelineState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class PipelineState : ScriptableSingleton<PipelineState>
    {
        [field: SerializeField] public PipelineStep Step { get; private set; }
        [field: SerializeField] public PipelineStep FailedStep { get; private set; }
        [field: SerializeField] public string Error { get; private set; }
        [field: SerializeField] public string RequestId { get; private set; }
        [field: SerializeField] public long StartedTicks { get; private set; }
        [field: SerializeField] public long StepStartedTicks { get; private set; }
        [field: SerializeField] public string ZipPath { get; set; }
        [field: SerializeField] public bool UsesPods { get; set; }
        [field: SerializeField] public long RunId { get; set; }
        [field: SerializeField] public string RunUrl { get; set; }
        [field: SerializeField] public int RunMinutes { get; set; }

        public bool IsActive => Step is not (PipelineStep.Idle or PipelineStep.Ready or PipelineStep.Failed);

        public float Progress { get; set; }

        public void Begin(string requestId)
        {
            RequestId = requestId;
            StartedTicks = DateTime.UtcNow.Ticks;
            Error = null;
            ZipPath = null;
            UsesPods = false;
            RunId = 0;
            RunUrl = null;
            RunMinutes = 0;
            SetStep(PipelineStep.Export);
        }

        public void Fail(string error)
        {
            FailedStep = Step;
            Error = error;
            SetStep(PipelineStep.Failed);
        }

        public void SaveChanges()
        {
            Save(true);
        }

        public void SetStep(PipelineStep step)
        {
            if (step != Step)
            {
                Step = step;
                StepStartedTicks = DateTime.UtcNow.Ticks;
                Progress = 0;
            }

            SaveChanges();
        }
    }
}
