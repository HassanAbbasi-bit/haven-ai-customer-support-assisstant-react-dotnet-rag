# Risks and decisions

## Decisions already made

| Decision | Reason |
| --- | --- |
| Modular monolith and SQL-leased jobs | Keep prototype manageable while making uploads/chat restart-safe |
| SQL owns visibility and exact source text | Protect citations and version authorization from vector-store drift |
| Validated complete responses with progress polling | Avoid displaying unsupported draft tokens; allow reconnect recovery |
| Explicit JWT implementation with Identity | Meet the requested JWT requirement without confusing Identity bearer tokens with JWT |
| Single business and public policy corpus | Deliver a credible focused prototype before tenancy complexity |
| Local ticket records, explicit review | Provide useful escalation without pretending to send email or execute business actions |
| No OCR in v1 | Make extraction limits honest and testable |
| Metrics are targets until measured | Prevent misleading accuracy, speed and usability claims |

## Open implementation experiments

### Resumed implementation decision, 24 October 2026

The earlier free-form structured generation omitted conditions and sometimes produced invalid quotations. The current local profile uses Ollama through Microsoft.Extensions.AI to select evidence IDs, then displays the complete stored policy passages with exact citations. This favors faithful wording over conversational brevity; relevance, interpretation and completeness remain separate quality gates. V3's canonical run exposed metadata-only selections, motivating a stricter V4 selection prompt. Both runs must remain visible, and the original cases 21–30 are now development data after inspection/tuning; they cannot support a fresh holdout accuracy claim. A new independent holdout is required.

SQL Server/Qdrant are running through Docker, with live SQL ownership tests passing. Historical statements below about Docker being unavailable describe the earlier inspection. Cold Ollama loading exceeded a request deadline once; `scripts/Warm-LocalAi.ps1` provides an explicit startup probe without increasing every chat deadline.

| Question | Owner / deadline | Resolution evidence |
| --- | --- | --- |
| Exact model pair, size and quantization? | Implementer / M0 | Real hardware benchmark, schema and embedding tests, license |
| Adapter and package versions compatible with .NET 10? | Implementer / M0 | Locked build and real provider contract test |
| Can this machine run SQL, Qdrant and models concurrently? | Implementer / M0 | Resource measurement and warm/cold load run |
| Best chunk size, topK and abstention threshold? | Implementer / M5 | Development-set comparison with frozen holdout |
| Is keyword fusion needed? | Implementer / M5 | Measurable recall failures on exact terms and product codes |
| Does PDF reading order preserve policies? | Implementer / M2 | Visual/text review of multicolumn and table fixtures |
| Are performance targets realistic? | Implementer / M5 | Full measured report; revise scope openly if needed |

M0 update: pinned packages now compile and real Ollama chat/embedding/PDF checks pass; see [verification record](reports/m0-verification.md). Native llama3.2:3b Q4_K_M and all-minilm:22m are provisional spike choices with recorded digests. Final model selection and simultaneous SQL/Qdrant/model load remain unresolved. Docker is unavailable locally; SQL/Qdrant live checks remain blocked. No performance target has been claimed.

## Risk register

| Risk | Mitigation and release response |
| --- | --- |
| Hallucinated claim with real citation | Claim/quote checks plus human semantic grading; unsupported material claim blocks the affected release case |
| Old/private policy leaks | SQL allowlist, output revalidation, citation checks and concurrent publish tests; any leak blocks release |
| Partial index becomes searchable | Explicit ready/publish boundaries, durable jobs, deterministic points, reconciliation |
| Slow local CPU model | Hardware spike first, bounded concurrency/context, honest progress, measured model fallback |
| Scanned or badly ordered extraction | Preview, warnings, hard rejection for missing material text, text upload recovery |
| Scope overwhelms internship timeline | Complete one real vertical slice first; defer optional hybrid/streaming polish rather than auth or safety |
| Weak UI despite working API | Explicit screen/state spec, accessibility tests, real task study, fix observed friction |
| False sense of quality from 30 cases | Split tuning/holdout, report all raw counts, repeat runs and add fresh cases before broader claims |
| Data loss across services | SQL as authority, backup/restore rehearsal, reindex procedure and published revision manifests |
| Unsafe public exposure | Default private supporting services; separate public readiness tasks |

Zero bugs and zero model mistakes cannot be guaranteed. Acceptance gates reduce risk and make residual limitations visible; they do not prove perfection. Do not mark a task completed or a target met without linked evidence.
