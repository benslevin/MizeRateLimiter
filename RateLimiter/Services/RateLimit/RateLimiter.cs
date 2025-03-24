using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace RateLimiterProgram.Services
{
    /// <summary>
    /// A rate limiter that enforces multiple rate limits using a sliding window approach.
    /// It ensures thread safety and delays execution if limits are exceeded.
    /// </summary>
    /// <typeparam name="TArg">The type of argument the action receives.</typeparam>
    public class RateLimiter<TArg> : IRateLimiter<TArg>
    {
        private readonly Func<TArg, Task> _action;
        private readonly RateLimit[] _limits;
        private readonly ITimeProvider _timeProvider;
        private readonly ConcurrentDictionary<TimeSpan, ConcurrentQueue<DateTime>> _requestLogs;
        private readonly ILogger<RateLimiter<TArg>> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<RequestItem<TArg>> _pendingRequests = new ConcurrentQueue<RequestItem<TArg>>();
        private Task _processingTask = null;
        private readonly object _processingLock = new object();
        private bool _isProcessing = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimiter{TArg}"/> class.
        /// </summary>
        /// <param name="action">The action to be executed with rate limiting.</param>
        /// <param name="limits">An array of rate limits defining allowed request rates.</param>
        /// <param name="timeProvider">A time provider for retrieving the current UTC time.</param>
        /// <param name="logger">A logger instance for logging rate limiting activities.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public RateLimiter(Func<TArg, Task> action, RateLimit[] limits, ITimeProvider timeProvider, ILogger<RateLimiter<TArg>> logger)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _requestLogs = new ConcurrentDictionary<TimeSpan, ConcurrentQueue<DateTime>>();
            foreach (var limit in _limits)
            {
                _requestLogs[limit.TimeWindow] = new ConcurrentQueue<DateTime>();
            }
        }

        /// <summary>
        /// Executes the provided action while ensuring that all rate limits are honored.
        /// Delays execution if necessary.
        /// </summary>
        /// <param name="argument">The argument to be passed to the action.</param>
        public async Task Perform(TArg argument)
        {
            var requestItem = new RequestItem<TArg>(argument);
            _pendingRequests.Enqueue(requestItem);
            EnsureProcessingTaskStarted();
            await requestItem.CompletionSource.Task;
        }

        /// <summary>
        /// Ensures a background task is running to process the queue of requests.
        /// </summary>
        private void EnsureProcessingTaskStarted()
        {
            lock (_processingLock)
            {
                if (!_isProcessing)
                {
                    _isProcessing = true;
                    _processingTask = Task.Run(ProcessQueueAsync);
                }
            }
        }

        /// <summary>
        /// Continuously processes the queue of pending requests according to rate limits.
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            try
            {
                while (!_pendingRequests.IsEmpty)
                {
                    await ProcessNextRequestAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in request processing task");
            }
            finally
            {
                HandleProcessingCompletion();
            }
        }

        /// <summary>
        /// Processes the next request in the queue if one is available.
        /// </summary>
        private async Task ProcessNextRequestAsync()
        {
            await WaitForRateLimitAsync();

            if (_pendingRequests.TryDequeue(out var requestItem))
            {
                await ExecuteRequestAsync(requestItem);
            }
        }

        /// <summary>
        /// Executes a single request and signals its completion.
        /// </summary>
        /// <param name="requestItem">The request item to execute.</param>
        private async Task ExecuteRequestAsync(RequestItem<TArg> requestItem)
        {
            try
            {
                _logger.LogInformation("Executing rate-limited request");
                await _action(requestItem.Argument);
                requestItem.CompletionSource.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rate-limited action");
                requestItem.CompletionSource.TrySetException(ex);
            }
        }

        /// <summary>
        /// Handles the completion of the processing task and restarts if needed.
        /// </summary>
        private void HandleProcessingCompletion()
        {
            lock (_processingLock)
            {
                _isProcessing = false;

                if (!_pendingRequests.IsEmpty)
                {
                    _isProcessing = true;
                    _processingTask = Task.Run(ProcessQueueAsync);
                }
            }
        }

        /// <summary>
        /// Ensures that execution does not exceed the defined rate limits.
        /// If limits are exceeded, it delays execution until permitted.
        /// </summary>
        private async Task WaitForRateLimitAsync()
        {
            DateTime now;
            while (true)
            {
                await _semaphore.WaitAsync();
                try
                {
                    now = _timeProvider.UtcNow;
                    CleanupAllExpiredEntries(now);

                    if (CanExecuteRequest(now))
                    {
                        RecordRequestExecution(now);
                        return;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                TimeSpan waitTime = GetEarliestExpirationWaitTime(now);
                await Task.Delay(waitTime);
            }
        }

        /// <summary>
        /// Removes expired request timestamps from the queue based on the time window.
        /// </summary>
        /// <param name="queue">The queue containing request timestamps.</param>
        /// <param name="window">The time window for expiration.</param>
        /// <param name="now">The current timestamp.</param>
        private void CleanupExpiredEntries(ConcurrentQueue<DateTime> queue, TimeSpan window, DateTime now)
        {
            DateTime cutoff = now - window;

            while (queue.TryPeek(out DateTime oldest) && oldest <= cutoff)
            {
                queue.TryDequeue(out _);
                _logger.LogDebug("Removed expired request timestamp: {Oldest}", oldest);
            }
        }

        /// <summary>
        /// Removes expired request entries from all time window queues.
        /// </summary>
        /// <param name="now">The current UTC timestamp.</param>
        private void CleanupAllExpiredEntries(DateTime now)
        {
            foreach (var limit in _limits)
            {
                var queue = _requestLogs[limit.TimeWindow];
                CleanupExpiredEntries(queue, limit.TimeWindow, now);
            }
        }

        /// <summary>
        /// Checks if a new request can be executed without exceeding rate limits.
        /// </summary>
        /// <param name="now">The current UTC timestamp.</param>
        /// <returns>True if the request can be executed, false otherwise.</returns>
        private bool CanExecuteRequest(DateTime now)
        {
            foreach (var limit in _limits)
            {
                var queue = _requestLogs[limit.TimeWindow];
                if (queue.Count >= limit.MaxRequest)
                {
                    _logger.LogWarning("Rate limit exceeded. Waiting before executing the action.");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Records the execution of a request in all time window queues.
        /// </summary>
        /// <param name="now">The current UTC timestamp to record.</param>
        private void RecordRequestExecution(DateTime now)
        {
            foreach (var limit in _limits)
            {
                _requestLogs[limit.TimeWindow].Enqueue(now);
            }
        }

        /// <summary>
        /// Calculates the exact time until the earliest request expires.
        /// </summary>
        /// <param name="now">The current UTC timestamp to record.</param>
        /// <returns></returns>
        private TimeSpan GetEarliestExpirationWaitTime(DateTime now)
        {
            TimeSpan minWaitTime = TimeSpan.FromMilliseconds(100); // Default wait time

            foreach (var limit in _limits)
            {
                if (_requestLogs[limit.TimeWindow].TryPeek(out DateTime oldest))
                {
                    TimeSpan remainingTime = (oldest + limit.TimeWindow) - now;
                    if (remainingTime > TimeSpan.Zero && remainingTime < minWaitTime)
                    {
                        minWaitTime = remainingTime;
                    }
                }
            }

            return minWaitTime;
        }
    }
}