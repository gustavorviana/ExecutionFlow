using ExecutionFlow.Abstractions;
using Hangfire.Common;
using Hangfire.Storage;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Single place that reads and writes the ExecutionFlow job parameters.
    /// Values are written JSON-encoded, the same encoding <c>IBackgroundJobClientV2.Create</c> uses,
    /// and read in both JSON and the raw format written by versions before 1.2.0.
    /// </summary>
    internal static class JobParameters
    {
        /// <summary>
        /// Gets the custom ID of an event, or <c>false</c> when the event has no custom ID.
        /// A null or empty <see cref="ICustomIdEvent.CustomId"/> counts as no custom ID.
        /// </summary>
        public static bool TryGetCustomId(object @event, out string customId)
        {
            customId = (@event as ICustomIdEvent)?.CustomId;
            return !string.IsNullOrEmpty(customId);
        }

        public static string EncodeCustomId(string customId)
        {
            return SerializationHelper.Serialize(customId);
        }

        public static void WriteCustomId(IStorageConnection connection, string jobId, string customId)
        {
            connection.SetJobParameter(jobId, ContextConsts.CustomId, EncodeCustomId(customId));
        }

        public static string ReadCustomId(IStorageConnection connection, string jobId)
        {
            return Decode(connection.GetJobParameter(jobId, ContextConsts.CustomId));
        }

        internal static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '"')
                return value;

            try
            {
                return SerializationHelper.Deserialize<string>(value);
            }
            catch
            {
                return value;
            }
        }
    }
}
