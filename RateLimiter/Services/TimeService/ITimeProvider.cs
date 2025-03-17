namespace RateLimiter.Services
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
