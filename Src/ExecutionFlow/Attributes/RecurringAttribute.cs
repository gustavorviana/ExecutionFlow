using System;

namespace ExecutionFlow.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RecurringAttribute : Attribute
    {
        public string Cron { get; }

        public RecurringAttribute(string cron)
        {
            Cron = cron;
        }
    }
}
