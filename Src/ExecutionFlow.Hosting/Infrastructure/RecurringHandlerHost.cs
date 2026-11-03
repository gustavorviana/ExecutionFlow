using Cronos;
using ExecutionFlow.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hosting.Infrastructure
{
    /// <summary>
    /// The scheduler of one handler that runs on its own schedule. The first run is at the first occurrence after the host
    /// starts. A cron is evaluated in the handler's time zone; an interval counts on a fixed cadence from the host start, or
    /// from the end of the previous run (<see cref="HostingOptions.IntervalFromEnd"/>).
    /// </summary>
    internal sealed class RecurringHandlerHost : BackgroundService
    {
        private readonly RecurringJobRegistryInfo _registration;
        private readonly RunCoordinator _coordinator;
        private readonly IClock _clock;
        private readonly ILogger _logger;
        private readonly CronExpression _cron;
        private readonly TimeZoneInfo _timeZone;
        private readonly bool _fromEnd;

        public RecurringHandlerHost(RecurringJobRegistryInfo registration, HostingSetup setup, RunCoordinator coordinator, IClock clock, ILogger logger)
        {
            _registration = registration;
            _coordinator = coordinator;
            _clock = clock;
            _logger = logger;

            if (registration.Interval.HasValue)
            {
                _fromEnd = setup.Options.IntervalFromEndOf(registration.HandlerType);
            }
            else
            {
                _cron = ParseCron(registration.Cron);
                _timeZone = setup.TimeZoneOf(registration);
            }
        }

        /// <summary>Parses a cron with 5 fields, or 6 when the first one is seconds.</summary>
        internal static CronExpression ParseCron(string cron)
        {
            var fields = cron.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return CronExpression.Parse(cron, fields == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var stopping = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _coordinator.Stopping))
            {
                var start = _clock.UtcNow;
                var last = start;

                while (!stopping.IsCancellationRequested)
                {
                    var next = NextOccurrence(start, last);
                    if (next == null)
                    {
                        _logger.LogWarning("ExecutionFlow: the cron '{Cron}' of '{Handler}' has no future occurrence, so it stops being scheduled.",
                            _registration.Cron, _registration.HandlerType.FullName);
                        return;
                    }

                    try
                    {
                        await _clock.Delay(next.Value - _clock.UtcNow, stopping.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    last = next.Value;
                    if (_fromEnd)
                    {
                        await _coordinator.RunOccurrenceAsync(_registration.HandlerType).ConfigureAwait(false);
                        last = _clock.UtcNow;
                    }
                    else
                    {
                        _coordinator.StartOccurrence(_registration.HandlerType);
                    }
                }
            }
        }

        /// <summary>
        /// The host is stopping: cancel the runs first, since a run counted from its end keeps this loop waiting for it.
        /// </summary>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _coordinator.Cancel();
            return base.StopAsync(cancellationToken);
        }

        private DateTimeOffset? NextOccurrence(DateTimeOffset start, DateTimeOffset last)
        {
            var now = _clock.UtcNow;
            if (_cron != null)
                return _cron.GetNextOccurrence(last > now ? last : now, _timeZone);

            var interval = _registration.Interval.Value;
            var next = last + interval;
            if (_fromEnd || next > now)
                return next;

            // Fell behind the fixed cadence: the next occurrence of the cadence after now.
            var elapsed = (now - start).Ticks / interval.Ticks + 1;
            return start + TimeSpan.FromTicks(interval.Ticks * elapsed);
        }
    }
}
