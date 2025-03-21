namespace RateLimiterProgram.Services
{
    /// <summary>
    /// Represents a rate limit rule with a maximum number of requests within a specified time window.
    /// </summary>
    public record RateLimit(int MaxRequest, TimeSpan TimeWindow);
    
}
