# Data and API contracts

## Relational model

Use UTC `datetimeoffset` timestamps, GUID primary keys, foreign keys, required maximum lengths, rowversion on mutable admin records, and explicit indexes. Customer-visible times localize in the browser. SQL is authoritative for ownership, publication and stored source text.

| Entity | Essential fields and constraints |
| --- | --- |
| Identity users/roles | Standard Identity tables; Customer/Admin roles; no default production password |
| GuestSession | Hashed opaque session secret, expiry, creation, revocation; never accept a client-chosen owner |
| Conversation | UserId XOR GuestSessionId, title, creation/update/deletion; owner index |
| ChatRequest | ConversationId, client request UUID, state/stage, lease, attempt, cancel flag, error code, corpus revision, model/prompt versions, timings; unique conversation/client request UUID |
| Message | ConversationId, request ID, sequence, role, content, response kind; unique request/role and conversation/sequence |
| Citation | AssistantMessageId, ChunkId, claim index, stored quote and version metadata; do not rely on a live Qdrant record to render citations |
| Document | Logical title, category, visibility, active version ID, rowversion, archived/deleted timestamp |
| DocumentVersion | DocumentId, version label, effective date, SHA-256, safe storage key, MIME, byte count, ingestion state, extraction warning, model/chunker versions, collection, publication timestamp |
| Chunk | VersionId, ordinal, stable source key, page range or TXT line range, heading, exact text, text hash, Qdrant point ID; unique version/ordinal |
| IngestionJob | VersionId, stage, attempts, lease owner/expiry, progress counts, safe failure code, retry time; unique active job per version |
| CorpusRevision | Monotonic revision incremented with publication/archive/delete changes |
| Ticket | Owner, conversation/message context reference, reference number, subject, editable summary, optional order reference, contact email, status, sharing consent timestamp, rowversion |
| Feedback | MessageId, owner, helpful boolean, reason enum, optional bounded comment, timestamp; unique assistant message/owner |
| RefreshSession | UserId, hashed refresh secret, family ID, expiry, revocation/reuse marker |
| AuditEvent | Actor, operation, resource ID, outcome, timestamp, trace ID; exclude passwords, tokens and raw chat text |

Keep document extraction state (`queued`, `extracting`, `chunking`, `embedding`, `indexing`, `ready`, `failed`, `canceled`) separate from publication (`draft`, `published`, `superseded`, `archived`, `deleted`). Ready does not automatically mean customer-visible. Tickets use `open`, `in_progress`, `resolved`, `closed`; drafts exist only in the browser until explicit submission.

## API conventions

Base `/api/v1`; JSON camelCase, UUID IDs, ISO 8601 timestamps. Paginate lists with opaque cursors and capped page size <= 100. Use OpenAPI as a generated contract and check frontend types for drift in CI. Errors use ProblemDetails with stable `code`, safe `detail`, `traceId`, and field errors. Never send stack traces.

| Endpoint | Access and semantics |
| --- | --- |
| POST `/auth/guest-session` | Rate-limited public; creates secure opaque guest cookie |
| POST `/auth/register`, `/auth/login` | Rate-limited public; Identity validation; generic login failure |
| POST `/auth/refresh`, `/auth/logout` | Refresh cookie with CSRF defenses; rotate/revoke |
| GET `/auth/me` | Authenticated customer/admin identity |
| POST `/conversations` | Guest/customer; creates owned conversation |
| GET `/conversations`, GET `/conversations/{id}` | Owner only; paginated history/messages |
| DELETE `/conversations/{id}` | Owner only; tombstone immediately; purge asynchronously |
| POST `/conversations/{id}/requests` | Owner; `{clientRequestId, message}`; 202 + request/status URL; one active request per conversation |
| GET `/conversations/{id}/requests/{requestId}` | Owner; current stage or complete answer payload |
| POST `/conversations/{id}/requests/{requestId}/cancel` | Owner; idempotent cancellation request |
| GET `/messages/{id}/citations/{citationId}` | Message owner/admin with permitted support purpose; returns exact authorized passage |
| PUT `/messages/{id}/feedback` | Owner; create/replace vote and optional reason/comment |
| POST `/tickets` | Owner; explicit reviewed content, consent, idempotency key; 201 + ticket reference |
| GET `/tickets`, GET `/tickets/{id}` | Owner; customers see only their tickets |
| GET `/admin/documents` | Admin; filters, warnings, status, versions |
| POST `/admin/documents` | Admin multipart PDF/TXT + title/category/version/effective date; 202 with ingestion job |
| GET `/admin/documents/{id}/versions/{versionId}` | Admin; status and extracted passage preview |
| GET `/admin/ingestion-jobs/{id}` | Admin; durable progress and safe error detail |
| POST `/admin/versions/{id}/retry` | Admin; new attempt only if terminal failed/canceled |
| POST `/admin/versions/{id}/cancel` | Admin; idempotent cancellation |
| POST `/admin/versions/{id}/publish` | Admin; requires ready, If-Match, effective-date rules and complete index verification |
| POST `/admin/documents/{id}/archive` | Admin; If-Match; immediately excludes from retrieval |
| DELETE `/admin/documents/{id}` | Admin; If-Match; logical deletion first, cleanup job later |
| GET `/admin/tickets`, PATCH `/admin/tickets/{id}` | Admin; allowed state transitions, If-Match |
| GET `/admin/feedback`, GET `/admin/metrics` | Admin; date range, pagination, counts/denominators |
| GET `/health/live`, GET `/health/ready` | Minimal public status; dependency details restricted |

Citation details are served from SQL and checked against current visibility/publication and message ownership. After withdrawal, return a safe “Source no longer available” response and retain only authorized historical metadata. The UI must not present a withdrawn source as current policy. A new answer always uses current published evidence.

## Completed answer shape

```json
{
  "requestId": "uuid",
  "state": "completed",
  "messageId": "uuid",
  "kind": "answered",
  "text": "Opened, non-defective items cannot be returned under this policy.",
  "claims": [{"text": "Opened, non-defective items cannot be returned.", "citationIds": ["uuid"]}],
  "citations": [{"id": "uuid", "documentTitle": "Returns Policy", "version": "1.0", "page": null, "sourceKey": "RET-02"}],
  "suggestedActions": ["draft_ticket"],
  "durationMs": 3400
}
```

`kind` is `answered`, `clarification`, `not_found`, or `human_help`. Dependency failures use request `state=failed` and a code; they are not `not_found`. Terminal request states are `completed`, `failed`, and `canceled`. Durations are measured, never generated by the model. Citation identifiers and metadata are resolved by the server from allowed chunks.

## Concurrency and retries

Same idempotency key + same payload returns the existing resource; same key + different payload gives 409. Retain mutation keys for at least 24 hours; preserve chat client IDs for conversation lifetime. If-Match mismatch gives 412. A second active question gives 409 with the existing request ID. Requests exceeding configured queue capacity give 429/503 with Retry-After.

Use database transactions for request/message/result writes. Publish active-version pointer plus corpus revision atomically in SQL after Qdrant verification. Indexing retries use deterministic point IDs. A canceled request cannot overwrite a terminal completed result; define terminal transition with an atomic compare-and-set. Support worker recovery tests for both sides of each commit boundary.

## Validation defaults

Question 1–4,000 characters; title <= 160; ticket subject <= 160; ticket summary <= 4,000; feedback comment <= 1,000. Files <= 20 MiB and PDFs <= 200 pages; extracted text <= 1 million characters per upload. These are configurable caps validated before expensive work. Page and character limits are also enforced inside the extraction worker.
