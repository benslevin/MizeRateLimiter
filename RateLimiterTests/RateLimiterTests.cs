using Moq;
using RateLimiterProgram.Services;

namespace RateLimiterTests
{
    public class RateLimiterTests
    {
        private Mock<ITimeProvider> _mockTimeProvider;
        private DateTime _currentTime;

        public RateLimiterTests()
        {
            _currentTime = new DateTime(2025, 3, 20, 12, 0, 0);
            _mockTimeProvider = new Mock<ITimeProvider>();
            _mockTimeProvider.Setup(tp => tp.Now).Returns(() => _currentTime);
            _mockTimeProvider.Setup(tp => tp.UtcNow).Returns(() => _currentTime.ToUniversalTime());
        }

        [Fact]
        public async Task Perform_SingleRateLimit_RespectsLimit()
        {
            // Arrange
            int executionCount = 0;
            var rateLimiter = new RateLimiter<string>(
                async (arg) => { executionCount++; await Task.CompletedTask; },
                new[] { new RateLimit(3, TimeSpan.FromSeconds(10)) },
                _mockTimeProvider.Object
            );

            // Act - Execute up to the limit
            for (int i = 0; i < 3; i++)
            {
                await rateLimiter.Perform("test");
            }

            // Assert - First 3 executions should succeed immediately
            Assert.Equal(3, executionCount);

            // Act - Start a task that will be rate limited
            var limitedTask = Task.Run(async () =>
            {
                await rateLimiter.Perform("test");
            });

            // Wait a short time - not enough for rate limit to expire
            await Task.Delay(50);

            // Assert - Task should be waiting because of rate limit
            Assert.False(limitedTask.IsCompleted);
            Assert.Equal(3, executionCount);

            // Act - Advance time to expire rate limit
            _currentTime = _currentTime.AddSeconds(10);

            // Wait for the rate-limited task to complete
            await Task.Delay(200);

            // Assert - Task should complete after time advances
            Assert.True(limitedTask.IsCompleted);
            Assert.Equal(4, executionCount);
        }

        [Fact]
        public async Task Perform_MultipleRateLimits_RespectsAllLimits()
        {
            // Arrange
            int executionCount = 0;
            var rateLimiter = new RateLimiter<string>(
                async (arg) => { executionCount++; await Task.CompletedTask; },
                new[]
                {
                    new RateLimit(3, TimeSpan.FromSeconds(10)),  // 3 per 10 seconds
                    new RateLimit(5, TimeSpan.FromMinutes(1))    // 5 per minute
                },
                _mockTimeProvider.Object
            );

            // Act - Execute up to the first limit
            for (int i = 0; i < 3; i++)
            {
                await rateLimiter.Perform("test");
            }

            // Assert - Should hit the first rate limit
            Assert.Equal(3, executionCount);

            // Act - Advance time to expire first rate limit
            _currentTime = _currentTime.AddSeconds(10);

            // Execute more to hit the second limit
            for (int i = 0; i < 2; i++)
            {
                await rateLimiter.Perform("test");
            }

            // Assert - Should hit the second rate limit
            Assert.Equal(5, executionCount);

            // Act - Try to exceed the second limit
            var limitedTask = Task.Run(async () =>
            {
                await rateLimiter.Perform("test");
            });

            // Wait a short time
            await Task.Delay(50);

            // Assert - Task should be waiting because of second rate limit
            Assert.False(limitedTask.IsCompleted);
            Assert.Equal(5, executionCount);

            // Act - Advance time to expire second rate limit
            _currentTime = _currentTime.AddMinutes(1);

            // Wait for the rate-limited task to complete
            await Task.Delay(200);

            // Assert - Task should complete after time advances
            Assert.True(limitedTask.IsCompleted);
            Assert.Equal(6, executionCount);
        }

        [Fact]
        public async Task Perform_SlidingWindow_RespectsTimeWindow()
        {
            // Arrange
            int executionCount = 0;
            var rateLimiter = new RateLimiter<string>(
                async (arg) => { executionCount++; await Task.CompletedTask; },
                new[] { new RateLimit(3, TimeSpan.FromSeconds(10)) },
                _mockTimeProvider.Object
            );

            // Act - Execute first request
            await rateLimiter.Perform("test1");
            Assert.Equal(1, executionCount);

            // Move time forward but stay within window
            _currentTime = _currentTime.AddSeconds(4);

            // Execute second request
            await rateLimiter.Perform("test2");
            Assert.Equal(2, executionCount);

            // Move time forward but stay within window
            _currentTime = _currentTime.AddSeconds(4);

            // Execute third request
            await rateLimiter.Perform("test3");
            Assert.Equal(3, executionCount);

            // Try fourth request - should be limited
            var limitedTask = Task.Run(async () =>
            {
                await rateLimiter.Perform("test4");
            });

            // Wait a short time
            await Task.Delay(50);

            // Assert - Task should be waiting
            Assert.False(limitedTask.IsCompleted);
            Assert.Equal(3, executionCount);

            // Move time forward to expire first request
            _currentTime = _currentTime.AddSeconds(3); // Now at t+11, first request at t+0 is expired

            // Wait for rate-limited task to complete
            await Task.Delay(200);

            // Assert - Task should complete after oldest request expires
            Assert.True(limitedTask.IsCompleted);
            Assert.Equal(4, executionCount);
        }

        [Fact]
        public async Task Perform_ConcurrentRequests_ThreadSafe()
        {
            // Arrange
            int executionCount = 0;
            var rateLimiter = new RateLimiter<int>(
                async (arg) => { executionCount++; await Task.CompletedTask; },
                new[] { new RateLimit(5, TimeSpan.FromSeconds(10)) },
                _mockTimeProvider.Object
            );

            // Act - Launch 10 concurrent requests
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int requestNum = i;
                tasks.Add(Task.Run(async () =>
                {
                    await rateLimiter.Perform(requestNum);
                }));
            }

            // Wait a short time for the first 5 to complete
            await Task.Delay(100);

            // Assert - Only 5 should have executed
            Assert.Equal(5, executionCount);

            // Advance time to allow more requests
            _currentTime = _currentTime.AddSeconds(10);

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Assert - All 10 should have executed
            Assert.Equal(10, executionCount);
        }

        [Fact]
        public async Task Perform_CleanupExpiredEntries_WorksCorrectly()
        {
            // Arrange
            int executionCount = 0;
            var rateLimiter = new RateLimiter<string>(
                async (arg) => { executionCount++; await Task.CompletedTask; },
                new[] { new RateLimit(3, TimeSpan.FromSeconds(5)) },
                _mockTimeProvider.Object
            );

            // Act - Execute 3 requests at time t=0
            await rateLimiter.Perform("test1");
            await rateLimiter.Perform("test2");
            await rateLimiter.Perform("test3");
            Assert.Equal(3, executionCount);

            // Trying a 4th request - should be rate limited
            var task4 = Task.Run(async () => await rateLimiter.Perform("test4"));
            await Task.Delay(50); // Give a little time for the task to start waiting
            Assert.False(task4.IsCompleted);

            // Advance time by 6 seconds - this should expire all previous requests
            _currentTime = _currentTime.AddSeconds(6);

            // Wait for task4 to complete now that time has advanced
            await Task.WhenAny(task4, Task.Delay(500));
            Assert.True(task4.IsCompleted);
            Assert.Equal(4, executionCount);

            // We should be able to do 2 more immediately since we've only used 1 of our 3 slots
            await rateLimiter.Perform("test5");
            await rateLimiter.Perform("test6");
            Assert.Equal(6, executionCount);

            // But the 7th should be limited again
            var task7 = Task.Run(async () => await rateLimiter.Perform("test7"));
            await Task.Delay(50);
            Assert.False(task7.IsCompleted);

            // Advance time to expire the oldest request in our current window
            _currentTime = _currentTime.AddSeconds(6);

            // Wait for task7 to complete
            await Task.WhenAny(task7, Task.Delay(500));
            Assert.True(task7.IsCompleted);
            Assert.Equal(7, executionCount);
        }
    }
}