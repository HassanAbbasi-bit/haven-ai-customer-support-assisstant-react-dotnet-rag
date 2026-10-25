# Requirement coverage checklist

Current implementation and measured results are in the [functional report](reports/functional-implementation.md), updated 24 October 2026. Core application features are implemented and 27 backend tests pass. The table below retains the planned evidence requirements; it is not a claim that every verification gate is complete. In particular, revised RAG semantic quality, full resilience, retention, accessibility and production operation remain open.

| Requirement | Planned specification | Verification evidence to add |
| --- | --- | --- |
| React/Vite/Tailwind frontend | 02 architecture; 05 UX | Build, lockfile, responsive browser checks |
| ASP.NET Core/C# API | 02 architecture; 03 contracts | Build and API integration suite |
| Microsoft.Extensions.AI + Ollama | 02 architecture; 04 RAG | Real chat/embedding contract tests and digests |
| Qdrant vectors | 04 RAG | Filtered search, reindex and delete tests |
| SQL Server/EF Core | 03 data; 09 operations | Migrations, persistence and restore tests |
| PdfPig PDF/TXT upload | 04 ingestion | Valid, malformed, textless and multicolumn fixtures |
| Identity + JWT | 06 security | Auth, refresh, expiry, revocation and role tests |
| xUnit/API integration | 07 testing | CI test artifacts using real SQL/Qdrant |
| Docker Compose | 09 operations | Clean setup and restart rehearsal |
| RAG answers + source passages | 04 RAG; 05 sources | Claim/source grading and source drawer checks |
| Clarification and unknown handling | 01 product; 04 RAG | Multi-turn and unsupported evaluation cases |
| Human ticket draft | 03 contracts; 05 ticket UX | Review/consent/submit/idempotency browser test |
| Feedback and dashboard | 01 product; 03 contracts | Persisted votes, access checks, real denominators |
| Impressive usable UX | 05 UX | Screenshots, task study, keyboard/mobile accessibility results |
| 20–30 test questions | fixtures/evaluation-cases.md | 30 cases executed, individual outcomes saved |
| Accuracy/source correctness/time | 07 testing; report template | Measured split report and raw artifacts |
| Error handling and reliability | 07 testing; 09 operations | Outage/crash/cancel/reload and cleanup results |
| Privacy and injection protection | 06 security | Ownership, XSS, malicious evidence and retention tests |

## Final delivery review

- [ ] Every required feature has working implementation and evidence.
- [ ] No unresolved critical security or data-loss defects.
- [ ] All documented quality gates have actual results.
- [ ] Setup instructions reflect tested commands and pinned versions.
- [ ] No fabricated numbers, fake integrations, exposed secrets or inaccessible actions.
- [ ] Known limitations and deferred items are easy to find.
- [ ] Demo seed uses only synthetic business/customer data.
- [ ] Sources remain traceable through updates, withdrawal and retained history rules.
