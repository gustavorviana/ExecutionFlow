using Hangfire.Console.Progress;

namespace ExecutionFlow.Hangfire.Console
{
    /// <summary>
    /// Wraps a Hangfire Console progress bar, providing simplified methods to update progress during job execution.
    /// </summary>
    public class ExecutionProgressBar
    {
        private readonly IProgressBar _progressBar;

        internal ExecutionProgressBar(IProgressBar progressBar)
        {
            _progressBar = progressBar;
        }

        /// <summary>
        /// Sets the progress bar to the specified percentage value.
        /// </summary>
        /// <param name="percentage">The progress percentage (0-100).</param>
        public void SetValue(float percentage)
        {
            _progressBar.SetValue((double)percentage);
        }

        /// <summary>
        /// Sets the progress bar based on the current item index relative to the total count.
        /// </summary>
        /// <param name="currentItem">The current item number (1-based).</param>
        /// <param name="total">The total number of items.</param>
        public void SetValue(int currentItem, int total)
        {
            if (total <= 0)
                return;

            var percentage = (double)currentItem / total * 100;
            _progressBar.SetValue(percentage);
        }

        /// <summary>
        /// Sets the progress bar to 100%, indicating completion.
        /// </summary>
        public void Complete()
        {
            _progressBar.SetValue(100);
        }
    }
}
