# Functional implementation — resumed 24 October 2026

This report supersedes the implementation status in the historical M0 and help-center preview reports. The numbered specifications remain the acceptance criteria; implemented code does not imply that every release gate has passed.

## Recovered work

The interruption occurred during revision of the RAG answer profile. The earlier generative development evaluation found incorrect eligibility wording, missing qualifications, and invalid citations. Its raw results remain in `functional-evidence/baseline-generation-development.json`. They are failure evidence, not a passing accuracy report.

The current `extractive-source-selection-v4` profile asks Ollama to select relevant source IDs and returns the full stored policy text for each selection. Exact stored citations and numeric containment are checked server-side. V3 incorrectly answered a discount-code question with a document header; V4 returns an explicit unknown for that case. This limits unsupported model wording; it does not establish semantic relevance, completeness, or prompt-injection resistance.

## Implemented functionality

| Requirements | Implementation | Remaining verification or scope |
| --- | --- | --- |
| FR-01, FR-02, FR-10 | Protected PDF/TXT upload, PdfPig page extraction, bounded deterministic chunks, durable indexing jobs, Qdrant verification, review, explicit publication, replacement, withdrawal and audit records | Complex PDFs, crash recovery and physical purge/reconciliation need broader tests |
| FR-03–FR-06 | Ollama embeddings through Microsoft.Extensions.AI, active-version Qdrant search, durable chat, extractive answers, stored citations, basic clarification and unsupported-action handling | Revised profile needs canonical semantic grading; general ambiguity remains limited |
| FR-07, FR-11 | Reviewed editable ticket draft, consent, SQL persistence, idempotency, owner isolation and admin status concurrency | Local cases only; no email or external helpdesk delivery |
| FR-08, FR-12 | Owned feedback upsert, optional comment/reason and real 30-day activity metrics with sample sizes | Usability and load testing remain |
| FR-09 | Identity customer registration, administrator role enforcement, guest sessions, JWT, refresh rotation and logout revocation | Production HTTPS, credential recovery and security review remain |
| FR-13 | Resumable canonical runner, expected facts, source keys, stored-quote checks, timing and optional retrieval preview | Completion alone is not semantic accuracy; revised profile requires measured grading |
| FR-14 | Specific errors, durable leases, cancellation, progress polling, ownership and idempotency checks | Full outage/restart/cancel race rehearsals remain |

This resumed session also added **Test published knowledge** to the admin Knowledge screen. It submits a question to the protected retrieval-preview endpoint and shows ranked stored passages and PDF pages. A real protected API check returned two Returns Policy passages. The updated frontend passes build, lint and formatting; visual interaction verification remains blocked as described below.

Main code: `src/HavenSupport.Api/SupportEndpoints.cs`, `SupportWorker.cs`, `SessionSecurity.cs`, `src/HavenSupport.Infrastructure/RagEngine.cs`, and `src/haven-support-web/src/`.

## Checks rerun on 24 October

- Release provider-independent tests: **21 passed** (17 unit, 4 API).
- Real SQL integration tests: **6 passed**, using isolated temporary databases.
- Frontend production build/typecheck: **passed**.
- Frontend lint with zero warnings: **passed**.
- Frontend formatting check: **passed**.
- Existing SQL Server and Qdrant Docker containers: **healthy**; volumes preserved.
- API readiness endpoint: **ready**.
- First live supported-answer smoke test: **failed with `answer_timeout`** during cold model startup. A separate warm-up then succeeded (about 40 seconds, including about 37 seconds loading). After warming, both V3 and V4 supported-answer smoke checks **passed**, including the opened-item prohibition, defect exception, and exact stored PDF citation. The failure is retained here rather than counted as an unknown answer.
- Added and executed `scripts/Warm-LocalAi.ps1`: **passed**. Use after reboot before the first chat; it does not download a model or change the chat deadline.
- All PowerShell scripts parsed successfully. Fixed the evaluation checkpoint writer's Windows `File.Replace` empty-backup-path error and recovered completed cases from its validated temporary checkpoint.
- Browser automation could not initialize in this resumed session: `failed to write kernel assets ... path specified`. Earlier responsive screenshots remain historical evidence; current UI changes have not received a new browser pass.

## Canonical RAG results

Raw evidence: [V3](functional-evidence/canonical-source-v3-20261024.json), [V4](functional-evidence/canonical-source-v4-20261024.json), and [V4 supported-answer smoke](functional-evidence/rag-smoke-v4-20261024.json). Both runs used the five published PDF fixtures, real Ollama, SQL and Qdrant. These are now **development runs**: cases 21–30 were inspected and used when changing the prompt, so a new independent holdout is required.

| V4 measurement | Observed result | Interpretation |
| --- | --- | --- |
| Completed durable turns | 32/32 across 30 cases | Transport/job completion, not semantic accuracy |
| Stored quote containment | 30/30 emitted citations | Exact text exists in the referenced passage; does not prove relevance |
| Candidate retrieval recall@6 | 33/37 gold source keys, 89.2% | Below the specified 90% target; missing SUP-02 in 03 and SUP-03 in 18, 24, 26 |
| Server completion p95 / maximum | 16,261 / 20,333 ms | Above the provisional 15-second p95 target; this was a development run with other checks active, not an isolated load benchmark |
| Fabricated order actions observed | None | Human-help responses did not claim cancellation, approval or live tracking |
| Discount-code regression (26) | V3 irrelevant header; V4 explicit unknown | Fixed in this run; independent robustness still unproven |

Retrieval recall uses the union of the preview's top-six candidates across turns in a case and each case's listed gold keys. The generator receives at most three bounded passages, so candidate recall does not measure final citation completeness. Latency is the saved request acceptance-to-completion duration; the failed cold-start probe is excluded and recorded separately above.

Code-agent inspection found these unresolved quality issues; independent human grading remains pending:

| Cases | Remaining issue |
| --- | --- |
| 03 | Correct defect rule, but the answer itself does not explicitly offer a reviewed ticket; the generic help control needs a new UI check |
| 04 | Correct 30-day rule, but does not explicitly interpret the customer's 45-day example |
| 13, 22 | Correct warranty facts accompanied by an unnecessary metadata-only passage; exact citation containment is insufficient as a relevance check |
| 22 | Generic first-turn warranty answer does not ask the specified product/issue clarification |
| 24 | Safe refusal to approve, but lacks a specific explanation of support assessment |
| 30 | API produces human-help and code opens an editable draft; automatic draft opening, consent and explicit submission need a fresh browser check |

No overall answer-accuracy or citation-precision percentage is claimed before the required human review. These results establish a working RAG path and expose remaining acceptance failures; they do not satisfy every release gate.

## Remaining release gates

The app is a local development demonstration. No production readiness claim is made. Outstanding work includes semantic/retrieval grading of all canonical cases, latency and concurrent load measurement, representative usability and accessibility testing, adversarial and conflicting-document tests, model digest enforcement, cursor pagination and generated API contracts, least-privilege SQL credentials (development currently uses SA), retention/deletion cleanup, backup/restore rehearsal, and production deployment/security configuration. OCR, live order actions and automatic email are outside the specified initial scope.

Use the repository README for current startup commands. Local credentials and connection secrets remain in ignored files; never include them in evidence artifacts.
