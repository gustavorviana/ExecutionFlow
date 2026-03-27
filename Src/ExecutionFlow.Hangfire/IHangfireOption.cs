namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Wraps a custom option value that can be injected into handlers.
    /// </summary>
    /// <typeparam name="T">The option type.</typeparam>
    public interface IHangfireOption<out T> where T : class
    {
        /// <summary>Gets the option value.</summary>
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
