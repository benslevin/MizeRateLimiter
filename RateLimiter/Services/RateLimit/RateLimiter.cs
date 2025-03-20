namespace RateLimiterProgram.Services
{
    public class RateLimiter<TArg>
    {
        private readonly Func<TArg, Task> _action;
        private readonly List<IRateLimitStrategy> _rateLimitStrategies;

        public RateLimiter(Func<TArg, Task> action, IEnumerable<RateLimit> rateLimits, ITimeProvider timeProvider)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));

            if (rateLimits == null || !rateLimits.Any())
                throw new ArgumentException("At least one rate limit must be provided.", nameof(rateLimits));

            _rateLimitStrategies = rateLimits
                .Select(rl => new SlidingWindowRateLimit(new List<RateLimit> { rl }, timeProvider))
                .ToList<IRateLimitStrategy>();
        }

        public async Task Perform(TArg args)
        {
            await Task.WhenAll(_rateLimitStrategies.Select(strategy => strategy.WaitForPermissionAsync()));
            await _action(args);
        }
    }
}
