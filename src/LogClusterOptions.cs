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

    /// <summary>
    /// Parses command-line arguments into a mining configuration.
    /// </summary>
    /// <param name="args">The command-line arguments to parse.</param>
    /// <returns>The parsed options, including <see cref="Error" /> when parsing fails.</returns>
    public static LogClusterOptions Parse(string[] args)
    {
        var options = new LogClusterOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help": return options with { ShowHelp = true };
                case "-m" or "--message":
                    if (++i >= args.Length)
                    {
                        return options with { Error = $"{args[i - 1]} requires a value" };
                    }

                    options = options with { Message = args[i] };
                    break;

                case "-s" or "--min-support":
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var minSupport) || minSupport < 1)
                    {
                        return options with { Error = "minimum support must be a positive integer" };
                    }

                    options = options with { MinSupport = minSupport };
                    break;

                case "-n" or "--max-candidates":
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var maxCandidates) || maxCandidates < 1)
                    {
                        return options with { Error = "maximum candidates must be a positive integer" };
                    }

                    options = options with { MaxCandidates = maxCandidates };
                    break;

                case "--max-samples":
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var maxSamples) || maxSamples < 1)
                    {
                        return options with { Error = "maximum samples must be a positive integer" };
                    }

                    options = options with { MaxSamplesPerGap = maxSamples };
                    break;

                case "--max-records":
                    if (++i >= args.Length || !long.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var maxRecords) || maxRecords < 1)
                    {
                        return options with { Error = "maximum records must be a positive integer" };
                    }

                    options = options with { MaxRecords = maxRecords };
                    break;

                case "--max-input-bytes":
                    if (++i >= args.Length || !long.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var maxInputBytes) || maxInputBytes < 1)
                    {
                        return options with { Error = "maximum input bytes must be a positive integer" };
                    }

                    options = options with { MaxInputBytes = maxInputBytes };
                    break;

                case "--json": options = options with { Json = true }; break;
                case "-v" or "--verbose": options = options with { Verbose = true }; break;
                case "--keep-empty": options = options with { SkipEmpty = false }; break;
                case "--outliers": options = options with { ShowOutliers = true }; break;

                case "--max-outlier-samples":
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out var maxOutlierSamples) || maxOutlierSamples < 1)
                    {
                        return options with { Error = "maximum outlier samples must be a positive integer" };
                    }

                    options = options with { MaxOutlierSamples = maxOutlierSamples };
                    break;

                case "--materialize": options = options with { ForceMaterialize = true }; break;
                case "--stream": options = options with { ForceMaterialize = false }; break;

                case "--weight-support":
                    if (++i >= args.Length || !double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var weightSupport) || weightSupport < 0)
                    {
                        return options with { Error = "support weight must be a non-negative number" };
                    }

                    options = options with { WeightSupport = weightSupport };
                    break;

                case "--weight-anchor":
                    if (++i >= args.Length || !double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var weightAnchor) || weightAnchor < 0)
                    {
                        return options with { Error = "anchor weight must be a non-negative number" };
                    }

                    options = options with { WeightAnchor = weightAnchor };
                    break;

                case "--weight-gaps":
                    if (++i >= args.Length || !double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var weightGaps) || weightGaps < 0)
                    {
                        return options with { Error = "gap consistency weight must be a non-negative number" };
                    }

                    options = options with { WeightGapConsistency = weightGaps };
                    break;

                case "--weight-specificity":
                    if (++i >= args.Length || !double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var weightSpecificity) || weightSpecificity < 0)
                    {
                        return options with { Error = "specificity weight must be a non-negative number" };
                    }

                    options = options with { WeightSpecificity = weightSpecificity };
                    break;

                case "--wweight-threshold":
                    if (++i >= args.Length || !double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var wweightThreshold) || wweightThreshold < 0)
                    {
                        return options with { Error = "word-weight threshold must be a non-negative number" };
                    }

                    options = options with { WordWeightThreshold = wweightThreshold };
                    break;

                default:
                    if (args[i].StartsWith('-'))
                    {
                        return options with { Error = $"unknown option: {args[i]}" };
                    }

                    options.Inputs.Add(args[i]);
                    break;
            }
        }
        return options;
    }

    /// <summary>
    /// Writes command-line usage text to standard error.
    /// </summary>
    public static void PrintUsage() => Console.Error.WriteLine("""
        usage: logcluster [options] [file-or-directory ...]

        Discovers recurring log message structures and suggests candidate liblognorm rules.
        Without file arguments, messages are read one per line from stdin.

          -s, --min-support <n>     minimum records that must contain a word or candidate (default: 2)
          -n, --max-candidates <n>  maximum candidates to print (default: 50)
              --max-samples <n>     bounded samples to retain per variable gap (default: 8)
              --max-records <n>     abort if input exceeds this many records (default: 5000000)
              --max-input-bytes <n> abort if input exceeds this many bytes (default: 2147483648)
              --materialize         force loading all records into memory (skip the streaming heuristic)
              --stream              force the re-read-from-disk streaming strategy
              --weight-support <n>     score weight for support strength (default: 0.35)
              --weight-anchor <n>      score weight for anchor quality (default: 0.30)
              --weight-gaps <n>        score weight for gap consistency (default: 0.20)
              --weight-specificity <n> score weight for pattern specificity (default: 0.15)
              --wweight-threshold <n>  merge single-anchor variants when distinct values <=
                                       threshold * combined support (default: 0.5)
              --outliers               report lines that matched no surviving candidate
              --max-outlier-samples <n> bounded outlier lines to print (default: 20)
          -m, --message <text>      mine one message supplied on the command line
              --json                emit JSON instead of text
          -v, --verbose             print gap samples and parser confidence
              --keep-empty          include empty input lines
          -h, --help                show this help
        """);
}
