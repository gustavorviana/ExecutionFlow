using System;

namespace ExecutionFlow.Abstractions
{
    public interface IJobRegistryInfo
    {
        Type HandlerType { get; }
        string DisplayName { get; }
    }
}
