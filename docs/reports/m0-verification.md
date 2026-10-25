# M0 verification record — 17 October 2026

**Milestone status: partial, not complete.** Repository/build foundation and real local AI/PDF compatibility checks are implemented. SQL Server and Qdrant round trips are blocked. M1 has not started. This report contains no RAG accuracy or performance claims.

## Environment inspected

| Item | Observation | Interpretation |
| --- | --- | --- |
| .NET | SDK 10.0.204, x64; ASP.NET/Core runtime 10.0.8 | Build SDK pinned in global.json; no SDK installation needed for this workspace |
| Node/npm | 24.15.0 / 11.12.1 | Compatible with the selected Vite/ESLint versions; exact engines and lockfile provided |
| Git | 2.53.0.windows.2 | Available; no commit, reset, remote change or push performed |
| OS | Windows 10 build 19045, x64 | Docker OS servicing/edition/virtualization compatibility still needs manual confirmation |
| CPU | Intel Core i5-3330 @ 3.00 GHz, four logical processors | Conservative CPU-only AI spike |
| RAM | 12,840,845,312 bytes (about 12 GiB) | Concurrent SQL/model load still needs measurement |
| GPU | NVIDIA GeForce GT 730, 2,147,483,648 bytes reported adapter RAM | No supported acceleration claim; probe explicitly requests zero GPU layers |
| Storage | C: about 4 GB free initially; about 2 GB after dependency/build work | Insufficient allowance for comfortable Docker/image setup; use a roomier disk or free space first |
| Native Ollama | 0.40.4; localhost API accessible | Selected instead of adding a second containerized server |
| Docker CLI/Compose | Not on PATH; standard all-user/per-user paths absent; no Docker service found | Not available in inspected locations; cannot prove absence from every custom location |
| Docker daemon/container mode | Cannot inspect without a reachable Docker installation | Linux-container check blocked, not passed |
| SQL Server | localhost:1433 unreachable; no SQL secret configured | Live connection, migration application and insert/read cleanup blocked |
| Qdrant | localhost:6333 and :6334 unreachable | Live collection/upsert/filter/cleanup blocked |
| WSL | `wsl.exe` discoverable | Installed WSL distribution/version/virtualization functionality not verified |

The first hardware query was denied by the sandbox; the approved read outside it returned the hardware above. Normal NuGet/npm network/cache access also failed; approved restores/installs were used. Missing services are different from permission-denied inspection. No AGENTS.md instructions were found in the repository or checked ancestor locations. The preexisting specifications and `Name-On-Github.txt` were preserved.

## Created foundation

- Seven .NET projects and solution: API, Application, Domain, Infrastructure, UnitTests, IntegrationTests and Evaluation, all net10.0. Production references follow Domain <- Application <- Infrastructure, with API composing Application/Infrastructure. Domain has no provider dependencies.
- Strict TypeScript React/Vite/Tailwind frontend; honest M0 page; localhost Vite proxy; no fake business actions or dashboard statistics.
- Central NuGet package pins, seven NuGet lockfiles, npm lockfile, SDK/tool pins, formatting/lint configuration, safe ignores, and basic backend/frontend CI configuration.
- Startup validation for provider options, minimal ProblemDetails setup, liveness/status endpoints and deliberately unready 503 readiness endpoint.
- A minimal EF migration for the FoundationProbes table and matching snapshot/design-time factory; no authentication or business tables.
- Developer-only provider runner with independent passed/failed/blocked results and nonzero exit for blockers. No diagnostic route is exposed through the API.
- SQL Server/Qdrant Compose services with named volumes, localhost ports and immutable registry digests. No images were downloaded or containers started in this session.
- Safe random local-secret initializer that refuses to overwrite configuration; connection setup and environment/provider check scripts.

## Verified dependency choices

| Dependency | Pin |
| --- | --- |
| EF Core SQL Server / Design / local dotnet-ef | 10.0.13 |
| Microsoft.Extensions.AI | 10.11.0 |
| OllamaSharp | 5.5.0 |
| Qdrant.Client | 1.19.0 |
| PdfPig | 0.1.16 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 |
| xUnit / test SDK | 2.9.3 / 17.14.1 |
| System.Security.Cryptography.Pkcs | Explicit patched override 10.0.13 |
| React / React DOM | 19.3.1 |
| Vite / React plugin | 8.3.4 / 6.1.2 |
| TypeScript | 5.9.3 |
| Tailwind / Tailwind Vite plugin | 4.3.3 |
| ESLint / @eslint/js / typescript-eslint | 10.13.0 / 10.0.1 / 8.71.2 |
| Prettier | 3.9.10 |

Exact transitive versions/hashes are in lockfiles. The initial ESLint 9 pin was replaced after npm reported it unsupported. The first NuGet restore rejected vulnerable transitive PKCS 9.0.11 (moderate/high advisories); an explicit 10.0.13 override resolved the audit without suppressing warnings. Final NuGet audit reported no vulnerable packages in all seven projects using the current feed. npm installation reported zero known vulnerabilities. These are point-in-time package audits, not complete security reviews; host runtimes and images need independent servicing/scanning before deployment.

Container registry manifests were queried without downloading image layers. SQL Server is `2022-CU24-GDR2-ubuntu-22.04`; Qdrant is `v1.19.0`. Their resolved immutable digests are in `deploy/compose.yaml`. Image startup, Linux mode, health checks and volume persistence remain unverified.

## Actual verification results

Saved evidence: [final provider results](m0-evidence/providers-final.json), [successful chat retry](m0-evidence/chat-retry.json), [final test counters](m0-evidence/tests-final.json), and [generated SQL migration script](m0-evidence/initial-foundation.sql). The final combined provider command exited **2**, correctly indicating the two blocked services despite three passing provider checks. A clean `npm ci` and the subsequent type-check, lint, formatting and production build also passed. Compose/workflow YAML was parsed by Prettier; Docker's own Compose validation is blocked until Docker is available.

| Check | Result | What it establishes |
| --- | --- | --- |
| Locked .NET restore | Passed | All seven dependency graphs resolve under locked mode |
| Debug and Release builds | Passed, zero warnings/errors | Provider APIs compile with the pinned packages |
| xUnit foundation tests | Passed: 5, failed: 0, skipped: 0 | PDF page boundaries, three invalid configuration cases, migration discovery/SQL model |
| API integration tests | Passed: 2, failed: 0, skipped: 0 | Liveness/readiness distinction and absent business routes, using WebApplicationFactory |
| EF migration generation | Passed | Initial migration and snapshot generated by the pinned EF tool |
| EF SQL script generation | Passed | SQL contains FoundationProbes and migration history creation |
| Pending EF model changes | None | Built Release model matches the generated migration snapshot |
| PdfPig real PDF | Passed | Generated text-based two-page PDF; extracted RET-01 on page 1 and RET-02 on page 2 |
| Ollama structured chat | Passed on explicit long-timeout retry | Real local llama3.2:3b response satisfies the requested JSON object/enum; not a RAG quality score |
| Chat cancellation | Passed | Pre-canceled and 200-ms in-flight cancel requests return cancellation; does not establish instant server GPU/CPU reclamation |
| Ollama embeddings | Passed | Real all-minilm:22m batch of two, finite/nonzero 384-dimensional vectors through IEmbeddingGenerator; pre-canceled call honored |
| Live SQL integration | Blocked | No reachable local SQL Server/credentials; migration application and DB cleanup not passed |
| Live Qdrant integration | Blocked | No reachable local Qdrant; collection, upsert, filter and cleanup not passed |
| Frontend type-check, lint, production build | Passed | Strict frontend compiles and bundles with Tailwind's Vite plugin |
| Frontend formatting | Passed | Source formatting verified; generated dist excluded |
| PowerShell scripts | Syntax passed | Does not claim real Docker startup or existing-secret setup was executed |
| Hosted CI, browser UX/accessibility, RAG evaluation | Not run | No implementation/quality claim for later milestones |

The first chat attempt hit the default HTTP client's approximately 100-second cold-load limit. Ollama logged model loading followed by client cancellation. The initial combined runner therefore failed chat while passing embeddings/PDF and blocking SQL/Qdrant. With an explicit five-minute HTTP/deadline ceiling, structured chat and cancellation passed. The failure is retained as evidence; no latency target has been measured or met. The advertised maximum model context is metadata only; the spike requests 2,048 tokens.

An early pending-model command used a stale pre-migration Debug assembly and reported changes; rebuilding/using the Release assembly containing the migration snapshot resolved this. Final SQL script generation and pending-model checks use `--configuration Release --no-build` after a Release build. No migration was applied to a live SQL instance.

The Qdrant README's old SearchAsync example is obsolete in the installed client. The implementation uses QueryAsync after checking the current client source and XML API documentation. No warnings were suppressed to compile it.

## Model decisions and limitations

Observed identifiers are in [the model manifest](../../deploy/models.json). Existing llama3.2:3b Q4_K_M is reused; only the approximately 46 MB all-minilm model was downloaded. The small model has a verified 512-token context and 384 dimensions; it is not a final retrieval-model choice. Context overflow/truncation protection, 350-token chunk compatibility, retrieval quality, alternate chat models and concurrent CPU/RAM behavior remain open. Licensing was inspected at the package/model-documentation level; redistribution obligations need final review.

## Remaining M0 exit blockers

1. Free disk space or configure Docker storage on a larger disk, install/enable supported Docker Desktop/WSL2, confirm `docker info --format '{{.OSType}}'` returns `linux`.
2. Review license, initialize local SQL secret and start the pinned Compose services. Verify actual startup, health and persistence without deleting volumes.
3. Run the real SQL and Qdrant checks; record migration/data/filter/cleanup results. A generated migration or compiled client is not sufficient.
4. Confirm the model/hardware profile under simultaneous supporting-service load; inspect tokenizer/truncation/context handling before accepting the final model pair.

Exact commands are in [local setup](../m0-local-setup.md). Once these blockers pass, close M0 before starting M1's accounts, ownership and interface foundation.

## Primary API references consulted

[NuGet package index](https://api.nuget.org/v3/index.json), [OllamaSharp Microsoft.Extensions.AI implementation](https://github.com/awaescher/OllamaSharp#usage-with-microsoftextensionsai), [OllamaSharp option mapping source](https://github.com/awaescher/OllamaSharp/blob/main/src/OllamaSharp/MicrosoftAi/AbstractionMapper.cs), [Qdrant current client source](https://github.com/qdrant/qdrant-dotnet/blob/main/src/Qdrant.Client/QdrantClient.cs), [PdfPig PDF builder/extraction](https://github.com/UglyToad/PdfPig), [Vite prerequisites](https://vite.dev/guide/), [Tailwind Vite installation](https://tailwindcss.com/docs/installation/using-vite), [Docker Windows setup](https://docs.docker.com/desktop/setup/install/windows-install/), and [SQL Server container setup](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/quickstart-install-docker?view=sql-server-ver17). Installed NuGet XML documentation and actual compilation were also used to verify signatures.
