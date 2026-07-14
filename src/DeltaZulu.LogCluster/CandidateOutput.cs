namespace DeltaZulu.LogCluster;

/// <summary>
/// Describes a mined log pattern together with its rendered output forms, gap evidence, and score.
/// </summary>
/// <param name="Support">The number of input records represented by the candidate.</param>
/// <param name="Specificity">The ratio of fixed anchor tokens to fixed anchors plus variable gaps.</param>
/// <param name="LogClusterPattern">The LogCluster-style pattern containing literals and wildcard gap ranges.</param>
/// <param name="LiblognormRule">The liblognorm rule text rendered from the candidate.</param>
/// <param name="IsExecutableRule">Whether <paramref name="LiblognormRule" /> contains only executable liblognorm parsers.</param>
/// <param name="RuleWarnings">Warnings that explain any structural limitations in the rendered rule.</param>
/// <param name="Gaps">Statistics for the variable gaps around and between the fixed anchors.</param>
/// <param name="Score">The weighted score assigned to the candidate.</param>
public sealed record CandidateOutput(int Support, double Specificity, string LogClusterPattern, string LiblognormRule, bool IsExecutableRule, IReadOnlyList<string> RuleWarnings, IReadOnlyList<GapOutput> Gaps, CandidateScore Score);
