# Product requirements

## Outcome and audience

Make policy support fast, understandable, and verifiable. The primary customer wants an answer without learning how AI works. The administrator wants to publish trustworthy documents and resolve cases the assistant cannot settle. The internship reviewer needs reproducible evidence of engineering quality.

The interface is branded **Haven Support** with the subtitle “Answers from Haven Goods policies.” Disclose that the assistant uses AI and can make mistakes. Do not claim it can inspect orders or execute refunds.

## Scope and acceptance criteria

| ID | Requirement | Acceptance criterion |
| --- | --- | --- |
| FR-01 | Upload PDF/TXT | Admin can upload either format, see validation and durable progress, and recover from failure; customer cannot upload |
| FR-02 | Publish documents | Only fully indexed, explicitly published versions become searchable; stale or deleted versions are excluded |
| FR-03 | RAG chat | Question is embedded and searched against authorized active knowledge; generation receives bounded evidence |
| FR-04 | Source-backed answers | Every substantive policy claim maps to a visible source; opening it shows the stored exact passage, title, version, and page if applicable |
| FR-05 | Clarification | Ambiguous requests receive one useful question; a follow-up retains context without inventing missing facts |
| FR-06 | Unknown answers | Unsupported requests state what is unavailable and offer human help without fabricated policy |
| FR-07 | Tickets | User can edit a proposed summary, review shared details, submit once, and receive a reference; submission persists in SQL |
| FR-08 | Feedback | Thumbs up/down persists per owned assistant response; optional reason/comment can be edited without duplicate counts |
| FR-09 | Accounts and access | Anonymous chat works; registered customers see only their history; admins use protected routes and server role checks |
| FR-10 | Knowledge administration | List, inspect, retry, publish, replace, archive, and delete documents with audit records |
| FR-11 | Support administration | Admin can filter tickets, inspect permitted context, and update statuses with concurrency protection |
| FR-12 | Quality dashboard | Actual volume, helpfulness, unknown rate, failed requests, and latency include timeframe and sample size; empty data never looks like measured success |
| FR-13 | Evaluation | Reproducible runner executes 30 canonical cases and reports answer quality, source correctness, retrieval recall, and response time |
| FR-14 | Resilience | Timeouts, model outages, extraction failure, authorization errors, and cancellation have specific tested behavior |

## Customer journeys

1. Open chat, choose “Can I return an opened item?”, receive a concise answer, inspect its source, and mark helpful.
2. Ask “Can I return this?”; assistant asks whether the item is opened or defective and how long since delivery; answer the relevant detail, then receive applicable rules.
3. Ask for live delivery tracking; assistant explains it cannot access orders and offers a ticket. Review/edit the draft and contact details; explicitly submit; see the saved ticket reference.
4. Close or reload during a slow answer; reopen the conversation and see the durable request status with an appropriate retry action.
5. Use the complete flow with only a keyboard and at a narrow mobile viewport.

## Admin journey

Sign in, upload a policy, inspect extracted page text and passage boundaries, review indexing success, then publish. Preview a question against the published knowledge. Upload a replacement, compare versions and dates, publish atomically, and verify the old text is no longer retrieved. Inspect negative feedback and open a related ticket when authorized.

## Nonfunctional requirements

- Accessibility: WCAG 2.2 AA target; manual keyboard and screen reader checks supplement automated checks.
- Performance: provisional warm local p95 completed answer <= 15 seconds, search p95 <= 500 ms, and non-AI API p95 <= 500 ms under the declared benchmark; verify on target hardware. Show progress immediately.
- Reliability: all persisted mutations survive process restart; retries do not duplicate turns, indexing records, feedback, or tickets.
- Security: endpoint authorization and resource ownership enforced server-side; no secret credentials in the browser or repository.
- Maintainability: typed boundaries, migrations, reproducible builds, pinned dependencies, structured errors and logs.
- Usability gate: at least four of five representative testers complete answer/source/ticket journeys without coaching; record failures and improve them.
- Load assumption: five active chat users, one generation per conversation, two concurrent generations globally to start, one indexing worker. Benchmark queueing and reject overload with a recoverable response.

## Out of scope for initial release

OCR, voice, multilingual guarantees, live order lookup, return authorization, refund execution, payment collection, automatic email delivery, external helpdesk sync, public multi-tenant hosting, model training, and autonomous tools. Ticket submission saves a local support case; the UI must say so. Any production expansion requires a separate readiness review.
