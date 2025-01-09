namespace MovieComparison.Core.Exceptions
{
    public class ProviderException(string message, Exception? innerException = null) : Exception(message, innerException)
    {
        public string? ProviderName { get; }
    }
}