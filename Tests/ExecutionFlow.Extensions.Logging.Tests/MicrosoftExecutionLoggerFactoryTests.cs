using ExecutionFlow.Abstractions;
using Microsoft.Extensions.Logging;

namespace ExecutionFlow.Extensions.Logging.Tests
{
    public class MicrosoftExecutionLoggerFactoryTests
    {
        private sealed class TestOptions : ExecutionFlowOptions { }
        private sealed class OrderPaid { }
        private sealed class OrderPaidHandler { }

        private sealed record Entry(string Category, LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State, object? Scope);

        private sealed class CapturingLoggerFactory : ILoggerFactory
        {
            public List<Entry> Entries { get; } = new();
            public LogLevel Minimum { get; set; } = LogLevel.Trace;
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);
            public void Dispose() { }
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerFactory _factory;
            private readonly string _category;
            private object? _scope;

            public CapturingLogger(CapturingLoggerFactory factory, string category) { _factory = factory; _category = category; }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                _scope = state;
                return new Scope(() => _scope = null);
            }

            public bool IsEnabled(LogLevel logLevel) => logLevel >= _factory.Minimum;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
                _factory.Entries.Add(new Entry(_category, logLevel, formatter(state, exception), values, _scope));
            }

            private sealed class Scope : IDisposable
            {
                private readonly Action _onDispose;
                public Scope(Action onDispose) => _onDispose = onDispose;
                public void Dispose() => _onDispose();
            }
        }

        private static ExecutionLoggerContext Context(Type? handler = null, Type? evt = null) =>
            new ExecutionLoggerContext(new FlowParameters(), "42", 3, handler!, evt!);

        [Fact]
        public void CreateLogger_UsesHandlerTypeAsCategory_WhenHandlerIsKnown()
        {
            var loggers = new CapturingLoggerFactory();
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(Context(typeof(OrderPaidHandler), typeof(OrderPaid)))
                .Log(HandlerLogType.Information, "hello");

            Assert.Equal(typeof(OrderPaidHandler).FullName, Assert.Single(loggers.Entries).Category);
        }

        [Fact]
        public void CreateLogger_UsesEventTypeAsCategory_WhenHandlerIsUnknown()
        {
            var loggers = new CapturingLoggerFactory();
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(Context(evt: typeof(OrderPaid)))
                .Log(HandlerLogType.Information, "hello");

            Assert.Equal(typeof(OrderPaid).FullName, Assert.Single(loggers.Entries).Category);
        }

        [Fact]
        public void CreateLogger_UsesDefaultCategory_WhenNoTypeIsKnown()
        {
            var loggers = new CapturingLoggerFactory();
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(new FlowParameters())
                .Log(HandlerLogType.Information, "hello");

            Assert.Equal(MicrosoftExecutionLoggerFactory.DefaultCategory, Assert.Single(loggers.Entries).Category);
        }

        [Fact]
        public void Log_KeepsTemplatePropertiesAndFormatsMessage_WhenArgsAreGiven()
        {
            var loggers = new CapturingLoggerFactory();
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(Context(typeof(OrderPaidHandler)))
                .Log(HandlerLogType.Information, "Order {OrderId} paid by {Customer}", 7, "Ana");

            var entry = Assert.Single(loggers.Entries);
            Assert.Equal("Order 7 paid by Ana", entry.Message);
            Assert.Contains(entry.State, kv => kv.Key == "OrderId" && Equals(kv.Value, 7));
            Assert.Contains(entry.State, kv => kv.Key == "Customer" && Equals(kv.Value, "Ana"));
            Assert.Contains(entry.State, kv => kv.Key == "{OriginalFormat}" && Equals(kv.Value, "Order {OrderId} paid by {Customer}"));
        }

        [Fact]
        public void Log_WrapsEntryInJobScope_WhenLogging()
        {
            var loggers = new CapturingLoggerFactory();
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(Context(typeof(OrderPaidHandler)))
                .Log(HandlerLogType.Warning, "careful");

            var scope = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object>>>(Assert.Single(loggers.Entries).Scope).ToList();
            Assert.Contains(scope, kv => kv.Key == "JobId" && Equals(kv.Value, "42"));
            Assert.Contains(scope, kv => kv.Key == "AttemptNumber" && Equals(kv.Value, 3));
        }

        [Theory]
        [InlineData(HandlerLogType.Trace, LogLevel.Trace)]
        [InlineData(HandlerLogType.Debug, LogLevel.Debug)]
        [InlineData(HandlerLogType.Information, LogLevel.Information)]
        [InlineData(HandlerLogType.Success, LogLevel.Information)]
        [InlineData(HandlerLogType.Warning, LogLevel.Warning)]
        [InlineData(HandlerLogType.Error, LogLevel.Error)]
        [InlineData(HandlerLogType.Critical, LogLevel.Critical)]
        public void Log_MapsLevel_WhenLogging(HandlerLogType level, LogLevel expected)
        {
            var loggers = new CapturingLoggerFactory();
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(Context()).Log(level, "x");

            Assert.Equal(expected, Assert.Single(loggers.Entries).Level);
        }

        [Fact]
        public void Log_SkipsEntry_WhenLevelIsDisabled()
        {
            var loggers = new CapturingLoggerFactory { Minimum = LogLevel.Warning };
            new MicrosoftExecutionLoggerFactory(loggers).CreateLogger(Context()).Log(HandlerLogType.Information, "x");

            Assert.Empty(loggers.Entries);
        }

        [Fact]
        public void AddMicrosoftLogging_RegistersFactoryOnce_WhenCalledTwice()
        {
            var options = new TestOptions();
            options.AddMicrosoftLogging().AddMicrosoftLogging();

            Assert.Single(options.LoggerFactoryTypes, t => t == typeof(MicrosoftExecutionLoggerFactory));
        }
    }
}
