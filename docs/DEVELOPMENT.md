# Development guide

## Prerequisites

Install the .NET 10 SDK. The project targets `net10.0`, enables nullable reference types and implicit usings, and uses centrally managed test package versions.

### DeltaZulu.Normalize submodule

`DeltaZulu.Suggester` consumes the canonical liblognorm parser catalog from
[`DeltaZulu.Normalize`](https://github.com/DeltaZulu-OU/DeltaZulu.Normalize). Because that project
is not published as a NuGet package, it is vendored as a git submodule at
`external/DeltaZulu.Normalize` and referenced as a project. A fresh checkout must initialize the
submodule before restoring, otherwise the referenced project file is missing:

```bash
git clone --recurse-submodules https://github.com/DeltaZulu-OU/DeltaZulu.LogCluster.git
```

For an existing checkout that was cloned without submodules:

```bash
git submodule update --init --recursive
```

CI checks out submodules automatically (`submodules: recursive` in the workflow). When
`DeltaZulu.Normalize` is eventually published to a feed, the submodule and project reference can be
replaced with a package reference.

## Project purpose

DeltaZulu.LogCluster exists to bring LogCluster-style unstructured log mining into the .NET ecosystem used by DeltaZulu.Platform. The repository should stay usable as both a standalone CLI and a reusable engine for future platform integration. Design changes should preserve the human-in-the-loop contract: the miner suggests message skeletons and parser fields, while maintainers decide which suggestions are safe to adopt.

## Repository layout

```text
Directory.Build.props       Shared build settings
Directory.Packages.props    Central package versions
DeltaZulu.LogCluster.slnx   Solution file
src/                        CLI and miner implementation
tests/                      MSTest project
docs/                       Documentation
external/                   Vendored submodules (DeltaZulu.Normalize)
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

Run with a small inline corpus on Linux:

```bash
printf '%s\n' \
  'Interface Ethernet1 down at node edge-a' \
  'Interface Ethernet2 down at node edge-b' \
| dotnet run --project src/DeltaZulu.LogCluster.Cli --min-support 2 --verbose
```

And on Windows:

```powershell
 'Interface Ethernet1 down at node edge-a', 'Interface Ethernet2 down at node edge-b' | dotnet run --project .\src\DeltaZulu.LogCluster.Cli\ --min-support 2 --verbose
```

## Design and documentation expectations

When adding behavior, update documentation with the context a parser author needs: why the feature exists, what output content changes, and how it affects review safety. Prefer explicit warnings and structured fields over silent assumptions when the miner cannot produce an executable parser rule.

The project acknowledges the original Perl LogCluster implementation, the LogClusterC implementation, and related publications as algorithmic references. New code should be an idiomatic C# implementation for DeltaZulu rather than copied source from those repositories.

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

## References for maintainers

* [LogCluster project page](https://ristov.github.io/logcluster/)
* [ristov/logcluster](https://github.com/ristov/logcluster) - original Perl implementation.
* [zhugegy/LogClusterC](https://github.com/zhugegy/LogClusterC) - C implementation of the LogCluster algorithm.
* [IDS 2017 LogClusterC paper](https://ristov.github.io/publications/ids17-logclusterc-web.pdf)

## Publishing

The project is configured with `PublishAot=true` and `AssemblyName=logcluster`, so release builds can be published as a native executable for a target runtime. Example:

```bash
dotnet publish src -c Release -r linux-x64 --self-contained true
```

Change the runtime identifier for other platforms, for example `win-x64` or `osx-arm64`.
