using Microsoft.Extensions.Hosting;
using RateLimiterProgram.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTimeProviderServices()
    .AddLoggerServices();

var app = builder.Build();
await app.RunAsync();
