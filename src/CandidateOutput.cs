namespace DeltaZulu.LogCluster;

public sealed record CandidateOutput(int Support, double Specificity, string LogClusterPattern, string LiblognormRule, bool IsExecutableRule, IReadOnlyList<string> RuleWarnings, IReadOnlyList<GapOutput> Gaps, CandidateScore Score);
