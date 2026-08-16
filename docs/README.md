# DeltaZulu.LogCluster — documentation

Architecture, Decisions, Constraints and roadmaps for the DeltaZulu estate live
in **[`DeltaZulu-OU/docs`](https://github.com/DeltaZulu-OU/docs)**, not here.

| Looking for | Go to |
|---|---|
| Decisions governing this repository | [`architecture/GOVERNING-DECISIONS.md`](https://github.com/DeltaZulu-OU/docs/blob/main/architecture/GOVERNING-DECISIONS.md) |
| The estate-wide pipeline architecture | [`architecture/PIPELINE.md`](https://github.com/DeltaZulu-OU/docs/blob/main/architecture/PIPELINE.md) — read with `PIPELINE-ERRATA.md` |
| Facts the estate does not control | [`constraints/`](https://github.com/DeltaZulu-OU/docs/tree/main/constraints) |
| This repository's historical ADRs | [`archive/DeltaZulu.LogCluster/`](https://github.com/DeltaZulu-OU/docs/tree/main/archive/DeltaZulu.LogCluster) |
| Roadmaps | [`roadmaps/`](https://github.com/DeltaZulu-OU/docs/tree/main/roadmaps) |
| Verification evidence | [`reports/`](https://github.com/DeltaZulu-OU/docs/tree/main/reports) |

Decisions are numbered globally across the estate. The per-repository scheme this
replaced produced collisions that citations could not resolve — `DeltaZulu.Agent`
ADR 0014 and `DeltaZulu.Platform` ADR 0014 decide opposite things, and the Agent
carried two different ADR 0003 documents, so "ADR 0003" did not resolve even
within one repository.

## What remains here

- `ARCHITECTURE.md`, `CLI.md`, `COMPARISON.md`, `DEVELOPMENT.md`.

`ROADMAP.md` has moved to the docs repository. Two corrections were attached to
it there: it proposes a `LiblognormParserDescriptor` type where Parse shipped
`ParserDescriptor`, and dropping the `v2-iptables` or `cisco-interface-spec`
motifs costs 20 cases of committed parity coverage rather than the 1 an earlier
undercount suggested.
