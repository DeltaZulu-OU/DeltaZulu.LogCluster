# Roadmap

This roadmap tracks planned work for making DeltaZulu.LogCluster a maintainable parser-suggestion component while preserving the mining core as a reusable LogCluster implementation.

## Status

* **P0 — done.** `DeltaZulu.Normalize` exposes the public `ILiblognormParserCatalog` / `LiblognormParserCatalog.Instance` catalog API described below.
* **P1 — done.** `DeltaZulu.Suggester` references `DeltaZulu.Normalize` (git submodule under `external/`) and `LiblognormSuggestionEngine` is now a thin adapter over `LiblognormParserCatalog.Instance`. The local `LiblognormMotifs` shim has been removed and regression tests prove suggestions come from Normalize metadata and full-match validation.
* **P2–P4** remain open.

## Prioritized phases

| Priority | Phase | Goal | Suggested changes | Exit criteria |
| --- | --- | --- | --- | --- |
| P0 (done) | Normalize parser catalog contract | Make `DeltaZulu.Normalize` the canonical source for liblognorm parser names, priorities, and whole-sample validators. | Add a public `ILiblognormParserCatalog` in `DeltaZulu.Normalize` that exposes parser descriptors, `WordParserName`, `RestParserName`, lookup by parser name, and `IsFullMatch` validation. Keep parser IDs, parser delegates, `Npb`, dispatch details, and extraction internals private. | `DeltaZulu.Normalize` publishes or otherwise exposes a consumable catalog API that the suggester can reference without duplicating parser syntax. |
| P1 (done) | Suggester catalog integration | Replace the local motif shim with an adapter over the Normalize catalog. | Add `DeltaZulu.Normalize` as a project/package reference to `DeltaZulu.Suggester`; update `LiblognormSuggestionEngine` to use `LiblognormParserCatalog.Instance`; remove duplicated regex/IP/date checks once Normalize validators cover the supported motifs. | `DeltaZulu.Suggester` no longer defines canonical parser names or validator logic locally; tests prove suggestions come from Normalize metadata and full-match validation. |
| P2 | Rule-rendering hardening | Use shared parser metadata consistently while keeping LogCluster mining independent. | Keep `DeltaZulu.LogCluster` dependent only on `IGapSuggestionEngine`; preserve conservative handling for unresolved internal gaps; add regression coverage for fallback-only `rest` and sample-inferable parser selection. | Core mining code remains parser-package agnostic, and rule rendering behavior is covered by tests for specific parsers, fallback parsers, and unresolved gaps. |
| P3 | Packaging and repository workflow | Make the cross-repository dependency repeatable for development and CI. | Prefer a package reference when `DeltaZulu.Normalize` is published; otherwise use a documented submodule path and project reference. Add restore/build instructions for submodule users. | A clean checkout can restore, build, and test with the Normalize-backed suggester in CI and local development. |
| P4 | Expanded parser suggestions | Broaden supported parser suggestions after the catalog integration is stable. | Review `DeltaZulu.Normalize` parser descriptors for additional sample-inferable motifs such as structured parsers; add confidence tests and user-facing warnings for ambiguous motifs. | New parser suggestions are data-driven from the catalog, have regression tests, and preserve human-reviewable output. |

## Proposed Normalize catalog shape

The suggester needs a metadata-only public surface from `DeltaZulu.Normalize`. It does not need access to `Npb`, parser delegates, field extraction, parser IDs, or PDAG internals.

```csharp
namespace DeltaZulu.Normalize;

public enum LiblognormParserSuggestionUse
{
    None,
    InferFromSample,
    FallbackOnly,
}

public sealed record LiblognormParserDescriptor(
    string Name,
    int Priority,
    LiblognormParserSuggestionUse SuggestionUse,
    bool RequiresConfiguration)
{
    public bool CanInferFromSample => SuggestionUse == LiblognormParserSuggestionUse.InferFromSample;

    public bool CanRenderWithoutConfiguration => !RequiresConfiguration;
}

public interface ILiblognormParserCatalog
{
    IReadOnlyList<LiblognormParserDescriptor> Parsers { get; }

    string WordParserName { get; }

    string RestParserName { get; }

    bool TryGetParser(string name, out LiblognormParserDescriptor parser);

    bool IsFullMatch(string parserName, ReadOnlySpan<char> sample);
}
```

`DeltaZulu.Normalize` can implement this as a thin public adapter over its internal parser table. `InferFromSample` identifies parsers the suggester may infer from observed gap samples, while `FallbackOnly` covers renderable fallback motifs such as `rest` that should not win sample-based recognition.
