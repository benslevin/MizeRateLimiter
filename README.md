# Rate Limiter Implementation

## Overview

This project implements a thread-safe, multi-limit Rate Limiter in C# that allows controlled execution of actions according to specified rate limits. The Rate Limiter uses a sliding window approach to ensure smooth and accurate rate control.

## Features

- Multiple simultaneous rate limits (e.g., 10/second, 100/minute, 1000/day)
- Thread-safe implementation that handles concurrent requests
- Sliding window approach for accurate rate limiting
- Automatic queuing of requests that exceed rate limits
- Customizable time provider for testing

## Implementation Details

### Core Components

1. **RateLimiter\<TArg\>**: The main implementation that manages request tracking and enforces rate limits.
2. **IRateLimiter\<TArg\>**: Interface defining the contract for rate limiters.
3. **RateLimit**: Record that defines a rate limit with maximum requests and time window.
4. **ITimeProvider**: Interface for time providers, allowing for dependency injection and testability.
5. **RateLimiterTimeProvider**: Default implementation of ITimeProvider that uses system time.

### How It Works

The Rate Limiter tracks request timestamps in concurrent queues, one for each rate limit. When a new request comes in:

1. It checks if executing the request would violate any rate limit
2. If all limits are satisfied, it records the request time and executes the action
3. If any limit would be exceeded, it delays and retries until execution is possible
4. Expired timestamps are automatically cleaned from the tracking queues

### Sliding Window Approach

The implementation uses a sliding window approach, which means:

- It considers all requests within the last X time units (e.g., last 60 seconds for a "per minute" limit)
- Requests gradually expire as time moves forward
- This prevents request spikes that can occur with absolute time windows (e.g., at midnight for daily limits)

## Usage Example

```csharp
// Create rate limits: 10 per second, 100 per minute, 1000 per day
var limits = new[]
{
    new RateLimit(10, TimeSpan.FromSeconds(1)),
    new RateLimit(100, TimeSpan.FromMinutes(1)),
    new RateLimit(1000, TimeSpan.FromDays(1))
};

// Create a rate limiter for string arguments
var rateLimiter = new RateLimiter<string>(
    async message => Console.WriteLine($"{DateTime.UtcNow}: {message}"),
    limits,
    new RateLimiterTimeProvider()
);

// Execute requests
await rateLimiter.Perform("Hello, world!");

// Execute multiple requests in parallel
var tasks = Enumerable.Range(1, 20)
    .Select(i => rateLimiter.Perform($"Request {i}"))
    .ToArray();

await Task.WhenAll(tasks);
```

## Testing

The Rate Limiter is designed to be easily testable. The included unit tests verify:

1. Basic rate limiting functionality with single limits
2. Multiple rate limits working together
3. Sliding window behavior
4. Thread safety with concurrent requests
5. Proper cleanup of expired entries

The tests use a mock time provider to control time advancement, allowing for fast and deterministic testing.

## Rate Limiting Approaches

The implementation uses a sliding window approach which offers these advantages:

- More accurate and consistent rate limiting
- Prevents request spikes at time boundaries
- Provides better protection for downstream systems

This approach is preferred over absolute windows (which reset at fixed intervals) for most API rate limiting scenarios.
