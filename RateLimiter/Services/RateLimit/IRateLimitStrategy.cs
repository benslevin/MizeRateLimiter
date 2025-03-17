namespace RateLimiter.Services
{
    public interface IRateLimitStrategy
    {
        Task WaitForPermissionAsync(); 
    }
}
