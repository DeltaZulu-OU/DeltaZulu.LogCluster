namespace DeltaZulu.LogCluster;

/// <summary>
/// Holds the weighted total score for a candidate and the normalized component scores that contributed to it.
/// </summary>
/// <param name="Total">The final weighted score used to rank candidates.</param>
/// <param name="Support">The normalized support-strength component.</param>
/// <param name="AnchorQuality">The normalized anchor-count quality component.</param>
/// <param name="GapConsistency">The normalized consistency component for variable gaps.</param>
/// <param name="PatternSpecificity">The normalized fixed-anchor specificity component.</param>
public sealed record CandidateScore(double Total, double Support, double AnchorQuality, double GapConsistency, double PatternSpecificity);
