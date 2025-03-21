namespace RateLimiterProgram.Services
{
    /// <summary>
    /// Interface for a rate limiter that controls the execution rate of a given action.
    /// </summary>
    /// <typeparam name="TArg">The type of argument the action receives.</typeparam>
    public interface IRateLimiter<TArg>
    {
        Task Perform(TArg argument);
    }
}
