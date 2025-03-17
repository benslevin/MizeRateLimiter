namespace RateLimiter.Services
{
    public class RateLimit
    {
        public int MaxRequest { get; }
        public TimeSpan TimeWindow { get; }

        public RateLimit(int maxRequest, TimeSpan timeWindow)
        {
            MaxRequest = maxRequest;
            TimeWindow = timeWindow;
        }
    }
}
