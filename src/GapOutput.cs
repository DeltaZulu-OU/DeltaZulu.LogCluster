namespace DeltaZulu.LogCluster;

public sealed record GapOutput(int MinWords, int MaxWords, int Observations, IReadOnlyList<string> Samples, string? SuggestedParser, double ParserConfidence);
