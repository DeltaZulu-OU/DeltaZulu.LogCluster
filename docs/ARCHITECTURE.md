# Architecture

DeltaZulu.LogCluster is organized as a small command-line host around a reusable mining pipeline.

## Pipeline overview

1. **Read records** - `Program` turns files, directories, standard input, or `--message` text into ordered `LogRecord` instances.
2. **Tokenize** - `TokenizedRecord` and `TokenDictionary` split records into stable word tokens while preserving separators for rendering.
3. **Select anchors** - recurring tokens that meet `MinSupport` become candidate anchors.
4. **Build candidates** - `PatternCandidate` and `CandidateKey` group compatible anchor sequences and track variable regions between anchors.
5. **Merge variants** - related patterns with low-diversity single-token differences, or shifted trailing anchors, are merged when the configured threshold allows it.
6. **Score and rank** - `CandidateScorer` combines support, anchor quality, gap consistency, and specificity into a total score.
7. **Render output** - `CandidateOutput`, `GapOutput`, and `LiblognormMotifs` produce LogCluster patterns, liblognorm rules, warnings, and parser confidence.
8. **Collect outliers** - when requested, `OutlierCollector` samples records that match no surviving candidate.

## Candidate scoring

The total score is a weighted sum of four components:

* **Support** - favors patterns that explain more records.
* **Anchor quality** - favors stable recurring words in useful positions.
* **Gap consistency** - favors variable regions with consistent width and parser motifs.
* **Pattern specificity** - favors candidates with more fixed structure and fewer broad gaps.

The CLI exposes each weight so users can bias ranking for their corpus. Setting all but one component to zero is a useful way to inspect that component in isolation.

## Gap and rule rendering

Each variable region records minimum width, maximum width, observation count, bounded samples, a suggested liblognorm parser, and confidence. Single-token values can resolve to specific parsers such as `ipv4` or `word` when all samples agree. Gaps that ever contain multiple words are represented as `rest` only when they are safe at the end of a rule; unresolved internal multi-word gaps are emitted as non-executable sketches with warnings.

This conservative behavior avoids producing liblognorm rules where an early `rest` parser would consume text needed by later anchors.

## Scaling behavior

The miner enforces both record-count and byte-count limits. It also chooses between two strategies:

* **Materialized** - read records once into memory. This is faster for normal inputs and allows simple re-use of tokenized records.
* **Streaming** - re-read records from the source when the estimated input size is large, reducing memory pressure.

`--materialize` and `--stream` override the heuristic for repeatable benchmarking and operational control.

## Public outputs

`MiningResult` contains the record count, ranked candidates, selected strategy, and optional outlier data. Each `CandidateOutput` contains both human-facing patterns and structured scoring/gap metadata so the CLI JSON output can be consumed by other tools.
