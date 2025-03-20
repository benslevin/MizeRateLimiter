namespace RateLimiterProgram.Services
{
    public interface IRateLimiter<TArg>
    {
        Task Perform(TArg argument);
    }
}
