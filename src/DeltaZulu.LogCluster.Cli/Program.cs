using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeltaZulu.Suggester;

namespace DeltaZulu.LogCluster.Cli
{
    internal static class Program
    {
        /// <summary>
        /// Serializes mining output for --json. The project builds with PublishAot, which disables
        /// System.Text.Json's reflection-based serializer at the runtimeconfig level (this affects
        /// plain `dotnet run`, not only AOT-published binaries), so a source-generated
        /// JsonSerializerContext is required rather than a plain JsonSerializerOptions.
        /// </summary>
        /// <param name="result">The mining result to serialize.</param>
        /// <param name="includeOutliers">Whether to wrap candidates with outlier count and samples.</param>
        /// <returns>The serialized JSON text.</returns>
        internal static string SerializeJson(MiningResult result, bool includeOutliers) => includeOutliers
            ? JsonSerializer.Serialize(new OutlierMiningOutput(result.Candidates, result.OutlierCount, result.OutlierSamples), LogClusterJsonContext.Default.OutlierMiningOutput)
            : JsonSerializer.Serialize(result.Candidates, LogClusterJsonContext.Default.IReadOnlyListCandidateOutput);

        private static int Main(string[] args)
        {
            var options = Parse(args) with { GapSuggestionEngine = LiblognormSuggestionEngine.Instance };
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }
            if (options.Error is not null)
            {
                Console.Error.WriteLine($"error: {options.Error}");
                PrintUsage();
                return 1;
            }

            string? spooledStdinPath = null;
            if (options.Message is null && options.Inputs.Count == 0)
            {
                spooledStdinPath = SpoolStdin();
                options.Inputs.Add(spooledStdinPath);
            }

            try
            {
                MiningResult result;
                try
                {
                    var estimatedBytes = EstimateInputBytes(options);
                    result = new LogClusterMiner(options).Mine(() => ReadRecords(options), estimatedBytes);
                }
                catch (LogClusterInputTooLargeException ex)
                {
                    Console.Error.WriteLine($"error: {ex.Message}");
                    return 1;
                }
                if (result.RecordCount == 0)
                {
                    Console.Error.WriteLine("error: no input messages were provided");
                    return 1;
                }
                if (options.Verbose)
                {
                    Console.Error.WriteLine($"info: mining strategy: {result.Strategy}");
                }
                if (options.Json)
                {
                    Console.WriteLine(SerializeJson(result, options.ShowOutliers));
                }
                else
                {
                    PrintText(result, options);
                }

                return 0;
            }
            finally
            {
                if (spooledStdinPath is not null)
                {
                    File.Delete(spooledStdinPath);
                }
            }

            static string SpoolStdin()
            {
                var path = Path.GetTempFileName();
                using var writer = new StreamWriter(path);
                string? line;
                while ((line = Console.In.ReadLine()) is not null)
                {
                    writer.WriteLine(line);
                }
                return path;
            }

            static long EstimateInputBytes(LogClusterOptions options)
            {
                if (options.Message is not null)
                {
                    return options.Message.Length;
                }

                long total = 0;
                foreach (var input in options.Inputs)
                {
                    if (Directory.Exists(input))
                    {
                        foreach (var file in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories))
                        {
                            total += new FileInfo(file).Length;
                        }
                    }
                    else if (File.Exists(input))
                    {
                        total += new FileInfo(input).Length;
                    }
                }
                return total;
            }

            static IEnumerable<LogRecord> ReadRecords(LogClusterOptions options)
            {
                var sequence = new SequenceCounter();
                if (options.Message is not null)
                {
                    yield return new LogRecord(sequence.Next(), options.Message, "argument");
                    yield break;
                }

                foreach (var input in options.Inputs)
                {
                    if (Directory.Exists(input))
                    {
                        foreach (var file in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                        {
                            foreach (var record in ReadFile(file, sequence, options.SkipEmpty))
                            {
                                yield return record;
                            }
                        }
                    }
                    else
                    {
                        foreach (var record in ReadFile(input, sequence, options.SkipEmpty))
                        {
                            yield return record;
                        }
                    }
                }
            }

            static IEnumerable<LogRecord> ReadFile(string path, SequenceCounter sequence, bool skipEmpty)
            {
                using var reader = File.OpenText(path);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (line.Length != 0 || !skipEmpty)
                    {
                        yield return new LogRecord(sequence.Next(), line, path);
                    }
                }
            }

            static void PrintText(MiningResult result, LogClusterOptions options)
            {
                Console.WriteLine($"LogCluster.NET candidates: {result.Candidates.Count} (records: {result.RecordCount}, minimum support: {options.MinSupport})");
                Console.WriteLine();

                foreach (var candidate in result.Candidates)
                {
                    Console.WriteLine($"Score {candidate.Score.Total:F1}  Support {candidate.Support}  Specificity {candidate.Specificity:F2}");
                    Console.WriteLine($"  LogCluster: {candidate.LogClusterPattern}");
                    Console.WriteLine($"  Rule:       {candidate.LiblognormRule}");
                    if (!candidate.IsExecutableRule)
                    {
                        Console.WriteLine("  Rule note:  structural sketch only; unresolved internal gaps make this non-executable as a liblognorm rule.");
                    }
                    Console.WriteLine($"  Score parts support={candidate.Score.Support:F1}, anchors={candidate.Score.AnchorQuality:F1}, gaps={candidate.Score.GapConsistency:F1}, specificity={candidate.Score.PatternSpecificity:F1}");
                    if (options.Verbose)
                    {
                        foreach (var warning in candidate.RuleWarnings)
                        {
                            Console.WriteLine($"  Warning: {warning}");
                        }
                        for (var i = 0; i < candidate.Gaps.Count; i++)
                        {
                            var gap = candidate.Gaps[i];
                            var parser = gap.SuggestedParser ?? options.GapSuggestionEngine.RestParser;
                            Console.WriteLine($"  Gap {i + 1}: words {gap.MinWords}-{gap.MaxWords}, observations {gap.Observations}, parser {parser} ({gap.ParserConfidence:P0})");
                            if (gap.Samples.Count > 0)
                            {
                                Console.WriteLine($"    samples: {string.Join(", ", gap.Samples)}");
                            }
                        }
                    }
                    Console.WriteLine();
                }

                if (options.ShowOutliers)
                {
                    Console.WriteLine($"Outliers: {result.OutlierCount} lines matched no surviving candidate");
                    foreach (var sample in result.OutlierSamples)
                    {
                        Console.WriteLine($"  {sample}");
                    }
                }
            }
        }


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

    internal sealed class SequenceCounter
    {
        private long _value;

        public long Next() => ++_value;
    }

    /// <summary>
    /// The --outliers JSON shape: candidates alongside the outlier count and bounded samples.
    /// </summary>
    /// <param name="Candidates">The ranked candidate patterns that survived filtering.</param>
    /// <param name="OutlierCount">The number of records that matched no surviving candidate.</param>
    /// <param name="OutlierSamples">A bounded set of sample outlier records.</param>
    internal sealed record OutlierMiningOutput(IReadOnlyList<CandidateOutput> Candidates, int OutlierCount, IReadOnlyList<string> OutlierSamples);

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(IReadOnlyList<CandidateOutput>))]
    [JsonSerializable(typeof(OutlierMiningOutput))]
    internal sealed partial class LogClusterJsonContext : JsonSerializerContext;
}
