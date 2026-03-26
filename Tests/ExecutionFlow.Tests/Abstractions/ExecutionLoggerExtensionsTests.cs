using ExecutionFlow.Abstractions;
using NSubstitute;

namespace ExecutionFlow.Tests.Abstractions;

public class ExecutionLoggerExtensionsTests
{
    private readonly IExecutionLogger _logger = Substitute.For<IExecutionLogger>();

    [Fact]
    public void Trace_String_CallsLogWithTraceLevel()
    {
        _logger.Trace("msg");
        _logger.Received(1).Log(HandlerLogType.Trace, "msg");
    }

    [Fact]
    public void Trace_StringWithArgs_CallsLogWithTraceLevel()
    {
        _logger.Trace("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Trace, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Trace_Object_CallsToString()
    {
        _logger.Trace((object)42);
        _logger.Received(1).Log(HandlerLogType.Trace, "42");
    }

    [Fact]
    public void Trace_NullObject_PassesNull()
    {
        _logger.Trace((object)null!);
        _logger.Received(1).Log(HandlerLogType.Trace, null);
    }

    [Fact]
    public void Debug_String_CallsLogWithDebugLevel()
    {
        _logger.Debug("msg");
        _logger.Received(1).Log(HandlerLogType.Debug, "msg");
    }

    [Fact]
    public void Debug_StringWithArgs_CallsLogWithDebugLevel()
    {
        _logger.Debug("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Debug, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Debug_Object_CallsToString()
    {
        _logger.Debug((object)"hello");
        _logger.Received(1).Log(HandlerLogType.Debug, "hello");
    }

    [Fact]
    public void Info_String_CallsLogWithInformationLevel()
    {
        _logger.Info("msg");
        _logger.Received(1).Log(HandlerLogType.Information, "msg");
    }

    [Fact]
    public void Info_StringWithArgs_CallsLogWithInformationLevel()
    {
        _logger.Info("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Information, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Info_Object_CallsToString()
    {
        _logger.Info((object)123);
        _logger.Received(1).Log(HandlerLogType.Information, "123");
    }

    [Fact]
    public void Warning_String_CallsLogWithWarningLevel()
    {
        _logger.Warning("msg");
        _logger.Received(1).Log(HandlerLogType.Warning, "msg");
    }

    [Fact]
    public void Warning_StringWithArgs_CallsLogWithWarningLevel()
    {
        _logger.Warning("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Warning, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Warning_Object_CallsToString()
    {
        _logger.Warning((object)"warn");
        _logger.Received(1).Log(HandlerLogType.Warning, "warn");
    }

    [Fact]
    public void Error_String_CallsLogWithErrorLevel()
    {
        _logger.Error("msg");
        _logger.Received(1).Log(HandlerLogType.Error, "msg");
    }

    [Fact]
    public void Error_StringWithArgs_CallsLogWithErrorLevel()
    {
        _logger.Error("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Error, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Error_Object_CallsToString()
    {
        _logger.Error((object)"err");
        _logger.Received(1).Log(HandlerLogType.Error, "err");
    }

    [Fact]
    public void Error_Exception_CallsLogWithExceptionToString()
    {
        var ex = new InvalidOperationException("boom");
        _logger.Error(ex);
        _logger.Received(1).Log(HandlerLogType.Error, Arg.Is<string>(s => s.Contains("boom")));
    }

    [Fact]
    public void Error_NullException_PassesNull()
    {
        _logger.Error((Exception)null!);
        _logger.Received(1).Log(HandlerLogType.Error, null);
    }

    [Fact]
    public void Success_String_CallsLogWithSuccessLevel()
    {
        _logger.Success("msg");
        _logger.Received(1).Log(HandlerLogType.Success, "msg");
    }

    [Fact]
    public void Success_StringWithArgs_CallsLogWithSuccessLevel()
    {
        _logger.Success("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Success, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Success_Object_CallsToString()
    {
        _logger.Success((object)"ok");
        _logger.Received(1).Log(HandlerLogType.Success, "ok");
    }

    [Fact]
    public void Critical_String_CallsLogWithCriticalLevel()
    {
        _logger.Critical("msg");
        _logger.Received(1).Log(HandlerLogType.Critical, "msg");
    }

    [Fact]
    public void Critical_StringWithArgs_CallsLogWithCriticalLevel()
    {
        _logger.Critical("msg {0}", "arg");
        _logger.Received(1).Log(HandlerLogType.Critical, "msg {0}", Arg.Any<object[]>());
    }

    [Fact]
    public void Critical_Object_CallsToString()
    {
        _logger.Critical((object)"crit");
        _logger.Received(1).Log(HandlerLogType.Critical, "crit");
    }

    [Fact]
    public void Critical_Exception_CallsLogWithExceptionToString()
    {
        var ex = new InvalidOperationException("critical failure");
        _logger.Critical(ex);
        _logger.Received(1).Log(HandlerLogType.Critical, Arg.Is<string>(s => s.Contains("critical failure")));
    }
}
