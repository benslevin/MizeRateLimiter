using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RateLimiterProgram.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ITimeProvider, RateLimiterTimeProvider>();
builder.Services.AddSingleton<RateLimiter<string>>(sp =>
{
    var timeProvider = sp.GetRequiredService<ITimeProvider>();

    var rateLimits = new List<RateLimit>
    {
        new RateLimit(10, TimeSpan.FromSeconds(1)),
        new RateLimit(100, TimeSpan.FromMinutes(1)),
        new RateLimit(1000, TimeSpan.FromDays(1))
    };

    return new RateLimiter<string>(
        async msg => Console.WriteLine($"{DateTime.UtcNow} : {msg}"),
        rateLimits,
        timeProvider
    );
});

var app = builder.Build();
var rateLimiter = app.Services.GetRequiredService<RateLimiter<string>>();

var tasks = Enumerable.Range(1, 20)
    .Select(i => rateLimiter.Perform($"Request {i}"))
    .ToArray();

await Task.WhenAll(tasks);

await app.RunAsync();