using System.Text;

namespace DeltaZulu.LogCluster;

internal sealed class PatternCandidate
{
    private readonly List<GapStatistics> _gaps = [];
    private readonly List<SeparatorStats> _separators = [];
    private readonly int[] anchors;
    private long _lastSequence;

    public PatternCandidate(CandidateKey key, int[] anchors)
    {
        this.anchors = anchors;
        Key = key;
    }

    public int AnchorCount => anchors.Length;
    public bool KeepEvidence => _gaps.Count > 0;
    public CandidateKey Key { get; }
    public int Support { get; private set; }

    public void AbsorbSupport(PatternCandidate other) => Support += other.Support;

    /// <summary>
    /// The anchor sequence with one internal position wildcarded (see
    /// LogClusterMiner.MergeLowDiversityVariants), used to group candidates that differ only at
    /// that one position.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public int[] AnchorsWithout(int position)
    {
        var result = new int[anchors.Length - 1];
        Array.Copy(anchors, 0, result, 0, position);
        Array.Copy(anchors, position + 1, result, position, anchors.Length - position - 1);
        return result;
    }

    public void InitializeGaps(int maxSamples)
    {
        var gapCount = anchors.Length + 1;
        for (var i = 0; i < gapCount; i++)
        {
            _gaps.Add(new GapStatistics(maxSamples));
            _separators.Add(new SeparatorStats());
        }
    }

    /// <summary>
    /// separators is aligned with tokens: separators[i] is the whitespace immediately before
    /// tokens[i], and separators[^1] is the trailing whitespace after the last token.
    /// absorbedPositions (0-indexed in encounter order, before extraLeadingAnchors/
    /// extraTrailingAnchors are applied) marks additional individual anchor occurrences that
    /// should also fold into the adjacent gap, from MergeLowDiversityVariants.
    /// </summary>
    /// <param name="tokens"></param>
    /// <param name="separators"></param>
    /// <param name="frequentWords"></param>
    /// <param name="dictionary"></param>
    /// <param name="extraLeadingAnchors"></param>
    /// <param name="extraTrailingAnchors"></param>
    /// <param name="absorbedPositions"></param>
    public void ObserveGaps(ReadOnlySpan<int> tokens, ReadOnlySpan<string> separators, ReadOnlySpan<bool> frequentWords, TokenDictionary dictionary, int extraLeadingAnchors = 0, int extraTrailingAnchors = 0, HashSet<int>? absorbedPositions = null)
    {
        var gapIndex = 0;
        var anchorsSeen = 0;
        // Every absorbed occurrence (edge or internal) still counts toward how many anchor hits
        // this record is expected to produce, so the trailing-edge check below doesn't misfire
        // once an internal position has already been absorbed.
        var totalAnchorsInRecord = anchors.Length + extraLeadingAnchors + extraTrailingAnchors + (absorbedPositions?.Count ?? 0); var gapWords = new List<int>();
        // The separator right after whichever real anchor was last flushed -- i.e. the boundary
        // that opens the trailing gap. Defaults to the record's true trailing separator, which
        // is exactly the same value when the trailing gap turns out to be empty (nothing follows
        // the last anchor either way); only differs when the trailing gap has actual words,
        // where "after the last anchor" and "after the whole record" are different positions.
        var trailingSeparator = separators[^1];
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (frequentWords[token])
            {
                var position = anchorsSeen;
                anchorsSeen++;
                var isEdgeAbsorbed = position < extraLeadingAnchors || anchorsSeen > totalAnchorsInRecord - extraTrailingAnchors;
                var isPositionAbsorbed = absorbedPositions?.Contains(position) == true;
                if (isEdgeAbsorbed || isPositionAbsorbed)
                {
                    gapWords.Add(token);
                    continue;
                }

                _gaps[gapIndex].Observe(gapWords, dictionary);
                _separators[gapIndex].Observe(separators[i]);
                gapIndex++;
                trailingSeparator = separators[i + 1];
                gapWords.Clear();
            }
            else
            {
                gapWords.Add(token);
            }
        }
        _gaps[gapIndex].Observe(gapWords, dictionary);
        _separators[gapIndex].Observe(trailingSeparator);
    }

    public void ObserveSupport(long sequenceNumber)
    {
        if (_lastSequence == sequenceNumber)
        {
            return;
        }

        _lastSequence = sequenceNumber;
        Support++;
    }

    /// <summary>
    /// Anchors dropped from either edge of a positionally-shifted variant that was merged into
    /// this candidate (see LogClusterMiner.MergeShiftedCandidates) don't split a gap for this
    /// candidate's own anchor sequence; fold them into the adjacent boundary gap's word range
    /// instead of treating them as anchors.
    /// </summary>
    /// <param name="dropFirst"></param>
    /// <returns></returns>
    public CandidateKey ReducedKey(bool dropFirst) => new(dropFirst ? anchors.AsSpan(1) : anchors.AsSpan(0, anchors.Length - 1));

    public CandidateKey TemplateKey(int position) => new(AnchorsWithout(position));

    public CandidateOutput ToOutput(int recordCount, TokenDictionary dictionary, LogClusterOptions options)
    {
        var renderedGaps = _gaps.Select(g => g.ToOutput()).ToArray();
        var separators = _separators.Select(s => s.Modal()).ToArray();
        var score = CandidateScorer.Score(Support, recordCount, anchors.Length, renderedGaps, options.WeightSupport, options.WeightAnchor, options.WeightGapConsistency, options.WeightSpecificity);
        var specificity = anchors.Length / (double)Math.Max(1, anchors.Length + renderedGaps.Count(g => g.MaxWords > 0));
        return new CandidateOutput(
            Support,
            specificity,
            RenderLogCluster(anchors, renderedGaps, separators, dictionary),
            RenderRule(anchors, renderedGaps, separators, dictionary, out var isExecutableRule, out var ruleWarnings),
            isExecutableRule,
            ruleWarnings, renderedGaps,
            score);
    }

    private static void AddRuleGap(Action<string, int> append, int gapIndex, GapOutput gap, bool isTrailing, ref int field, List<string> warnings)
    {
        if (gap.MaxWords == 0)
        {
            return;
        }

        if (!isTrailing && (gap.MinWords == 0 || gap.MaxWords > 1 || string.IsNullOrEmpty(gap.SuggestedParser)))
        {
            append($"/* unresolved gap: {gap.MinWords}-{gap.MaxWords} words */", gapIndex); warnings.Add($"Internal gap {field} spans {gap.MinWords}-{gap.MaxWords} words and cannot be rendered as an executable liblognorm parser.");
            return;
        }

        var parser = gap.SuggestedParser ?? (gap.MaxWords == 1 ? LiblognormMotifs.Word : LiblognormMotifs.Rest);
        if (isTrailing && (gap.MinWords == 0 || gap.MaxWords > 1))
        {
            parser = LiblognormMotifs.Rest;
        }

        append($"%field{field++}:{parser}%", gapIndex);
    }

    private static string EscapeLiteral(string token) => token.Contains('%') || token.Contains(':')
        ? token.Replace("%", "%%", StringComparison.Ordinal).Replace(":", "\\x3a", StringComparison.Ordinal)
        : token;

    /// <summary>
    /// separators[i] is the modal separator observed immediately before anchor i (or, for the
    /// final entry, the modal trailing separator); reused as the join before whichever rendered
    /// part (gap placeholder or anchor literal) falls at that boundary.
    /// </summary>
    /// <param name="anchors"></param>
    /// <param name="gaps"></param>
    /// <param name="separators"></param>
    /// <param name="dictionary"></param>
    /// <returns></returns>
    private static string RenderLogCluster(int[] anchors, GapOutput[] gaps, string[] separators, TokenDictionary dictionary)
    {
        var builder = new StringBuilder();
        void Append(string text, int gapIndex)
        {
            if (builder.Length > 0)
            {
                builder.Append(separators[gapIndex]);
            }
            builder.Append(text);
        }

        for (var i = 0; i < anchors.Length; i++)
        {
            if (gaps[i].MaxWords > 0)
            {
                Append($"*{{{gaps[i].MinWords},{gaps[i].MaxWords}}}", i);
            }
            Append(dictionary[anchors[i]], i);
        }

        if (gaps[^1].MaxWords > 0)
        {
            Append($"*{{{gaps[^1].MinWords},{gaps[^1].MaxWords}}}", anchors.Length);
        }
        return builder.ToString();
    }

    private static string RenderRule(int[] anchors, GapOutput[] gaps, string[] separators, TokenDictionary dictionary, out bool isExecutable, out IReadOnlyList<string> warnings)
    {
        var builder = new StringBuilder();
        void Append(string text, int gapIndex)
        {
            if (builder.Length > 0)
            {
                builder.Append(separators[gapIndex]);
            }
            builder.Append(text);
        }

        var ruleWarnings = new List<string>();
        var field = 1;

        for (var i = 0; i < anchors.Length; i++)
        {
            AddRuleGap(Append, i, gaps[i], isTrailing: false, ref field, ruleWarnings);
            Append(EscapeLiteral(dictionary[anchors[i]]), i);
        }

        AddRuleGap(Append, anchors.Length, gaps[^1], isTrailing: true, ref field, ruleWarnings);
        isExecutable = ruleWarnings.Count == 0;
        warnings = ruleWarnings;
        return builder.ToString();
    }
}
