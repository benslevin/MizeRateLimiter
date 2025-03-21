using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RateLimiterProgram.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTimeProviderServices(this IServiceCollection services)
        {
            services.AddSingleton<ITimeProvider, RateLimiterTimeProvider>();
            return services;
        }

        public static IServiceCollection AddLoggerServices(this IServiceCollection services)
        {
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            return services;
        }
    }
}
