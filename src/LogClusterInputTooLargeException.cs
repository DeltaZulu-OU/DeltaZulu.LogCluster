namespace DeltaZulu.LogCluster;

/// <summary>
/// The exception thrown when log input exceeds the configured record or byte safety limits.
/// </summary>
public sealed class LogClusterInputTooLargeException : Exception
{
    /// <summary>
    /// Initializes a new exception with a message describing the exceeded input limit.
    /// </summary>
    /// <param name="message">The error message that explains which limit was exceeded.</param>
    public LogClusterInputTooLargeException(string message) : base(message)
    {
    }
}
