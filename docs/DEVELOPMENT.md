# Development guide

## Prerequisites

Install the .NET 10 SDK. The project targets `net10.0`, enables nullable reference types and implicit usings, and uses centrally managed test package versions.

## Repository layout

```text
Directory.Build.props       Shared build settings
Directory.Packages.props    Central package versions
DeltaZulu.LogCluster.slnx   Solution file
src/                        CLI and miner implementation
tests/                      MSTest project
docs/                       Documentation
```

## Daily workflow

Restore, build, and test from the repository root:

```bash
dotnet restore

dotnet build

dotnet test
```

Run the CLI locally:

```bash
dotnet run --project src -- --help
```

Run with a small inline corpus:

```bash
printf '%s\n' \
  'Interface Ethernet1 down at node edge-a' \
  'Interface Ethernet2 down at node edge-b' \
| dotnet run --project src -- --min-support 2 --verbose
```

## Testing focus

The current tests cover:

* trailing and internal gap rendering,
* parser selection and parser confidence,
* outlier collection,
* score weighting,
* low-diversity variant merging,
* separator preservation,
* input limits, and
* materialized versus streaming strategy equivalence.

Add tests when changing candidate grouping, scoring, rendering, or CLI parsing because small changes can alter both human-readable and JSON output.

## Publishing

The project is configured with `PublishAot=true` and `AssemblyName=logcluster`, so release builds can be published as a native executable for a target runtime. Example:

```bash
dotnet publish src -c Release -r linux-x64 --self-contained true
```

Change the runtime identifier for other platforms, for example `win-x64` or `osx-arm64`.
