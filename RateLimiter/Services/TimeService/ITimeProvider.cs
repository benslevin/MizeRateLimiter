namespace RateLimiterProgram.Services
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
