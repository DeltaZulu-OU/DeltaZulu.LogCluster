# Comparison with LogClusterC and LogCluster Perl

DeltaZulu.LogCluster is inspired by the LogCluster algorithm and its established C and Perl implementations, but it is an independent C# implementation designed for parser suggestion and DeltaZulu.Platform integration. This document identifies the compatibility points and deliberate divergences that matter when comparing candidate sets, support values, rankings, and rendered output. It should not be read as a claim that DeltaZulu.LogCluster is a direct port of either implementation.

## Comparison with LogClusterC

DeltaZulu.LogCluster follows the core candidate model used by [LogClusterC](https://github.com/zhugegy/LogClusterC), the C reference implementation by Risto Vaarandi and Mauno Pihelgas. The projects therefore agree on several fundamental mining semantics. However, DeltaZulu.LogCluster replaces or omits several supporting heuristics and adds ranking and rendering features that do not exist in LogClusterC.

### Compatibility points

* **Candidate identity.** Both tools identify a pattern by the ordered sequence of frequent anchor words in a record. Runs of infrequent words between anchors are represented as `[min, max]` word-count envelopes rather than enumerated values. LogClusterC stores this identity as a newline-joined string key plus a `fullWildcard[]` array on `struct Cluster`. DeltaZulu.LogCluster stores it as a `CandidateKey` over token IDs plus `GapStatistics` entries on `PatternCandidate`. The storage models differ, but the identity semantics are equivalent.

* **Support counting.** LogClusterC counts the number of distinct lines containing a word or matching a cluster. Repeated occurrences within one line do not increase support. `LogClusterMiner.DiscoverFrequentWords` and `PatternCandidate.ObserveSupport` apply the same one-record, one-increment rule through a per-pass stamp and a last-sequence-number guard. Support values are therefore comparable in meaning.

* **Threshold semantics.** LogClusterC uses `--support` or `--rsupport` both to classify words as frequent and to decide whether candidates survive. `LogClusterOptions.MinSupport` performs the same dual role in DeltaZulu.LogCluster.

* **Wildcard rendering.** Both implementations omit a wildcard when the observed gap cannot contain any words. LogClusterC's `print_cluster` suppresses `*{min,max}` when the maximum width is zero. `PatternCandidate.RenderLogCluster` and `PatternCandidate.RenderRule` use the equivalent `gap.MaxWords > 0` condition rather than emitting `*{0,0}`.

### Deliberate divergences

* **Aggregate-support merging.** LogClusterC's `--aggrsup` heuristic builds a prefix trie over all candidates. For each general candidate, it locates more-specific candidates whose anchor sequences fit the candidate's wildcard envelopes across any number of gap positions, then rolls their support into the general candidate. `LogClusterMiner.MergeShiftedCandidates` supports only a narrow edge case: one candidate must contain exactly one additional anchor, and that anchor must occur at the leading or trailing edge. Interior insertions, multiple additional anchors, chained merges, and multi-position generalization remain separate. This method is an analog of `--aggrsup`, not a port of it.

* **Word-dependency merging.** LogClusterC's `--wweight` heuristic builds a corpus-wide word co-occurrence matrix and computes conditional dependencies using `dep(w1, w2) = matrix[w1][w2] / matrix[w1][w1]`. It can then mark low-dependency anchor positions as mergeable. `LogClusterMiner.MergeLowDiversityVariants` uses a local diversity proxy instead. It merges candidates that differ at one anchor position when the number of distinct values at that position is small relative to their combined support, as controlled by `WordWeightThreshold`. This avoids the `O(words²)` dependency matrix, but it does not model inter-word dependency and can produce different results on corpora where dependency and local diversity are not equivalent.

* **Tokenization.** LogClusterC uses `[ ]+` as its default delimiter, so only ASCII spaces split words unless `--separator` is supplied. `TokenizedRecord.Tokenize` always splits on `char.IsWhiteSpace`. Tabs and other Unicode whitespace therefore create token boundaries by default, while the original separator is preserved for rendering. DeltaZulu.LogCluster has no option equivalent to `--separator` for restoring space-only tokenization.

* **Per-record limits.** LogClusterC limits each line to `MAXWORDS` words and `MAXLINELEN` bytes, currently 512 words and 10,240 bytes. Longer lines are truncated. DeltaZulu.LogCluster does not impose equivalent per-record word or byte limits. Its limits are corpus-wide controls such as `--max-records` and `--max-input-bytes`.

* **Large-input approximation.** LogClusterC can use the optional `--wsize` and `--csize` single-hash sketches as pre-filters. These sketches avoid materializing words or candidates that cannot reach the support threshold, at the cost of another input pass. DeltaZulu.LogCluster has no sketch pre-pass. It controls memory by choosing between record materialization and source re-reading across passes.

* **Word preprocessing.** LogClusterC supports `--wfilter`, `--wsearch`, and `--wreplace` to normalize words before frequency counting. DeltaZulu.LogCluster mines words as observed and has no equivalent preprocessing stage. This also means it does not reproduce LogClusterC's inconsistency in which candidate generation can use normalized words while `outliers.c` performs matching without the same normalization.

* **Candidate ranking.** LogClusterC ranks candidates by raw support or, in arity mode, by anchor count. `CandidateScorer` instead computes a weighted score from support strength, anchor quality, gap consistency, and pattern specificity. The weights are exposed through CLI options. This multi-factor score is specific to DeltaZulu.LogCluster and has no LogClusterC counterpart.

* **Output ordering.** LogClusterC's default `O(n²)` selection sort is unstable for equal-support candidates, while arity mode can leave ties in hash-bucket order. `LogClusterMiner.Mine` applies a total deterministic order: score descending, support descending, specificity descending, and pattern text in ordinal ascending order.

* **Parser-oriented output.** The `LiblognormSuggestionEngine` adapter over the `DeltaZulu.Normalize` parser catalog and `PatternCandidate.RenderRule` add gap-motif detection and liblognorm rule rendering. LogClusterC emits LogCluster-style patterns only and has no equivalent parser-suggestion layer.

### Practical interpretation

The differences above are deliberate design choices rather than accidental incompatibilities. References elsewhere in the codebase that describe `MergeShiftedCandidates` or `MergeLowDiversityVariants` as mirroring LogClusterC should be interpreted as narrow behavioral analogies. They should not be interpreted as claims of complete `--aggrsup` or `--wweight` compatibility. Identical input can therefore produce different candidate consolidation, ranking, and final ordering.

## Comparison with LogCluster Perl

DeltaZulu.LogCluster also follows the core mining model of Risto Vaarandi's original `logcluster.pl`: frequent words define the stable anchors of a candidate, while variable regions are represented as bounded wildcard gaps. The implementation retains that conceptual model but changes candidate merging, evidence collection, ranking, resource management, and output generation. It is therefore a reinterpretation rather than a line-for-line port.

### Compatibility points

* **Core mining model.** Both implementations first identify frequent words and then group records according to their ordered frequent-word tuples. This preserves the central LogCluster distinction between stable anchors and variable regions.

* **Candidate representation.** Both implementations represent variable regions through `*{min,max}`-style word-count envelopes. The resulting patterns express the minimum and maximum number of words observed between adjacent anchors.

* **Support-oriented candidate discovery.** Both implementations use record support to determine which words and candidate structures are significant. Their later ranking behavior differs, but support remains the underlying evidence used during mining.

* **Default whitespace treatment.** DeltaZulu.LogCluster splits on Unicode whitespace runs, which is consistent with the default behavior of the Perl implementation. It does not, however, implement the Perl tool's configurable `--separator` option.

### Deliberate divergences

* **Aggregate-support merging.** The Perl `--aggrsup` option builds a prefix tree through `build_prefix_tree` and `find_more_specific`. It rolls support into a general candidate when a more-specific candidate contains a compatible superset of anchors within the general candidate's gap bounds. Additional anchors may occur inside any gap, and more than one additional anchor is allowed. `MergeShiftedCandidates` handles only candidates that differ by one leading or trailing anchor. Interior insertions and multi-anchor differences are not merged.

* **Word-dependency merging.** The Perl `--wweight` option computes corpus-wide dependency statistics with four weight functions, `f1` through `f4`, over a word co-occurrence matrix. It joins clusters whose low-weight anchors differ. `MergeLowDiversityVariants` covers only the single-position case and uses value diversity relative to combined support as its criterion. It does not construct a dependency matrix or perform multi-word generalization. Edge merging and position merging are also mutually exclusive for a candidate, which prevents a stale edge merge from being composed with a later position merge.

* **Candidate ranking.** `logcluster.pl` orders clusters by `Count`, meaning support, in descending order. DeltaZulu.LogCluster applies `CandidateScorer`, which combines support strength, anchor quality, gap consistency, and pattern specificity. The `--weight-*` options configure an original ranking heuristic rather than published LogCluster behavior.

* **Parser-oriented output.** The Perl tool emits LogCluster patterns and support counts. The `DeltaZulu.Normalize`-backed suggestion engine and the rule-rendering path in `PatternCandidate.ToOutput` additionally suggest parsers such as `ipv4`, `number`, and `word`, and distinguish executable rules from structural sketches. These capabilities are additions to the mining algorithm.

* **Input and vocabulary preprocessing.** The Perl implementation supports line-level filtering and rewriting through options such as `--lfilter`, `--template`, and `--lcfunc`. It also supports word normalization and word-class generalization through `--wfilter`, `--wsearch`, `--wreplace`, and `--wcfunc`. DeltaZulu.LogCluster implements none of these stages and mines input records as observed.

* **Configurable separators.** The Perl implementation permits a custom word separator through `--separator`. DeltaZulu.LogCluster always splits on Unicode whitespace runs and does not expose an equivalent configuration option.

* **Large-input approximation.** The Perl implementation can use `--wsize` and `--csize` hash-bucket sketches to reduce memory consumption when exact word or candidate counters would be too large. DeltaZulu.LogCluster does not implement approximate counting. It uses a materialize-versus-stream strategy together with `--max-records` and `--max-input-bytes` limits.

* **Saved intermediate state.** The Perl implementation can write and reload cluster and word-dependency data through `--writedump`, `--readdump`, `--readwords`, and `--writewords`. This permits threshold changes without repeating the complete mining process. DeltaZulu.LogCluster has no equivalent intermediate-state format.

* **Gap-evidence collection.** `logcluster.pl` updates wildcard minimum and maximum sizes during candidate discovery for every candidate, including candidates that later fail the support threshold. DeltaZulu.LogCluster runs `PatternCandidate.InitializeGaps` and `PatternCandidate.ObserveGaps` in a dedicated `CollectEvidence` pass after candidate merging and `MinSupport` filtering. Only surviving candidates collect gap samples, separators, and parser votes. This reduces discarded evidence state but requires one additional full pass over the input.

* **Output ordering.** Equal-support candidates in the Perl implementation can inherit randomized hash iteration order from expressions such as `keys %candidates` and `keys %clusters`. DeltaZulu.LogCluster uses a deterministic total order based on score, support, specificity, and ordinal pattern text. Repeated runs over identical input therefore produce the same candidate ordering.

### Practical interpretation

DeltaZulu.LogCluster preserves the original algorithm's anchor-and-gap model, but it does not reproduce the full behavior of `--aggrsup`, `--wweight`, preprocessing, sketching, or dump-based iteration. Its extra evidence pass, deterministic ordering, weighted ranking, and parser-oriented output further change observable results. Comparisons against `logcluster.pl` should therefore expect differences in candidate sets and rankings, not merely differences in formatting.
::: 
