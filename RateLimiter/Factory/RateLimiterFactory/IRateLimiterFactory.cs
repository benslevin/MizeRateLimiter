using RateLimiterProgram.Services;

namespace RateLimiter.Factory.RateLimiterFactory
{
    /// <summary>
    /// Factory for creating rate limiters with proper dependencies.
    /// </summary>
    public interface IRateLimiterFactory
    {
        /// <summary>
        /// Creates a rate limiter for the specified key type.
        /// </summary>
        /// <typeparam name="TKey">The type of key to rate limit by.</typeparam>
        /// <param name="performAction">The action to perform when allowed.</param>
        /// <param name="rateLimits">The rate limits to apply.</param>
        /// <returns>A configured rate limiter.</returns>
        RateLimiter<TKey> Create<TKey>(Func<TKey, Task> performAction, IEnumerable<RateLimit> rateLimits);
    }
}
