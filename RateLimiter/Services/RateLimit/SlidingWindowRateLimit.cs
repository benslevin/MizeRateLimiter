namespace RateLimiter.Services
{
    public class SlidingWindowRateLimit : IRateLimitStrategy
    {
        private readonly RateLimit _rateLimit;
        private readonly ITimeProvider _timeProvider;
        private readonly Queue<DateTime> _requestTimestamps = new();
        private readonly SemaphoreSlim _semaphore = new(1,1);

        public SlidingWindowRateLimit(RateLimit rateLimit, ITimeProvider timeProvider)
        {
            _rateLimit = rateLimit;
            _timeProvider = timeProvider;
        }

        public async Task WaitForPermissionAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                DateTime now = _timeProvider.UtcNow;
                DateTime windowStart = now - _rateLimit.TimeWindow;

                while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
                {
                    _requestTimestamps.Dequeue();
                }
                
                while(_requestTimestamps.Count >= _rateLimit.MaxRequest)
                {
                    TimeSpan waitTime = _requestTimestamps.Peek() - windowStart;
                    await Task.Delay(waitTime);
                    now = _timeProvider.UtcNow;
                    windowStart = now - _rateLimit.TimeWindow;

                    while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
                    {
                        _requestTimestamps.Dequeue();
                    }
                }

                _requestTimestamps.Enqueue(now);
            }
            catch (Exception ex) 
            { 

            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
