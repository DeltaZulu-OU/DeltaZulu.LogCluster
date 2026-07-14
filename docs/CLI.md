# CLI reference

`logcluster` discovers recurring message structures from line-oriented logs and prints ranked candidate patterns. It is intended for unstructured logs that do not yet have a reliable parser, giving maintainers a reviewable starting point for DeltaZulu.Platform normalization work.

## Usage

```text
logcluster [options] [file-or-directory ...]
```

If no files or directories are supplied, `logcluster` reads one message per line from standard input. Directory inputs are scanned recursively in ordinal file-name order.

## Operational context

Use the CLI when you have raw samples from a source and need to understand the dominant message shapes before writing or improving a parser. A typical workflow is:

1. collect representative historical log lines,
2. run `logcluster` with a support threshold that filters one-off noise,
3. inspect text output or export JSON,
4. review suggested liblognorm rules and warnings, and
5. promote validated candidates into parser or normalization configuration.

The command produces suggestions only. Generated rules should be reviewed against source documentation, edge cases, and production samples before they are used in an automated ingest path.

## Inputs

| Input form | Example | Notes |
| --- | --- | --- |
| File | `logcluster /var/log/app.log` | Reads one record per line. |
| Directory | `logcluster ./logs` | Recursively reads all files under the directory. |
| Standard input | `journalctl -o cat | logcluster` | Used when no path arguments are provided. |
| Single message | `logcluster --message "Interface down node1"` | Mines exactly one message, mostly useful for smoke tests. |

Empty lines are skipped by default. Use `--keep-empty` to include them.

## Options

| Option | Default | Description |
| --- | ---: | --- |
| `-s`, `--min-support <n>` | `2` | Minimum records that must contain a word or candidate. |
| `-n`, `--max-candidates <n>` | `50` | Maximum number of ranked candidates to print. |
| `--max-samples <n>` | `8` | Maximum example values retained for each variable gap. |
| `--max-records <n>` | `5000000` | Abort when more than this many records are read. |
| `--max-input-bytes <n>` | `2147483648` | Abort when estimated or observed input bytes exceed this limit. |
| `--materialize` | heuristic | Force loading all records into memory. |
| `--stream` | heuristic | Force the streaming strategy, which re-reads input instead of materializing it. |
| `--weight-support <n>` | `0.35` | Score weight for support strength. |
| `--weight-anchor <n>` | `0.30` | Score weight for anchor quality. |
| `--weight-gaps <n>` | `0.20` | Score weight for gap consistency. |
| `--weight-specificity <n>` | `0.15` | Score weight for pattern specificity. |
| `--wweight-threshold <n>` | `0.5` | Merge single-anchor variants when `distinct values <= threshold * combined support`. |
| `--outliers` | off | Report lines that matched no surviving candidate. |
| `--max-outlier-samples <n>` | `20` | Maximum outlier line samples to print. |
| `-m`, `--message <text>` | unset | Mine a single message supplied on the command line. |
| `--json` | off | Emit JSON instead of text. |
| `-v`, `--verbose` | off | Print gap samples, parser confidence, and warnings. |
| `--keep-empty` | off | Include empty input lines. |
| `-h`, `--help` | off | Show help. |

All numeric values must be non-negative, except counts and limits that must be positive integers.

## Text output

Text mode starts with a summary line, then prints each candidate:

```text
LogCluster.NET candidates: 1 (records: 3, minimum support: 2)

Score 1.0  Support 3  Specificity 0.60
  LogCluster: Interface *{1,1} down at node *{1,1}
  Rule:       Interface %field1:word% down at node %field2:word%
  Score parts support=1.0, anchors=1.0, gaps=1.0, specificity=0.6
```

A LogCluster wildcard such as `*{1,2}` means the gap observed at least one word and at most two words. The generated `Rule` is executable only when every gap can be represented safely as a liblognorm parser. Internal multi-word gaps are rendered as structural sketches with warnings instead of unsafe executable rules.

## JSON output

Without `--outliers`, JSON output is an array of candidates. With `--outliers`, output is an object containing `candidates`, `outlierCount`, and `outlierSamples`.

Candidate fields:

| Field | Meaning |
| --- | --- |
| `support` | Number of records matched by the candidate. |
| `specificity` | Fraction-like measure of how much of the pattern is anchored instead of variable. |
| `logClusterPattern` | LogCluster-style pattern using `*{min,max}` gaps. |
| `liblognormRule` | Suggested liblognorm rule or structural sketch. |
| `isExecutableRule` | `true` when `liblognormRule` is intended to be directly executable. |
| `ruleWarnings` | Reasons a rule is a sketch or needs review. |
| `gaps` | Gap observations, samples, suggested parser, and parser confidence. |
| `score` | Total score and weighted component scores. |

## Output content

Text and JSON modes both preserve the context needed for review: support counts show how common a candidate is, gap bounds show how much text varied between anchors, samples show representative field values, parser confidence explains liblognorm suggestions, and warnings identify candidates that should remain sketches. This content is designed to be copied into parser development notes or consumed by DeltaZulu.Platform tooling.

## Examples

Find recurring SSH messages in a file and include outliers:

```bash
logcluster --min-support 5 --outliers /var/log/auth.log
```

Generate compact JSON for downstream tooling:

```bash
logcluster --json --max-candidates 25 ./logs > candidates.json
```

Tune scoring to prioritize high support over specificity:

```bash
logcluster --weight-support 1 --weight-anchor 0 --weight-gaps 0 --weight-specificity 0 ./logs
```

Force streaming for large directory inputs:

```bash
logcluster --stream --max-input-bytes 10737418240 ./logs
```
