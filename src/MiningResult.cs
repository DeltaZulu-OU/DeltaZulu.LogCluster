namespace DeltaZulu.LogCluster;

/// <summary>
/// Contains the complete result of mining a set of log records.
/// </summary>
/// <param name="RecordCount">The number of input records processed.</param>
/// <param name="Candidates">The ranked candidate patterns that survived filtering.</param>
/// <param name="Strategy">The mining strategy used for the input, such as <c>materialize</c> or <c>stream</c>.</param>
/// <param name="OutlierCount">The number of records that matched no surviving candidate.</param>
/// <param name="OutlierSamples">A bounded set of sample outlier records.</param>
public sealed record MiningResult(int RecordCount, IReadOnlyList<CandidateOutput> Candidates, string Strategy, int OutlierCount, IReadOnlyList<string> OutlierSamples);
