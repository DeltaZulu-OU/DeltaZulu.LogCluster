namespace DeltaZulu.LogCluster;

/// <summary>
/// Summarizes the words observed in one variable gap of a mined candidate.
/// </summary>
/// <param name="MinWords">The fewest words observed in the gap.</param>
/// <param name="MaxWords">The most words observed in the gap.</param>
/// <param name="Observations">The number of records that contributed gap evidence.</param>
/// <param name="Samples">A bounded set of distinct sample values observed in the gap.</param>
/// <param name="SuggestedParser">The suggested liblognorm parser for the gap, or <see langword="null" /> when no parser was inferred.</param>
/// <param name="ParserConfidence">The fraction of observations that supported the suggested parser.</param>
public sealed record GapOutput(int MinWords, int MaxWords, int Observations, IReadOnlyList<string> Samples, string? SuggestedParser, double ParserConfidence);
