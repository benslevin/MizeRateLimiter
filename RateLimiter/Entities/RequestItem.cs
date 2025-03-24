namespace RateLimiterProgram.Services
{
    /// <summary>
    /// Represents a request to be processed with its associated completion source
    /// </summary>
    public class RequestItem<TArg>
    {
        public TArg Argument { get; }
        public TaskCompletionSource<bool> CompletionSource { get; }

        public RequestItem(TArg argument)
        {
            Argument = argument;
            CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
