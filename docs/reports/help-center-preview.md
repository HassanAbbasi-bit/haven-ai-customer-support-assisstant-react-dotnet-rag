# Help center preview verification — 19 October 2026

This delivery starts the customer interface; it does not complete M1 or the support assistant. The existing roadmap remains the release plan. The read-only fixture browser is an independently usable preview while provider compatibility and secure persistence are finished.

## Implemented

- Responsive React help center, topic navigation, keyword search, no-results recovery, loading/error/retry states and device-local policy bookmarks.
- Native modal source reader with exact canonical passages, stable passage IDs, version and effective date. Escape closes it and returns focus. Clipboard failure has an actionable message.
- Public `GET /api/v1/policies` serves five fictional demo documents and 13 passages. The fixture is embedded at build time; no arbitrary file paths or uploaded/private documents are exposed. This endpoint is not an ingestion, publication, retrieval, or AI implementation.
- Keyboard focus handling, mobile navigation focus containment, background inertness, skip link, reduced-motion support and readable touch targets.
- `scripts/Start-Local.ps1` provides Services, Api, Web and Verify modes without exposing SQL credentials or replacing an existing .env.
- Repaired the SQL image digest after Docker rejected the previous pin. The tag resolves to `sha256:d01cc45e6b920eff17abc60295b8748821e09b678f0fcf54959ef37406b80203`; Qdrant's existing pin resolves correctly. This was verified using `docker buildx imagetools inspect`.

## Verification

- Release .NET build: passed, zero warnings/errors.
- Backend tests: 5 unit + 3 API integration tests passed. New coverage checks fixture count, unique passage IDs, versions/dates, preservation of return restrictions and product-care negations.
- Frontend strict type-check, production build, ESLint and formatting: passed.
- `dotnet format --verify-no-changes --no-restore`: passed.
- Browser checks: multiword search, no results and recovery, source dialog content/Escape/focus return, save/saved view/remove/empty state, mobile navigation and focus return passed in the in-app browser.
- A same-origin iframe harness checked 320/375/768/1024/1440 CSS-pixel widths. All five loaded five guides. Initial illustration overflow was found and corrected using clipping on the decorative hero. Final document scroll widths were 305/360/753/1009/1425 respectively (15 px vertical scrollbar); no horizontal document overflow remained. Decorative bounds beyond the hero are clipped intentionally.
- Screenshots: [help center](ui-evidence/help-center.png). This is a narrow preview, not a complete accessibility audit.

Two observed interface defects were fixed during checks: mobile navigation focused its trigger while the background was still inert, and decorative hero elements extended the document width. Focus restoration now runs after the closed state commits.

## Docker and remaining work

Docker 29.8.2 and Compose 5.6.0 are installed; the engine reports Linux containers. User accepted the SQL Developer license and moved Docker's disk to `H:\Docker Disk Image\DockerDesktopWSL`. A random local SQL secret was initialized in ignored `deploy/.env`; it was not printed. The first pull failed on the old SQL digest; the repaired pull is in progress. Do not claim a successful SQL/Qdrant round trip until the images finish downloading and the provider checks pass.

Native Ollama remains the configured model host. The API and Vite currently run on the host; Docker manages SQL and Qdrant. A Docker-only full-stack launch is still planned. Accounts/JWT/guest ownership, SQL business schema, uploads/publication, durable RAG chat, tickets, persisted feedback and admin tools remain unimplemented. This browser deliberately makes no AI-generated claims or submitted-ticket promises. No usability study, full WCAG audit, RAG accuracy benchmark, persistence/restart rehearsal or production-readiness claim is made.

## Current real-provider result

The 19 October provider run passed PDF extraction, real Ollama structured chat/cancellation, and real 384-dimensional embeddings/cancellation. SQL and Qdrant were blocked while their containers were still downloading. [Raw provider evidence](ui-evidence/providers.json). The first Verify attempt was blocked by sandboxed NuGet vulnerability-feed access; an approved locked restore passed, and the runner was retried with `--no-restore`. No vulnerability warning was suppressed. Frontend final rebuild/lint/format and Start-Local script syntax checks also passed.
