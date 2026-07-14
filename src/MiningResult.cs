namespace DeltaZulu.LogCluster;

public sealed record MiningResult(int RecordCount, IReadOnlyList<CandidateOutput> Candidates, string Strategy, int OutlierCount, IReadOnlyList<string> OutlierSamples);
