namespace DeltaZulu.LogCluster;

public sealed class LogClusterMiner
{
    private readonly LogClusterOptions options;

    public LogClusterMiner(LogClusterOptions options)
    {
        this.options = options;
    }

    /// <summary>
    /// Picks the strategy once, before mining starts. Below the safety margin, holding every
    /// tokenized record in memory is simpler and faster than re-reading from disk three times;
    /// above it, streaming trades CPU (re-tokenizing each pass) for a memory ceiling set by the
    /// still-finite TokenDictionary/candidate structures rather than by recordCount record count.
    /// </summary>
    /// <param name="estimatedInputBytes"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static bool ShouldStream(long? estimatedInputBytes, LogClusterOptions options)
    {
        if (options.ForceMaterialize is { } forced)
        {
            return !forced;
        }

        if (estimatedInputBytes is not { } bytes)
        {
            return false;
        }

        var headroom = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (headroom <= 0)
        {
            headroom = options.MaxInputBytes;
        }

        const double tokenizedOverheadFactor = 6.0; // int[] tokens + dictionary entries + candidate bookkeeping vs. raw bytes
        const double safetyMargin = 0.5;
        var estimatedMemoryUsage = bytes * tokenizedOverheadFactor;
        return estimatedMemoryUsage > headroom * safetyMargin;
    }

    /// <summary>
    /// Reused for both strategies: "materialize" runs this once and caches the array; "stream"
    /// re-invokes it (and re-tokenizes) once per pass, trading CPU for not holding every record
    /// in memory at once. The two strategies differ only in whether tokenizedPass() below is
    /// backed by a cached array or a fresh re-enumeration of recordSource().
    /// </summary>
    /// <param name="records"></param>
    /// <returns></returns>
    public MiningResult Mine(IEnumerable<LogRecord> records) => Mine(() => records, estimatedInputBytes: null);

    public MiningResult Mine(Func<IEnumerable<LogRecord>> recordSource, long? estimatedInputBytes)
    {
        var dictionary = new TokenDictionary();
        var streaming = ShouldStream(estimatedInputBytes, options);

        Func<IEnumerable<TokenizedRecord>> tokenizedPass;
        if (streaming)
        {
            tokenizedPass = () => TokenizedRecord.Stream(recordSource(), dictionary, options.MaxRecords, options.MaxInputBytes);
        }
        else
        {
            var materialized = TokenizedRecord.Stream(recordSource(), dictionary, options.MaxRecords, options.MaxInputBytes).ToArray();
            tokenizedPass = () => materialized;
        }

        var (frequentWords, recordCount) = DiscoverFrequentWords(tokenizedPass());
        var candidates = GenerateCandidates(tokenizedPass(), frequentWords);
        var routes = new Dictionary<CandidateKey, EvidenceRoute>();
        MergeShiftedCandidates(candidates, routes);
        MergeLowDiversityVariants(candidates, routes, options.WordWeightThreshold);
        var survivors = candidates.Values.Distinct().Where(c => c.Support >= options.MinSupport).ToArray();
        foreach (var candidate in survivors)
        {
            candidate.InitializeGaps(options.MaxSamplesPerGap);
        }

        var outliers = options.ShowOutliers ? new OutlierCollector(options.MaxOutlierSamples) : null;
        CollectEvidence(tokenizedPass(), frequentWords, candidates, dictionary, routes, outliers);

        var outputs = survivors.Select(c => c.ToOutput(recordCount, dictionary, options))
            .OrderByDescending(c => c.Score.Total)
            .ThenByDescending(c => c.Support)
            .ThenByDescending(c => c.Specificity)
            .ThenBy(c => c.LogClusterPattern, StringComparer.Ordinal)
            .Take(options.MaxCandidates)
            .ToArray();
        return new MiningResult(recordCount, outputs, streaming ? "stream" : "materialize", outliers?.Count ?? 0, outliers?.Samples ?? []);
    }

    private static void CollectEvidence(IEnumerable<TokenizedRecord> records, bool[] frequentWords, Dictionary<CandidateKey, PatternCandidate> candidates, TokenDictionary dictionary, Dictionary<CandidateKey, EvidenceRoute> routes, OutlierCollector? outliers)
    {
        foreach (var record in records)
        {
            var anchors = AnchorBuffer.From(record.Tokens, frequentWords);
            if (anchors.Length == 0)
            {
                outliers?.Observe(record.Reconstruct(dictionary));
                continue;
            }

            var key = new CandidateKey(anchors);
            if (!candidates.TryGetValue(key, out var candidate) || !candidate.KeepEvidence)
            {
                outliers?.Observe(record.Reconstruct(dictionary));
                continue;
            }

            var route = routes.TryGetValue(key, out var r) ? r : default;
            candidate.ObserveGaps(record.Tokens, record.Separators, frequentWords, dictionary, route.Leading, route.Trailing, route.AbsorbedPositions);
        }
    }

    private static Dictionary<CandidateKey, PatternCandidate> GenerateCandidates(IEnumerable<TokenizedRecord> records, bool[] frequentWords)
    {
        var candidates = new Dictionary<CandidateKey, PatternCandidate>();
        foreach (var record in records)
        {
            var anchors = AnchorBuffer.From(record.Tokens, frequentWords);
            if (anchors.Length == 0)
            {
                continue;
            }

            var key = new CandidateKey(anchors);
            if (!candidates.TryGetValue(key, out var candidate))
            {
                candidate = new PatternCandidate(key, anchors);
                candidates.Add(key, candidate);
            }
            candidate.ObserveSupport(record.SequenceNumber);
        }
        return candidates;
    }

    /// <summary>
    /// <para>
    /// Word-weight-style join (mirrors LogClusterC's --wweight): candidates that differ in
    /// exactly one anchor position, whose distinct values at that position recur often enough
    /// relative to their combined support (per LogClusterOptions.WordWeightThreshold), are
    /// reported as one candidate with that position demoted to a gap instead of fragmenting into
    /// one candidate per value (e.g. per-source-IP variants of the same alert).
    /// </para>
    /// <para>
    /// Scoped to candidates that haven't already absorbed a MergeShiftedCandidates edge merge:
    /// composing an internal-position removal with a prior edge absorption would require
    /// reindexing the edge route, which isn't worth the correctness risk for this heuristic.
    /// </para>
    /// </summary>
    /// <param name="candidates"></param>
    /// <param name="routes"></param>
    /// <param name="threshold"></param>
    private static void MergeLowDiversityVariants(Dictionary<CandidateKey, PatternCandidate> candidates, Dictionary<CandidateKey, EvidenceRoute> routes, double threshold)
    {
        var alreadyEdgeMerged = new HashSet<PatternCandidate>(routes.Keys.Select(k => candidates[k]));

        var groups = new Dictionary<(int Position, CandidateKey Template), List<PatternCandidate>>();
        foreach (var candidate in candidates.Values.Distinct())
        {
            if (candidate.AnchorCount == 0 || alreadyEdgeMerged.Contains(candidate) || candidates[candidate.Key] != candidate)
            {
                continue;
            }

            for (var position = 0; position < candidate.AnchorCount; position++)
            {
                var groupKey = (position, candidate.TemplateKey(position));
                if (!groups.TryGetValue(groupKey, out var members))
                {
                    groups[groupKey] = members = [];
                }
                members.Add(candidate);
            }
        }

        foreach (var group in groups.OrderBy(g => g.Key.Position).ThenBy(g => g.Key.Template.ToString(), StringComparer.Ordinal))
        {
            var position = group.Key.Position;
            var live = group.Value.Where(c => candidates[c.Key] == c).Distinct().ToArray();
            if (live.Length < 2)
            {
                continue;
            }

            var combinedSupport = live.Sum(c => c.Support);
            if (live.Length > threshold * combinedSupport)
            {
                continue;
            }

            var mergedAnchors = live[0].AnchorsWithout(position);
            var mergedKey = new CandidateKey(mergedAnchors);
            if (!candidates.TryGetValue(mergedKey, out var merged))
            {
                merged = new PatternCandidate(mergedKey, mergedAnchors);
            }

            foreach (var member in live)
            {
                if (member == merged)
                {
                    continue;
                }

                merged.AbsorbSupport(member);
                candidates[member.Key] = merged;
                routes[member.Key] = new EvidenceRoute(Leading: 0, Trailing: 0, AbsorbedPositions: new HashSet<int> { position });
            }
            candidates[mergedKey] = merged;
        }
    }

    /// <summary>
    /// Aggregation-style merge (mirrors LogClusterC's --aggrsup): a candidate whose anchor
    /// sequence is exactly one token longer than another candidate's, with the extra token at
    /// either edge, is a positionally-shifted variant of the same underlying pattern rather than
    /// a distinct one. Merge it into the shorter candidate, folding the extra anchor into the
    /// adjacent boundary gap. Only single-token, single-edge differences are merged: an
    /// already-merged key that some longer candidate reduces to by more than one token is left
    /// as a separate candidate rather than chaining merges across a larger gap.
    /// </summary>
    /// <param name="candidates"></param>
    /// <param name="routes"></param>
    private static void MergeShiftedCandidates(Dictionary<CandidateKey, PatternCandidate> candidates, Dictionary<CandidateKey, EvidenceRoute> routes)
    {
        var originals = candidates.Values.Distinct().OrderBy(c => c.AnchorCount).ToArray();
        foreach (var extended in originals)
        {
            if (extended.AnchorCount == 0 || candidates[extended.Key] != extended)
            {
                continue;
            }

            if (candidates.TryGetValue(extended.ReducedKey(dropFirst: true), out var leadingBase) && leadingBase != extended && leadingBase.AnchorCount == extended.AnchorCount - 1)
            {
                leadingBase.AbsorbSupport(extended);
                candidates[extended.Key] = leadingBase;
                routes[extended.Key] = new EvidenceRoute(Leading: 1, Trailing: 0, AbsorbedPositions: null);
            }
            else if (candidates.TryGetValue(extended.ReducedKey(dropFirst: false), out var trailingBase) && trailingBase != extended && trailingBase.AnchorCount == extended.AnchorCount - 1)
            {
                trailingBase.AbsorbSupport(extended);
                candidates[extended.Key] = trailingBase;
                routes[extended.Key] = new EvidenceRoute(Leading: 0, Trailing: 1, AbsorbedPositions: null);
            }
        }
    }

    private (bool[] Frequent, int RecordCount) DiscoverFrequentWords(IEnumerable<TokenizedRecord> records)
    {
        // Sized lazily rather than up front: in the streaming strategy the dictionary is still
        // growing as this same pass tokenizes records, so the final token universe isn't known
        // until the pass completes.
        var counts = new List<int>();
        var seenStamp = new List<int>();
        var stamp = 0;
        var recordCount = 0;
        foreach (var record in records)
        {
            recordCount++;
            stamp++;
            foreach (var token in record.Tokens)
            {
                while (seenStamp.Count <= token)
                {
                    seenStamp.Add(0);
                    counts.Add(0);
                }

                if (seenStamp[token] == stamp)
                {
                    continue;
                }

                seenStamp[token] = stamp;
                counts[token]++;
            }
        }

        var frequent = new bool[counts.Count];
        for (var token = 0; token < counts.Count; token++)
        {
            frequent[token] = counts[token] >= options.MinSupport;
        }
        return (frequent, recordCount);
    }
}
