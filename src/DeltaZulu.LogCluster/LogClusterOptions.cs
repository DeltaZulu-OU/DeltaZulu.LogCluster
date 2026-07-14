using System.Globalization;

namespace DeltaZulu.LogCluster;

/// <summary>
/// Stores command-line and mining configuration for the LogCluster miner.
/// </summary>
public sealed record LogClusterOptions
{
    /// <summary>Gets the minimum number of records required for a word or candidate to be retained.</summary>
    public int MinSupport { get; init; } = 2;
    /// <summary>Gets the maximum number of ranked candidates to emit.</summary>
    public int MaxCandidates { get; init; } = 50;
    /// <summary>Gets the maximum number of distinct examples retained for each variable gap.</summary>
    public int MaxSamplesPerGap { get; init; } = 8;
    /// <summary>Gets the maximum number of records accepted before mining aborts.</summary>
    public long MaxRecords { get; init; } = 5_000_000;
    /// <summary>Gets the maximum total input size, in message characters, accepted before mining aborts.</summary>
    public long MaxInputBytes { get; init; } = 2_147_483_648;
    /// <summary>Gets an override for the mining strategy: true to materialize, false to stream, or null to use the heuristic.</summary>
    public bool? ForceMaterialize { get; init; }
    /// <summary>Gets the weight applied to the support-strength score component.</summary>
    public double WeightSupport { get; init; } = 0.35;
    /// <summary>Gets the weight applied to the anchor-quality score component.</summary>
    public double WeightAnchor { get; init; } = 0.30;
    /// <summary>Gets the weight applied to the gap-consistency score component.</summary>
    public double WeightGapConsistency { get; init; } = 0.20;
    /// <summary>Gets the weight applied to the pattern-specificity score component.</summary>
    public double WeightSpecificity { get; init; } = 0.15;

    /// <summary>
    /// Default of 0.5 merges a group of candidates differing at exactly one anchor position when
    /// that position's distinct values recur at least twice each on average relative to the
    /// group's combined support (distinctValues &lt;= 0.5 * combinedSupport) -- e.g. a handful of
    /// recurring source IPs, not a firehose of one-off values.
    /// </summary>
    public double WordWeightThreshold { get; init; } = 0.5;
    /// <summary>Gets a value indicating whether unmatched input records should be reported as outliers.</summary>
    public bool ShowOutliers { get; init; }
    /// <summary>Gets the maximum number of outlier sample lines to retain.</summary>
    public int MaxOutlierSamples { get; init; } = 20;
    /// <summary>Gets a value indicating whether results should be emitted as JSON.</summary>
    public bool Json { get; init; }
    /// <summary>Gets a value indicating whether verbose diagnostic details should be printed.</summary>
    public bool Verbose { get; init; }
    /// <summary>Gets a value indicating whether empty input lines are ignored.</summary>
    public bool SkipEmpty { get; init; } = true;
    /// <summary>Gets a value indicating whether usage text should be shown.</summary>
    public bool ShowHelp { get; init; }
    /// <summary>Gets a command-line parsing error message, or null when parsing succeeded.</summary>
    public string? Error { get; init; }
    /// <summary>Gets the single message supplied on the command line, or null when inputs are read from files or standard input.</summary>
    public string? Message { get; init; }
    /// <summary>Gets the file or directory inputs supplied on the command line.</summary>
    public List<string> Inputs { get; } = [];
}
