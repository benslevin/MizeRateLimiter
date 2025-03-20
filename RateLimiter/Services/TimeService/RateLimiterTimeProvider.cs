namespace RateLimiterProgram.Services
{
    public class RateLimiterTimeProvider : ITimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
