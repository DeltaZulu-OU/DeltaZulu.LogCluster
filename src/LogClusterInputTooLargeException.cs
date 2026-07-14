namespace DeltaZulu.LogCluster;

public sealed class LogClusterInputTooLargeException : Exception
{
    public LogClusterInputTooLargeException(string message) : base(message)
    {
    }
}
