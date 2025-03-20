using RateLimiterProgram.Services;

namespace RateLimiterTests
{
    public class SlidingWindowRateLimitTests
    {

        [Fact] 
        public async Task WaitForPermissionAsync_AllowsRequestsWithinLimit()
        {
            var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var rateLimits = new List<RateLimit> { 
                new RateLimit(3, TimeSpan.FromSeconds(10))
            };
            var limiter = new SlidingWindowRateLimit(rateLimits, fakeTimeProvider);

            await limiter.WaitForPermissionAsync();
            await limiter.WaitForPermissionAsync();
            await limiter.WaitForPermissionAsync();
        }

        [Fact]
        public async Task WaitForPermissionAsync_BlocksWhenLimitIsExceeded()
        {
            var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var rateLimits = new List<RateLimit> {
                new RateLimit(2, TimeSpan.FromSeconds(10))
            };
            var limiter = new SlidingWindowRateLimit(rateLimits, fakeTimeProvider);

            await limiter.WaitForPermissionAsync();
            await limiter.WaitForPermissionAsync();

            var delayTask = Task.Run(async () =>
            {
                await limiter.WaitForPermissionAsync();
            });

            await Task.Delay(500);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(10));

            await delayTask;
        }
    }
}
