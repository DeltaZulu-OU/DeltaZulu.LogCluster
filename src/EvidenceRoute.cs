namespace DeltaZulu.LogCluster;

/// <summary>
/// Which anchor occurrences (0-indexed in encounter order) a record matching this route's key
/// should fold into the adjacent gap instead of treating as a real split point for whichever
/// PatternCandidate it's been merged into. Leading/Trailing come from MergeShiftedCandidates
/// (edge extensions); AbsorbedPositions comes from MergeLowDiversityVariants (one internal
/// position wildcarded). The two are never combined on the same route (see
/// MergeLowDiversityVariants' scoping comment).
/// </summary>
/// <param name="Leading"></param>
/// <param name="Trailing"></param>
/// <param name="AbsorbedPositions"></param>
internal readonly record struct EvidenceRoute(int Leading, int Trailing, HashSet<int>? AbsorbedPositions);
