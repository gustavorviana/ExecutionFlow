namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Defines the severity levels for handler logging.
    /// </summary>
    public enum HandlerLogType
    {
        /// <summary>Verbose diagnostic information.</summary>
        Trace = 0,
        /// <summary>Debug-level diagnostic information.</summary>
        Debug = 1,
        /// <summary>General informational messages.</summary>
        Information = 2,
        /// <summary>Potentially harmful situations.</summary>
        Warning = 3,
        /// <summary>Error events that allow the application to continue.</summary>
        Error = 4,
        /// <summary>Critical failures requiring immediate attention.</summary>
        Critical = 5,
        /// <summary>Successful operation completion.</summary>
        Success = 6
    }
}
