# DeltaZulu.LogCluster

DeltaZulu.LogCluster is a standalone .NET command-line tool and reusable C# mining library for discovering recurring structures in plain-text log messages while remaining suitable for integration into DeltaZulu.Platform.

The executable is named `logcluster`. It reads log lines from files, directories, standard input, or a single command-line message, then emits ranked pattern candidates and suggested [liblognorm](https://www.liblognorm.com/) rules. Its primary product role is a suggestion engine for unstructured logs that do not yet have a proper parser: operators can mine historical samples, review the generated patterns, and promote useful candidates into parser, normalization, or correlation work.

## Purpose and context

Many operational and security log sources arrive as free-form text before a team has written a stable parser. DeltaZulu.LogCluster narrows that bootstrap gap by finding repeated message skeletons, exposing the variable fields between stable anchors, and suggesting parsers for those fields. The output is intentionally reviewable rather than fully autonomous: it proposes patterns and liblognorm-style rules that can be validated by maintainers before being adopted by DeltaZulu.Platform pipelines.

This project is a C# implementation inspired by the LogCluster family of tools. The original [`ristov/logcluster`](https://github.com/ristov/logcluster) implementation is a Perl command-line tool for mining line patterns from textual event logs, while [`zhugegy/LogClusterC`](https://github.com/zhugegy/LogClusterC) is a C implementation used to explore performance characteristics of the same algorithmic lineage. DeltaZulu.LogCluster follows the same frequent-anchor and wildcard-gap idea, but it is not a source translation; it is designed for .NET packaging, tests, JSON output, liblognorm suggestions, and future DeltaZulu.Platform integration.

## What it does

* Finds recurring anchor words across log records and turns the variable regions between them into gaps.
* Scores candidates by support, anchor quality, gap consistency, and pattern specificity.
* Suggests liblognorm parsers for variable gaps when the observed values match known motifs such as IPv4 addresses or single words.
* Preserves separators such as tabs when rendering LogCluster-style patterns.
* Can report outlier lines that do not match any surviving candidate.
* Supports either materialized or streaming mining strategies for large inputs.

## Requirements

* .NET SDK 10.0 or later.
* Linux, macOS, or Windows supported by .NET.

## Build and test

```bash
dotnet restore

dotnet build

dotnet test
```

## Quick start

Run against a file:

```bash
dotnet run --project src -- ./sample.log
```

Pipe log lines through standard input:

```bash
printf '%s\n' \
  'Interface Ethernet1 down at node edge-a' \
  'Interface Ethernet2 down at node edge-b' \
  'Interface Ethernet3 down at node edge-c' \
| dotnet run --project src -- --min-support 2
```

Example text output:

```text
LogCluster.NET candidates: 1 (records: 3, minimum support: 2)

Score 1.0  Support 3  Specificity 0.60
  LogCluster: Interface *{1,1} down at node *{1,1}
  Rule:       Interface %field1:word% down at node %field2:word%
```

Emit JSON for automation:

```bash
dotnet run --project src -- --json ./sample.log
```

## Common options

| Option | Description |
| --- | --- |
| `-s`, `--min-support <n>` | Minimum number of records that must contain a word or candidate. Default: `2`. |
| `-n`, `--max-candidates <n>` | Maximum candidates to print. Default: `50`. |
| `--json` | Emit machine-readable JSON instead of text. |
| `--outliers` | Include lines that matched no surviving candidate. |
| `-v`, `--verbose` | Print gap samples, parser confidence, and rule warnings. |
| `--stream` | Force the re-read-from-disk streaming strategy. |
| `--materialize` | Force loading all records into memory. |
| `--max-records <n>` | Abort when input exceeds this many records. Default: `5000000`. |
| `--max-input-bytes <n>` | Abort when input exceeds this many bytes. Default: `2147483648`. |

See [CLI reference](docs/CLI.md) for the complete option list and output schema.

## Design principles

* **Human-in-the-loop suggestions** - generated rules are candidates for review, not a claim that every log source can be parsed safely without operator judgment.
* **Stable anchors first** - recurring words that meet the support threshold form the backbone of each pattern, while lower-frequency regions become bounded gaps such as `*{1,2}`.
* **Operational outputs** - text output is optimized for inspection, and JSON output is optimized for automation in platform workflows.
* **Conservative parser generation** - liblognorm rules are marked executable only when variable gaps can be represented safely; ambiguous internal multi-word gaps remain sketches with warnings.
* **Separated syntax ownership** - parser suggestions flow through the suggester abstraction; `DeltaZulu.Normalize` is the intended source of canonical liblognorm parser syntax once it is consumable as a package or shared project reference.
* **Scalable execution choices** - materialized and streaming strategies let users trade speed and memory use for different corpus sizes.

## References and acknowledgement

`DeltaZulu.LogCluster` acknowledges the prior LogCluster work by [Risto Vaarandi](https://ristov.github.io/) and collaborators, including the Perl LogCluster implementation and publications describing the algorithm. It also acknowledges the LogClusterC implementation and IDS 2017 paper, which document a C implementation, performance evaluation, support-threshold workflow, outlier mining, and applications to security and network event logs.

Reference material:

* [LogCluster project page](https://ristov.github.io/logcluster/)
* [ristov/logcluster](https://github.com/ristov/logcluster) - original Perl implementation.
* [zhugegy/LogClusterC](https://github.com/zhugegy/LogClusterC) - C implementation of the LogCluster algorithm.
* [IDS 2017 LogClusterC paper](https://ristov.github.io/publications/ids17-logclusterc-web.pdf) - performance and application discussion for LogClusterC.

## Documentation

* [CLI reference](docs/CLI.md) - inputs, options, output formats, and examples.
* [Architecture](docs/ARCHITECTURE.md) - mining pipeline, scoring, gaps, outliers, and scaling behavior.
* [Roadmap](docs/ROADMAP.md) - prioritized phases for Normalize-backed parser suggestions and packaging workflow.
* [Comparison with LogClusterC and Perl](docs/COMPARISON.md) - compatibility points and deliberate divergences from prior implementations.
* [Development guide](docs/DEVELOPMENT.md) - repository layout, build/test workflow, and release notes.

## Repository layout

```text
src/    Command-line app and mining implementation
tests/  MSTest coverage for candidate mining, rendering, outliers, and scaling limits
docs/   User and maintainer documentation
```

## License

This project is licensed under the terms in [LICENSE.txt](LICENSE.txt).
