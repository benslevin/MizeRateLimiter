namespace RateLimiterProgram.Services
{
    public record RateLimit(int MaxRequest, TimeSpan TimeWindow);
    
}
