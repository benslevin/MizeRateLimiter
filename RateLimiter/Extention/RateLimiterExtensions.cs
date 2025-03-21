using Microsoft.Extensions.DependencyInjection;
using RateLimiter.Factory.RateLimiterFactory;

namespace RateLimiterProgram.Services
{
    public static class RateLimiterExtensions
    {
        /// <summary>
        /// Adds rate limiting services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRateLimiterServices(this IServiceCollection services)
        {
            services.AddSingleton<ITimeProvider, RateLimiterTimeProvider>();
            services.AddSingleton<IRateLimiterFactory, RateLimiterFactory>();

            return services;
        }
    }
}
