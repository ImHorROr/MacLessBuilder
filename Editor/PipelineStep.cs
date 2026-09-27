namespace HorrorTools.MacLessBuilder
{
    internal enum PipelineStep
    {
        Idle,
        Export,
        Zip,
        Upload,
        Dispatch,
        Queued,
        Building,
        Download,
        Ready,
        Failed,
    }
}
