# Testing and evaluation

## Test layers

| Layer | Required coverage |
| --- | --- |
| Unit / xUnit | Paragraph/page chunking, limit checks, deterministic IDs, publication transitions, citation membership/quote checks, model schema rejection, clarification routing, retention rules, ticket transitions |
| API integration / xUnit | WebApplicationFactory + real isolated SQL Server and Qdrant; auth/ownership matrix, migrations, all mutations, validation, pagination, idempotency, concurrency, deletion and response contracts |
| Provider contracts | Real local Ollama chat/embedding, dimensions, structured output, timeout and cancellation; PdfPig extraction fixtures; Qdrant filtered upsert/search/delete |
| Frontend component | Composer IME/keyboard behavior, feedback retry, source focus, ticket validation, progress polling cleanup, expired-session recovery |
| Browser / Playwright | Guest chat, account history, source drawer, clarification follow-up, ticket review/submit, admin upload/publish/replace, responsive layout, keyboard-only journeys |
| Accessibility | Automated axe checks plus manual screen reader, focus, zoom/reflow, contrast, touch-target and reduced-motion checks |
| Reliability/security | Dependency outage, worker crash, expired lease, partial indexing, SQL/Qdrant divergence, cancellation races, injection, XSS, IDOR and revoked publication |
| RAG evaluation | Real corpus and real models using the 30-case fixture; manual answer/source grading with saved evidence |

Use synthetic test accounts and separate collections/databases per test run. Tests must never delete non-test volumes. Mock AI only in deterministic application/API tests; clearly label mocked results. Real model evaluation is a separate job and is required before claiming AI quality.

## Evaluation protocol

1. Import canonical knowledge fixtures as separate TXT documents with stable passage keys; also generate equivalent text-based PDFs during implementation to verify page provenance. Confirm extracted text matches expected facts before benchmarking retrieval.
2. Use cases 01–20 for development and threshold tuning. Keep 21–30 untouched until a candidate configuration is frozen. Since the holdout is small, report split results and avoid general accuracy claims. Add a fresh private set before any broader claims.
3. Freeze corpus hashes, model digests, embedding dimensions, prompt/chunker/retrieval versions and hardware. Run once cold and three times warm per case, sequentially for quality, then separately at five active users for load behavior. Reset conversation per case unless the case specifies multiple turns.
4. Capture question, expected behavior, candidates, final cited IDs, final answer, latency stages, error and request IDs. Keep raw outputs as development artifacts with synthetic data.
5. Human reviewer grades factual correctness and support. A second reviewer checks disputed outcomes. An optional model judge supplements, never replaces, source inspection. Do not treat exact-string matching as semantic accuracy.
6. Record failures and improvements. Never silently remove hard questions or count abstention as correct for answerable questions.

## Metrics and provisional gates

| Metric | Definition | Initial gate |
| --- | --- | --- |
| Overall pass | Cases with required behavior AND all required facts AND no unsupported claims / all 30 cases | >= 27/30 on a frozen candidate |
| Answerable accuracy | Correct answerable cases / answerable cases; report count and category | >= 90% |
| Citation precision | Supporting claim-citation pairs / all emitted claim-citation pairs, manually checked | >= 95%; no fabricated citation IDs |
| Citation coverage | Supported policy claims with an adequate citation / all policy claims | 100% on release set |
| Retrieval recall@6 | Gold required passages found in selected top six / total required gold passages, before generation | >= 90% |
| Unknown/escalation accuracy | Correct abstention or escalation on designated cases / those cases | >= 90%; fabricated live actions = release blocker |
| Clarification quality | Clarification cases asking relevant missing information and handling the follow-up / clarification cases | All designated cases pass |
| Latency | End-to-end request acceptance to validated saved result; p50/p95, cold/warm separately | Warm p95 <= 15 s under documented hardware/profile |
| Retrieval latency | Embed query excluded: Qdrant request + authorized passage resolution; report embedding separately | Warm p95 <= 500 ms |
| Failure rate | Failed infrastructure requests / all requests, distinct from deliberate abstention | 0 in standard local demo run |

With a small set, a single failure changes percentages substantially. Show numerators and denominators and all individual outcomes. For multi-turn cases, grade the whole interaction; record per-turn timings and final interaction latency. In quality runs, deadline is initially 60 seconds per turn; warm speed targets can still fail below that timeout.

## Required failure experiments

- Kill API/worker after extraction, midway through embeddings, after Qdrant upsert and before SQL completion; verify safe resume and no duplicate passages.
- Publish/archive while a chat is generating; ensure stale knowledge cannot be committed as current.
- Timeout generation, retry the same request ID, submit ticket twice and edit feedback rapidly; verify one durable result each.
- Lose network during polling then reload; recover status and preserve input. Stop a turn while completion races; exactly one terminal outcome.
- Delete a document and fail vector cleanup; retrieval/source endpoints still deny it immediately.
- Run tests using two different owners and an admin; attempt access to each other's UUIDs on every resource route.
- Use damaged, encrypted, textless, mixed scanned/text, oversized, multicolumn and misleading-extension uploads.
- Return malformed JSON, fabricated chunk IDs, quote mismatch, unsupported numeric claims and overlong output from a fake provider; safe fallback is required.

## Release definition

Builds, meaningful tests and security checks pass; clean setup succeeds from documented prerequisites; no unresolved critical/high-severity defects; all required UX states implemented; no placeholder metrics or false sent-email claims; measured evaluation report attached; fresh-clone migration and persistence/recovery rehearsal complete. A failed numeric gate requires fixing or an explicitly documented scope/target decision before release, not changing reported results.
