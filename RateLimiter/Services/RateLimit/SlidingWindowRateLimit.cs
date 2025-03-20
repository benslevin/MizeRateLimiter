namespace RateLimiterProgram.Services
{
    public class SlidingWindowRateLimit : IRateLimitStrategy
    {

        private readonly IEnumerable<RateLimit> _rateLimits;
        private readonly Dictionary<RateLimit, Queue<DateTime>> _requestTimestamps;
        private readonly object _lock = new object();
        private readonly ITimeProvider _timeProvider;

        public SlidingWindowRateLimit(IEnumerable<RateLimit> rateLimits, ITimeProvider timeProvider)
        {
            _rateLimits = rateLimits ?? throw new ArgumentNullException(nameof(rateLimits));
            _requestTimestamps = new Dictionary<RateLimit, Queue<DateTime>>();
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

            // Initialize queues for each rate limit
            foreach (var rateLimit in _rateLimits)
            {
                _requestTimestamps[rateLimit] = new Queue<DateTime>();
            }
        }

        private void RemoveOldRequests()
        {
            lock (_lock)
            {
                foreach (var rateLimit in _rateLimits)
                {
                    var cutoffTime = _timeProvider.UtcNow - rateLimit.TimeWindow;

                    while (_requestTimestamps[rateLimit].Count > 0 && _requestTimestamps[rateLimit].Peek() < cutoffTime)
                    {
                        _requestTimestamps[rateLimit].Dequeue();
                    }
                }
            }
        }

        public async Task Perform(Func<Task> action)
        {
            await WaitForPermissionAsync();
            await action();
        }

        public async Task WaitForPermissionAsync()
        {
            while (true)
            {
                DateTime now = _timeProvider.UtcNow;
                bool canProceed = false;

                lock (_lock)
                {
                    RemoveOldRequests();

                    canProceed = _rateLimits.All(rateLimit =>
                        _requestTimestamps[rateLimit].Count < rateLimit.MaxRequest);

                    if (canProceed)
                    {
                        foreach (var rateLimit in _rateLimits)
                        {
                            _requestTimestamps[rateLimit].Enqueue(now);
                        }
                    }
                }

                if (canProceed) return;

                // Instead of a fixed delay, calculate when the next request will be possible
                var earliestExpiration = _rateLimits
                    .Select(rateLimit => _requestTimestamps[rateLimit].Peek())
                    .Min();

                var waitTime = earliestExpiration - now;
                if (waitTime > TimeSpan.Zero)
                    await Task.Delay(waitTime);
                else
                    await Task.Delay(50); // Prevent tight loop if calculations are off
            }
        }
    }
}
