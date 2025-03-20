namespace RateLimiterProgram.Services
{
    public interface ITimeProvider
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }

    }
}
