﻿namespace RateLimiter.Services
{
    public class RateLimiterTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
