namespace RateLimiter.Services
{
    public class RateLimiter<TArg>
    {
        private readonly Func<TArg, Task> _action;
        private readonly List<IRateLimitStrategy> _rateLimitStrategies;

        public RateLimiter (Func<TArg, Task> action, IEnumerable<RateLimit> rateLimits, ITimeProvider timeProvider)
        {
            _action = action;
            _rateLimitStrategies = rateLimits
                .Select(rl => new SlidingWindowRateLimit(rl, timeProvider))
                .ToList<IRateLimitStrategy>();
        }

        public async Task Perform(TArg args)
        {
            foreach (var strategy in _rateLimitStrategies)
            {
                await strategy.WaitForPermissionAsync();
            }
            await _action(args);
        }
    }
}
