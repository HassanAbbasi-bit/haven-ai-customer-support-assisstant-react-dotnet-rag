# Operations and local setup plan

Use the [current startup commands](../README.md) and [functional verification record](reports/functional-implementation.md). Docker Compose currently runs SQL Server and Qdrant; native Ollama, the API and Vite run on the host. The full-container demo profile and broader operational capabilities below remain planned unless the functional report records evidence.

## Prerequisites and profiles

Windows: supported Docker Desktop with WSL2/Linux containers and working Compose, .NET 10 SDK, Node compatible with the selected Vite version, Git, enough free disk for SQL/model/images and measured RAM for the selected model. Check virtualization and Docker license applicability. SQL Server container architecture must match the host or use an explicitly tested supported alternative running SQL Server; do not silently replace it with SQLite.

Development profile: SQL Server, Qdrant and optional Ollama in Compose; API and Vite on host. Full demo profile: frontend reverse proxy, API/worker and all supporting services in Compose. Native GPU Ollama may be used through an explicit base URL profile if container acceleration is unsuitable. Verify host/container DNS rather than assuming localhost addresses are interchangeable.

| Service | Internal default | Persistence and exposure |
| --- | --- | --- |
| Frontend/reverse proxy | HTTP/HTTPS entry point | Public local entry only; production TLS mandatory |
| API | 8080 container port | Private behind proxy; host binding for development only |
| SQL Server | 1433 | Named data volume; localhost debug binding only; EULA acknowledgment and secret password |
| Qdrant | 6333 REST / 6334 gRPC | Named volume; internal network, optional localhost development binding |
| Ollama | 11434 | Model volume; private/local binding; no browser direct access |
| Document store | No public port | Private named volume outside web root |

## Setup sequence to implement

1. Verify prerequisites and available disk/memory. Create secrets from documented blank configuration.
2. Start supporting services; wait for real readiness, not just container running state.
3. Provision selected Ollama model digests explicitly; record licenses/download size. Check embeddings dimensions and chat schema support.
4. Apply EF migrations using a one-shot command; create Qdrant collection/indexes compatible with embedding model.
5. Bootstrap a unique admin account using an explicit developer action. Import synthetic policies through the same ingestion pipeline and publish only after verification.
6. Start API/frontend; verify liveness/readiness, sign-in, chat/source, feedback, ticket and admin upload.
7. Run deterministic tests, real-provider smoke tests and evaluation separately. Reset only isolated demo data through a clearly named guarded command.

## Configuration inventory

Required settings: SQL connection string, JWT issuer/audience/signing key and token lifetimes, allowed origins/proxy trust, Ollama base URL/chat model/embedding model/digests/dimension/context limits, Qdrant endpoints/API key/collection, private upload path, file/page/text caps, chunker settings, retrieval thresholds/topK, prompt version, worker lease/retry/concurrency, generation timeout/queue capacity, retention periods and log level. Validate mandatory values on startup; never print secrets. Document host and container examples separately.

## Timeouts and recovery defaults

Initial budgets: 60-second chat deadline, 120-second extraction limit, 15-minute ingestion job limit for supported uploads, maximum three transient ingestion attempts with jittered backoff. Provider calls use cancellation and shorter per-call budgets within the total deadline. Worker leases heartbeat and recover after expiry; use a fencing/concurrency check so an expired worker cannot finalize over a new attempt. Measure and tune these defaults for target hardware.

Liveness tests the process only. Readiness checks SQL, Qdrant collection compatibility, model availability and worker capacity without generating costly sample text each probe. Dependency outage can degrade chat while ticket/history remain available; display capability-specific status. Public health output exposes no credentials or internal addresses.

## Observability

Correlate request/trace IDs across endpoint, queue, retrieval, model and persistence. Record stage durations, completion/failure/cancel counts, queue depth, ingestion failures, selected source IDs, model/config versions, feedback and ticket counts. Separate unknown-answer rate from infrastructure error rate. Dashboard values derive from stored measurements with timeframe, sample count and percentiles; no seeded success metrics masquerading as real traffic.

## Backup, restore and deletion

Back up SQL, private documents and Qdrant snapshots to protected local storage; store configuration/model identifiers without secrets in the manifest. SQL is the source for rebuilding Qdrant. Pause publication/ingestion or establish a consistent revision during snapshots. Test restoring into fresh volumes, rebuilding vectors if necessary and opening retained source passages. Initial demo recovery objectives: <= 24 hours potential data loss with daily backups, <= 2 hours recovery on the documented machine; these remain targets until rehearsed.

Deletion removes access first, then retries physical cleanup. Monitor stuck cleanup and orphan reconciliation. Explain that backup retention can delay physical removal. Never recommend deleting all Docker volumes as a first troubleshooting step.

## Troubleshooting runbook

| Symptom | Check and action |
| --- | --- |
| Docker command missing | Install/enable supported Docker Desktop and PATH; restart terminal; check engine/WSL status |
| SQL not ready | Health logs, EULA, secret strength, memory, disk and volume permissions; preserve data |
| Model missing or dimension mismatch | Provision configured digest; rebuild a new collection for changed embeddings; never force wrong dimensions |
| Textless PDF | Explain OCR limitation; upload text-based PDF/TXT; do not retry endlessly |
| Repeated no-answer | Inspect extraction, active versions, gold retrieval and threshold; distinguish poor retrieval from true unknown |
| Slow answers | Measure cold/warm model, context length, queue and embedding contention; use tested smaller model/profile |
| Failed ingestion | Read safe stage/error, fix cause, retry job; deterministic indexing prevents duplicates |
| Stale sources | Check SQL active version/revision and authorization recheck; reconcile Qdrant; block release if reproducible |

Public deployment additionally needs managed secrets/TLS, malware scanning, verified account email/recovery, backup monitoring, incident response, capacity tests and operational ownership. The local Compose demonstration is not automatically production-ready.
