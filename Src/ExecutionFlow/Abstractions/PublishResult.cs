namespace ExecutionFlow.Abstractions
{
    public class PublishResult
    {
        public string JobId { get; }
        public bool Enqueued { get; }

        public PublishResult(string jobId, bool enqueued)
        {
            JobId = jobId;
            Enqueued = enqueued;
        }
    }
}
