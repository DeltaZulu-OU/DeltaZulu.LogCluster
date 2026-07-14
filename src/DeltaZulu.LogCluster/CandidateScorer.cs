namespace DeltaZulu.LogCluster;

internal static class CandidateScorer
{
    /// <summary>
    /// Weights default to 0.35/0.30/0.20/0.15 (support/anchor/gaps/specificity), a fixed split
    /// with no published derivation; --weight-support/--weight-anchor/--weight-gaps/
    /// --weight-specificity let callers retune the mix for log types dissimilar to whatever
    /// informed those defaults, rather than silently trusting an unstated tuning.
    /// </summary>
    /// <param name="support">The number of records represented by the candidate.</param>
    /// <param name="recordCount">The total number of records processed.</param>
    /// <param name="anchorCount">The number of fixed anchor tokens in the candidate.</param>
    /// <param name="gaps">The rendered gap statistics for the candidate.</param>
    /// <param name="weightSupport">The multiplier for the support-strength component.</param>
    /// <param name="weightAnchor">The multiplier for the anchor-quality component.</param>
    /// <param name="weightGapConsistency">The multiplier for the gap-consistency component.</param>
    /// <param name="weightSpecificity">The multiplier for the pattern-specificity component.</param>
    /// <returns>The total and component scores for the candidate.</returns>
    public static CandidateScore Score(int support, int recordCount, int anchorCount, IReadOnlyList<GapOutput> gaps, double weightSupport, double weightAnchor, double weightGapConsistency, double weightSpecificity)
    {
        var supportStrength = Math.Min(100, 100.0 * Math.Log(1 + support) / Math.Log(1 + recordCount));
        var anchorQuality = Math.Min(100, anchorCount * 20.0);
        var variableGaps = gaps.Where(g => g.MaxWords > 0).ToArray();
        var gapConsistency = variableGaps.Length == 0 ? 100 : variableGaps.Average(g => g.MinWords == g.MaxWords ? 100 : 60 + (40.0 * g.MinWords / Math.Max(1, g.MaxWords)));
        var specificity = 100.0 * anchorCount / Math.Max(1, anchorCount + variableGaps.Length);
        var total = (supportStrength * weightSupport) + (anchorQuality * weightAnchor) + (gapConsistency * weightGapConsistency) + (specificity * weightSpecificity);
        return new CandidateScore(total, supportStrength, anchorQuality, gapConsistency, specificity);
    }
}
