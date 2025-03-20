using RateLimiterProgram.Services;

namespace RateLimiterTests
{
    public class RateLimiterTests
    {
        [Fact]
        public async Task Perform_HonorsAllRateLimits()
        {
            var fakeTimeProvider = new FakeTimeProvider(DateTime.UtcNow);

            var rateLimits = new List<RateLimit>
            {
                new RateLimit(2, TimeSpan.FromSeconds(10)),  // 2 requests per 10 sec
                new RateLimit(5, TimeSpan.FromSeconds(30))   // 5 requests per 30 sec
            };

            var limiter = new RateLimiter<string>(
                async msg => Console.WriteLine($"{DateTime.UtcNow}: {msg}"),
                rateLimits,
                fakeTimeProvider
            );

            // Should allow two requests immediately
            await limiter.Perform("Request 1");
            await limiter.Perform("Request 2");

            var delayTask = Task.Run(async () =>
            {
                Console.WriteLine("[Test] Third request should be blocked...");
                await limiter.Perform("Request 3");
                Console.WriteLine("[Test] Third request should now be allowed!");
            });

            await Task.Delay(500);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(10)); // Move time forward for 10s limit

            await delayTask;

            // More requests testing 30s limit
            await limiter.Perform("Request 4");
            await limiter.Perform("Request 5");
            await limiter.Perform("Request 6");

            var secondDelayTask = Task.Run(async () =>
            {
                Console.WriteLine("[Test] Sixth request should be blocked...");
                await limiter.Perform("Request 7");
                Console.WriteLine("[Test] Sixth request should now be allowed!");
            });

            await Task.Delay(500);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(30));

            await secondDelayTask;
        }
    }
}
