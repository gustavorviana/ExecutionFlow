using Hangfire.Console.Progress;

namespace ExecutionFlow.Hangfire.Console
{
    public class ExecutionProgressBar
    {
        private readonly IProgressBar _progressBar;

        internal ExecutionProgressBar(IProgressBar progressBar)
        {
            _progressBar = progressBar;
        }

        public void SetValue(float percentage)
        {
            _progressBar.SetValue((double)percentage);
        }

        public void SetValue(int currentItem, int total)
        {
            if (total <= 0)
                return;

            var percentage = (double)currentItem / total * 100;
            _progressBar.SetValue(percentage);
        }

        public void Complete()
        {
            _progressBar.SetValue(100);
        }
    }
}
