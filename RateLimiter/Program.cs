using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RateLimiterProgram.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ITimeProvider, RateLimiterTimeProvider>();
builder.Services.AddSingleton<ILoggerFactory, LoggerFactory>();
builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

builder.Services.AddSingleton<RateLimiter<string>>(sp =>
{
    var timeProvider = sp.GetRequiredService<ITimeProvider>();
    var logger = sp.GetRequiredService<ILogger<RateLimiter<string>>>();

    var rateLimits = new []
    {
        new RateLimit(10, TimeSpan.FromSeconds(1)),
        new RateLimit(100, TimeSpan.FromMinutes(1)),
        new RateLimit(1000, TimeSpan.FromDays(1))
    };

    return new RateLimiter<string>(
        async msg => Console.WriteLine($"{DateTime.UtcNow} : {msg}"),
        rateLimits,
        timeProvider,
        logger
    );
});

var app = builder.Build();
await app.RunAsync();

// Just for testing the rate limit functionallity
var rateLimiter = app.Services.GetRequiredService<RateLimiter<string>>();

var tasks = Enumerable.Range(1, 20)
    .Select(i => rateLimiter.Perform($"Request {i}"))
    .ToArray();

await Task.WhenAll(tasks);

await app.RunAsync();