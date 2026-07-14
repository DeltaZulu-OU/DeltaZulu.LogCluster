namespace DeltaZulu.LogCluster;

/// <summary>
/// Represents one input log message and the metadata needed to identify it during mining.
/// </summary>
/// <param name="SequenceNumber">The monotonically increasing sequence number assigned to the record.</param>
/// <param name="Message">The raw log message text.</param>
/// <param name="Source">The source path or label for the message, or <see langword="null" /> when no source is known.</param>
public sealed record LogRecord(long SequenceNumber, string Message, string? Source);
