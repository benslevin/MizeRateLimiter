using System.Collections.Concurrent;

namespace RateLimiterProgram.Services
{
    public class RateLimiter<TArg> : IRateLimiter<TArg>
    {
        private readonly Func<TArg, Task> _action;
        private readonly RateLimit[] _limits;
        private readonly ITimeProvider _timeProvider;
        private readonly ConcurrentDictionary<TimeSpan, ConcurrentQueue<DateTime>> _requestLogs;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public RateLimiter(Func<TArg, Task> action, RateLimit[] limits, ITimeProvider timeProvider)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

            _requestLogs = new ConcurrentDictionary<TimeSpan, ConcurrentQueue<DateTime>>();
            foreach (var limit in _limits)
            {
                _requestLogs[limit.TimeWindow] = new ConcurrentQueue<DateTime>();
            }
        }

        public async Task Perform(TArg argument)
        {
            await WaitForRateLimitAsync();
            await _action(argument);
        }

        private async Task WaitForRateLimitAsync()
        {
            while (true)
            {
                await _semaphore.WaitAsync();
                try
                {
                    DateTime now = _timeProvider.UtcNow;
                    bool canExecute = true;

                    // First, clean up expired entries from queues
                    foreach (var limit in _limits)
                    {
                        var queue = _requestLogs[limit.TimeWindow];
                        CleanupExpiredEntries(queue, limit.TimeWindow, now);
                    }

                    // Check if any limit would be exceeded
                    foreach (var limit in _limits)
                    {
                        var queue = _requestLogs[limit.TimeWindow];
                        if (queue.Count >= limit.MaxRequest)
                        {
                            canExecute = false;
                            break;
                        }
                    }

                    if (canExecute)
                    {
                        // Record this execution for all limits
                        foreach (var limit in _limits)
                        {
                            _requestLogs[limit.TimeWindow].Enqueue(now);
                        }
                        return; // We can execute now
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                // If we can't execute now, wait a bit before trying again
                await Task.Delay(100);
            }
        }

        private void CleanupExpiredEntries(ConcurrentQueue<DateTime> queue, TimeSpan window, DateTime now)
        {
            // Get the cutoff time for this window
            DateTime cutoff = now - window;

            // Remove entries older than the cutoff
            while (queue.TryPeek(out DateTime oldest) && oldest <= cutoff)
            {
                queue.TryDequeue(out _);
            }
        }
    }
}