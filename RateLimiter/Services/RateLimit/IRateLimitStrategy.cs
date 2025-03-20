namespace RateLimiterProgram.Services
{
    public interface IRateLimitStrategy
    {
        Task WaitForPermissionAsync(); 
    }
}
