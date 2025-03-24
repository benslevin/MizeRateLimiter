using Microsoft.Extensions.Logging;
using Moq;
using RateLimiterProgram.Services;

namespace RateLimiterTests
{
    public class RateLimiterTests
    {
        private readonly Mock<ILogger<RateLimiter<string>>> _loggerMock;
        private readonly Mock<ITimeProvider> _timeProviderMock;
        private readonly List<string> _executedItems;
        private readonly Func<string, Task> _action;

        public RateLimiterTests()
        {
            _loggerMock = new Mock<ILogger<RateLimiter<string>>>();
            _timeProviderMock = new Mock<ITimeProvider>();
            _executedItems = new List<string>();
            _action = async (arg) =>
            {
                await Task.Delay(10); // Small delay to simulate work
                lock (_executedItems)
                {
                    _executedItems.Add(arg);
                }
            };
        }

        [Fact]
        public async Task Perform_SingleRequest_ExecutesAction()
        {
            // Arrange
            var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(now);

            var limits = new[]
            {
                new RateLimit(10, TimeSpan.FromSeconds(1))
            };

            var rateLimiter = new RateLimiter<string>(_action, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act
            await rateLimiter.Perform("test-item");

            // Assert
            Assert.Single(_executedItems);
            Assert.Equal("test-item", _executedItems[0]);
        }

        [Fact]
        public async Task Perform_MultipleRequests_ExecutesInOrder()
        {
            // Arrange
            var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(now);

            var limits = new[]
            {
                new RateLimit(10, TimeSpan.FromSeconds(1))
            };
            var rateLimiter = new RateLimiter<string>(_action, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                string item = $"item-{i}";
                tasks.Add(rateLimiter.Perform(item));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(5, _executedItems.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal($"item-{i}", _executedItems[i]);
            }
        }

        [Fact]
        public async Task Perform_ExceedsRateLimit_DelaysExecution()
        {
            // Arrange
            var currentTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            _timeProviderMock.SetupSequence(tp => tp.UtcNow)
                .Returns(currentTime)                          // First check
                .Returns(currentTime)                          // Record first request
                .Returns(currentTime)                          // Second check
                .Returns(currentTime)                          // Rate limit check - will exceed
                .Returns(currentTime.AddMilliseconds(500))     // Still within rate limit window
                .Returns(currentTime.AddSeconds(1.1));         // Beyond rate limit window - should allow next request

            var limits = new[]
            {
                new RateLimit(1, TimeSpan.FromSeconds(1))
            };

            var rateLimiter = new RateLimiter<string>(_action, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act
            var task1 = rateLimiter.Perform("first");
            var task2 = rateLimiter.Perform("second");

            await Task.WhenAll(task1, task2);

            // Assert
            Assert.Equal(2, _executedItems.Count);
            Assert.Equal("first", _executedItems[0]);
            Assert.Equal("second", _executedItems[1]);
        }

        [Fact]
        public async Task Perform_MultipleRateLimits_RespectsAllLimits()
        {
            // Arrange
            var startTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // Configure time provider to simulate time passing
            Queue<DateTime> times = new Queue<DateTime>();

            // Initial requests (2 requests)
            times.Enqueue(startTime); // First request check
            times.Enqueue(startTime); // First request record
            times.Enqueue(startTime); // Second request check
            times.Enqueue(startTime); // Second request record

            // Third request (exceeds short window, but not long window)
            times.Enqueue(startTime); // Check
            times.Enqueue(startTime.AddSeconds(0.1)); // Still within short window
            times.Enqueue(startTime.AddSeconds(0.2)); // Still within short window
            times.Enqueue(startTime.AddSeconds(3.1)); // Beyond short window, within long window
            times.Enqueue(startTime.AddSeconds(3.1)); // Record third request

            // Fourth request (exceeds long window)
            times.Enqueue(startTime.AddSeconds(3.2)); // Check
            times.Enqueue(startTime.AddSeconds(3.3)); // Still within long window
            times.Enqueue(startTime.AddSeconds(3.4)); // Still within long window
            times.Enqueue(startTime.AddSeconds(12.1)); // Beyond long window
            times.Enqueue(startTime.AddSeconds(12.1)); // Record fourth request

            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(() => times.Dequeue());

            // Configure two rate limits: 
            // - 2 requests per 3 seconds
            // - 3 requests per 10 seconds
            var limits = new[]
            {
                new RateLimit(2, TimeSpan.FromSeconds(3)),
                new RateLimit(3, TimeSpan.FromSeconds(10))
            };

            var rateLimiter = new RateLimiter<string>(_action, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act
            await rateLimiter.Perform("request-1");
            await rateLimiter.Perform("request-2");
            await rateLimiter.Perform("request-3"); // Should wait for 3-second window
            await rateLimiter.Perform("request-4"); // Should wait for 10-second window

            // Assert
            Assert.Equal(4, _executedItems.Count);
            Assert.Equal("request-1", _executedItems[0]);
            Assert.Equal("request-2", _executedItems[1]);
            Assert.Equal("request-3", _executedItems[2]);
            Assert.Equal("request-4", _executedItems[3]);
        }

        [Fact]
        public async Task Perform_ActionThrowsException_PropagatesException()
        {
            // Arrange
            var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(now);

            var limits = new[]
            {
                new RateLimit(10, TimeSpan.FromSeconds(1))
            };

            // Action that will throw an exception
            Func<string, Task> failingAction = (arg) =>
            {
                throw new InvalidOperationException("Test exception");
            };

            var rateLimiter = new RateLimiter<string>(failingAction, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => rateLimiter.Perform("test-item"));
            Assert.Equal("Test exception", ex.Message);
        }

        [Fact]
        public async Task Perform_ContinuesProcessingAfterException()
        {
            // Arrange
            var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(now);

            var limits = new[]
            {
                new RateLimit(10, TimeSpan.FromSeconds(1))
            };

            List<string> executedItems = new List<string>();

            // Action that throws for specific items
            Func<string, Task> mixedAction = async (arg) =>
            {
                await Task.Delay(10);
                if (arg == "fail-item")
                {
                    throw new InvalidOperationException("Test exception");
                }

                lock (executedItems)
                {
                    executedItems.Add(arg);
                }
            };

            var rateLimiter = new RateLimiter<string>(mixedAction, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act
            var task1 = rateLimiter.Perform("first-item");
            var failTask = Assert.ThrowsAsync<InvalidOperationException>(() => rateLimiter.Perform("fail-item"));
            var task3 = rateLimiter.Perform("third-item");

            await Task.WhenAll(task1, failTask, task3);

            // Assert
            Assert.Equal(2, executedItems.Count);
            Assert.Equal("first-item", executedItems[0]);
            Assert.Equal("third-item", executedItems[1]);
        }

        [Fact]
        public async Task CleanupExpiredEntries_CorrectlyRemovesOldEntries()
        {
            // Arrange
            var startTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // Set up time provider to return incrementing times
            Queue<DateTime> times = new Queue<DateTime>();

            // First 5 requests at different times within the window
            for (int i = 0; i < 5; i++)
            {
                // Add the time twice - once for checking, once for recording
                times.Enqueue(startTime.AddSeconds(i));
                times.Enqueue(startTime.AddSeconds(i));
            }

            // Now add a time that's beyond the window
            times.Enqueue(startTime.AddSeconds(10));
            times.Enqueue(startTime.AddSeconds(10));

            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(() => times.Dequeue());

            var limits = new[]
            {
                new RateLimit(5, TimeSpan.FromSeconds(5))
            };

            // Create a rate limiter with a small max request to test cleanup
            var rateLimiter = new RateLimiter<string>(_action, limits, _timeProviderMock.Object, _loggerMock.Object);

            // Act - first fill up to the limit
            for (int i = 0; i < 5; i++)
            {
                await rateLimiter.Perform($"item-{i}");
            }

            // The next call should work because old entries will be cleaned up
            await rateLimiter.Perform("final-item");

            // Assert
            Assert.Equal(6, _executedItems.Count);
            Assert.Equal("final-item", _executedItems[5]);
        }
    }
}