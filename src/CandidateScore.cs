namespace DeltaZulu.LogCluster;

public sealed record CandidateScore(double Total, double Support, double AnchorQuality, double GapConsistency, double PatternSpecificity);
