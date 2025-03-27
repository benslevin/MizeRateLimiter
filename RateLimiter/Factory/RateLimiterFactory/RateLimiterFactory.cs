using Microsoft.Extensions.Logging;
using RateLimiterProgram.Services;

namespace RateLimiter.Factory.RateLimiterFactory
{
    /// <summary>
    /// Default implementation of the rate limiter factory.
    /// </summary>
    public class RateLimiterFactory : IRateLimiterFactory
    {
        private readonly ITimeProvider _timeProvider;
        private readonly ILoggerFactory _loggerFactory;

        public RateLimiterFactory(ITimeProvider timeProvider, ILoggerFactory loggerFactory)
        {
            _timeProvider = timeProvider;
            _loggerFactory = loggerFactory;
        }

        public RateLimiter<TKey> Create<TKey>(Func<TKey, Task> performAction, IEnumerable<RateLimit> rateLimits)
        {
            var logger = _loggerFactory.CreateLogger<RateLimiter<TKey>>();
            return new RateLimiter<TKey>(performAction, rateLimits.ToArray(), _timeProvider, logger);
        }
    }
}
