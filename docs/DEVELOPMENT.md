# Development guide

## Prerequisites

Install the .NET 10 SDK. The project targets `net10.0`, enables nullable reference types and implicit usings, and uses centrally managed test package versions.

### DeltaZulu.Parse package

`DeltaZulu.Suggester` consumes the canonical liblognorm parser catalog from
[`DeltaZulu.Parse`](https://github.com/DeltaZulu-OU/DeltaZulu.Parse), referenced as a NuGet
package (see `Directory.Packages.props` and
`src/DeltaZulu.Suggester/DeltaZulu.Suggester.csproj`). Restoring the package requires access to the
`deltazulu-github` package source mapped for the `DeltaZulu.*` pattern in `NuGet.config`. Its endpoint is `https://nuget.pkg.github.com/DeltaZulu-OU/index.json`. The repository config defines the source, but does not store credentials.

For a local clean checkout, authenticate using a GitHub personal access token (classic) with `read:packages` and access to the DeltaZulu.Parse package. The command below refers to `GITHUB_PACKAGES_TOKEN`, set locally as an environment variable; do not commit the token or a credential-bearing NuGet.config.

```bash
dotnet nuget update source deltazulu-github --username YOUR_GITHUB_USERNAME --password "$GITHUB_PACKAGES_TOKEN" --store-password-in-clear-text --configfile NuGet.config
```

On PowerShell, the equivalent one-line command is `dotnet nuget update source deltazulu-github --username YOUR_GITHUB_USERNAME --password $env:GITHUB_PACKAGES_TOKEN --store-password-in-clear-text --configfile NuGet.config`. The clear-text option persists credentials in the **local** config and is appropriate only for an isolated checkout that is never committed or redistributed. Prefer local user-level credential storage where available.

In GitHub Actions, `.github/workflows/unit-tests.yml` requests `packages: read` and injects `GITHUB_TOKEN` immediately before restore. The package owner must grant this repository's Actions workflow access to the package; `packages: read` alone does not grant cross-repository access. Fork pull-request tokens may not be entitled to private/internal packages and should not be worked around by exposing a privileged secret to untrusted code.

Dependency lock files belong beside each project and must be committed. To refresh locks intentionally, use `dotnet restore --force-evaluate` in an authenticated working copy, review changes to all `packages.lock.json` files, commit them, then check with `dotnet restore --locked-mode`. The CI workflow rejects missing lock files before restoring and must also fail if the committed graph is stale.

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
