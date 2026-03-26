namespace ExecutionFlow.Hangfire
{
    public interface IHangfireOption<out T> where T : class
    {
        T Value { get; }
    }

    internal class HangfireOption<T> : IHangfireOption<T> where T : class
    {
        public HangfireOption(T value)
        {
            Value = value ?? throw new System.ArgumentNullException(nameof(value));
        }

        public T Value { get; }
    }
}
