namespace DeltaZulu.LogCluster;

public sealed record LogRecord(long SequenceNumber, string Message, string? Source);
