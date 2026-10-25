> Current status, 24 October 2026: functional local implementation recovered after interruption. See [implementation and verification](reports/functional-implementation.md). The original detailed milestone checkboxes below are a planning baseline and have not yet been reconciled individually; they must not be read as current feature status.

# Implementation roadmap

M0 provider integration and the core M1–M4 application flows are implemented, with remaining acceptance checks tracked in the functional report. M5 evaluation is in progress; M6 operational and release gates remain incomplete. The previous generative answer profile failed quality checks; the revised extractive profile requires a new measured run. The historical M0 report records earlier blockers, many of which have since been resolved. Time ranges below are original planning estimates, not delivery promises.

## M0 — Verify the stack and establish the repository (2–3 days)

- [ ] Check Docker Desktop/Compose and Linux containers, .NET 10, compatible Node, SQL Server platform support, RAM/disk/GPU resources and model licensing.
- [ ] Spike Microsoft.Extensions.AI + Ollama structured chat and embeddings; verify cancellation, dimensions, context limits and model options.
- [ ] Spike SQL migration, Qdrant filtered search, and PdfPig page extraction.
- [ ] Select model/hardware profile; record exact versions and limits in an architecture decision.
- [x] Scaffold planned solution/frontend/test layout, lock dependencies, strict TypeScript, lint/format settings and CI configuration. Local builds/tests are verified; hosted CI has not run.
- [x] Add `.gitignore`, safe configuration example, startup validation and development secret instructions.

Partial evidence for the unchecked tasks: tools/hardware were inspected; Docker is unavailable so Linux-container mode is unverified. PDF extraction, real structured chat/embeddings and cancellation pass, and the minimal EF migration is generated. Live SQL/Qdrant checks are blocked. Model choices are provisional, with observed digests recorded; full context-limit/quality/load testing remains open. Do not treat generated migration code as a live SQL pass.

Exit: reproducible builds and one real round trip for each provider. Do not build the whole interface before provider compatibility is proven.

## M1 — Establish the usable interface and secure persistence (3–5 days)

- [ ] Build chat/source/ticket/admin wireframes using the UI specification, including mobile and failure states.
- [ ] Implement shared accessible controls, navigation, focus primitives and visual tokens.
- [ ] Create EF schema/migrations, Identity, guest sessions, JWT validation/refresh, roles and secure admin bootstrap.
- [ ] Implement conversation ownership/history, ProblemDetails, request correlation, pagination and API contract generation.
- [ ] Write authorization and idempotency integration tests before adding sensitive administration.

Exit: guest and customer can use owned history; admin routes enforce role; two-owner isolation proven; core screen shell usable by keyboard.

## M2 — Deliver the first complete supported answer (4–6 days)

- [ ] Add private PDF/TXT storage, validation, durable jobs and page-aware extraction previews.
- [ ] Chunk/embed/index with deterministic IDs and model-specific collections; publish explicit ready versions.
- [ ] Implement current-version retrieval, evidence prompt, structured output and citation validation.
- [ ] Implement durable chat request/polling, progress, timeouts and source detail.
- [ ] Upload Returns Policy and complete question -> answer -> exact source -> feedback-ready UI flow.

Exit: the first vertical slice uses real Ollama, SQL and Qdrant; source text matches stored passage; unknown response works; reloading does not lose durable state.

## M3 — Complete knowledge and customer help flows (4–6 days)

- [ ] Add remaining fixture documents; detect missing/scanned pages and surface extraction warnings.
- [ ] Implement replacement, archive/delete, corpus revision validation, canceled/retried jobs and cleanup reconciliation.
- [ ] Implement ambiguity resolution, bounded history, human-help routing and safe generation failures.
- [ ] Implement reviewed ticket submission, customer ticket list, persisted feedback and admin ticket/feedback views.
- [ ] Test duplicates, concurrent edits, withdrawn citations and forbidden accesses.

Exit: all core feature acceptance criteria FR-01 through FR-11 pass with real persistence.

## M4 — Reliability, operations and security (3–5 days)

- [ ] Add service health/readiness, queue limits, backoff, structured redacted telemetry and actual dashboard metrics.
- [ ] Add Compose profiles, model provisioning script, health-dependent initialization and explicit migrations.
- [ ] Implement retention, deletion reconciliation, backup/restore scripts and safe admin audit.
- [ ] Run outage, restart, injection, upload abuse, JWT/CSRF/ownership and cancellation-race tests.
- [ ] Verify clean setup on documented environment and actual persistent volumes after restart.

Exit: recoverable failures stay understandable; no known critical access defect; setup and restore rehearsal documented.

## M5 — Measure quality and polish the experience (4–6 days)

- [ ] Build evaluation runner and per-case artifact output; run development set, tune, freeze configuration, then run holdout.
- [ ] Grade answers and citations manually; record all errors and latency distributions in the report.
- [ ] Complete Playwright/accessibility/mobile coverage and five-person task study.
- [ ] Fix observed friction, dead ends, layout overflow, errors, slow-state messaging and unsupported answers.
- [ ] Check all release gates and close coverage checklist with links to evidence.

Exit: measured report meets gates; visual and interaction QA complete; unresolved limitations explicitly documented.

## M6 — Package the internship demonstration (1–2 days)

- [ ] Update README with real setup commands and screenshots only after code exists.
- [ ] Prepare a short demo: supported answer/source; clarification; unknown/live action; reviewed ticket; upload/publish; quality report.
- [ ] Include architecture diagram, known limitations, test command list, result artifacts and a resettable synthetic demo seed.
- [ ] Rehearse without internet dependency after images/models are provisioned.

Exit: a reviewer can reproduce the demonstration and distinguish measured capability from future plans.

## Definition of done for each feature

Acceptance behavior implemented; server validation/authorization enforced; meaningful tests cover the failure paths; keyboard/mobile/loading/empty/error states checked; telemetry contains no sensitive content; contracts and documentation agree; no unexplained placeholders or swallowed errors. A feature is not done solely because its success path works once.
