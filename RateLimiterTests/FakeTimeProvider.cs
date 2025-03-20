using RateLimiterProgram.Services;

namespace RateLimiterTests
{
    public class FakeTimeProvider : ITimeProvider
    {
        private DateTime utcNow;

        public FakeTimeProvider(DateTime initialTime) 
        {
            utcNow = initialTime;
        }

        public DateTime UtcNow => utcNow;   

        public void Advance(TimeSpan timeSpan)
        {
            utcNow = utcNow.Add(timeSpan);
        }
    }
}
