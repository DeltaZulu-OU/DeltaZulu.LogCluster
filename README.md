# DeltaZulu.LogCluster

DeltaZulu.LogCluster is a standalone .NET command-line tool for discovering recurring structures in plain-text log messages. It was extracted from [`DeltaZulu.Normalize`](https://github.com/DeltaZulu-OU/DeltaZulu.Normalize) so the LogCluster miner can be built, tested, published, and used independently.

The executable is named `logcluster`. It reads log lines from files, directories, standard input, or a single command-line message, then emits ranked pattern candidates and suggested [liblognorm](https://www.liblognorm.com/) rules.

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

## Documentation

* [CLI reference](docs/CLI.md) - inputs, options, output formats, and examples.
* [Architecture](docs/ARCHITECTURE.md) - mining pipeline, scoring, gaps, outliers, and scaling behavior.
* [Development guide](docs/DEVELOPMENT.md) - repository layout, build/test workflow, and release notes.

## Repository layout

```text
src/    Command-line app and mining implementation
tests/  MSTest coverage for candidate mining, rendering, outliers, and scaling limits
docs/   User and maintainer documentation
```

## License

This project is licensed under the terms in [LICENSE.txt](LICENSE.txt).
