> Current setup, 24 October 2026: use the [README](../README.md) and [functional verification report](reports/functional-implementation.md). SQL Server/Qdrant, authentication, ingestion, chat, tickets and admin screens are implemented. The observations below are retained as the historical M0 inspection and are superseded where they describe absent functionality or unavailable Docker.

# M0 local setup and checks

This document records the original foundation stage. Use PowerShell from the repository root; follow the current README when running the functional application.

## Build and run what is available now

Install the exact .NET SDK `10.0.204` and Node `24.15.0` (npm `11.12.1`) if they are not already installed. The current machine has these versions. The project intentionally rejects silent SDK/runtime toolchain drift; upgrade pins and locks together in a later change.

```powershell
dotnet tool restore
dotnet restore HavenSupport.slnx --locked-mode
dotnet build HavenSupport.slnx --configuration Release --no-restore
dotnet test HavenSupport.slnx --configuration Release --no-build --logger trx
dotnet format HavenSupport.slnx --verify-no-changes --no-restore

npm.cmd --prefix src/haven-support-web ci
npm.cmd --prefix src/haven-support-web run typecheck
npm.cmd --prefix src/haven-support-web run lint
npm.cmd --prefix src/haven-support-web run format:check
npm.cmd --prefix src/haven-support-web run build
```

Run these in separate terminals:

```powershell
dotnet run --project src/HavenSupport.Api --launch-profile http
```

```powershell
npm.cmd --prefix src/haven-support-web run dev
```

Open [the frontend](http://127.0.0.1:5173). The API binds [port 5080](http://127.0.0.1:5080/api/v1/status). `/health/live` checks process liveness; `/health/ready` deliberately returns 503 until dependency readiness is implemented. The frontend shows an honest foundation page without working customer actions. Development uses HTTP only for these nonsensitive foundation endpoints; security flows in M1 require local HTTPS.

## Docker installation and disk prerequisites

Docker/Compose were not found on PATH, in standard all-user/per-user locations, or as a system service. SQL ports 1433 and Qdrant ports 6333/6334 were unreachable. Docker daemon/Linux-container mode therefore cannot currently be inspected, and no Compose run is claimed.

C: initially had approximately 4 GB free and fell further after dependency/build files. **Free at least 15–20 GB as a practical development allowance, or move Docker's disk image to a roomier drive before installing/pulling services.** This is an allowance, not a measured image download size. Existing volumes must be retained. The current machine has other drives with more free space; no files were moved or deleted.

1. Review [Docker's current Windows requirements and installer](https://docs.docker.com/desktop/setup/install/windows-install/). This machine reports Windows build 19045; confirm its edition, current servicing eligibility and hardware virtualization support. Installation support is not proven merely by an x64 CPU.
2. In an **Administrator PowerShell**, inspect `wsl --version`. If absent, run `wsl --install --no-distribution`; if installed but outdated, run `wsl --update`. Restart Windows if requested. Docker requires a compatible WSL2 version; enable virtualization in firmware if Windows reports it disabled. Do not reinstall an existing WSL distribution unnecessarily.
3. Download and run the official Docker Desktop Windows x86_64 installer. Select the WSL2 backend. Start Docker Desktop; review its license. Choose Linux containers if the installed edition offers a Windows/Linux switch. Configure Docker's disk image location on a drive with sufficient space before pulling images.
4. Open a new terminal and verify:

```powershell
docker --version
docker compose version
docker info --format '{{.OSType}}'
```

The final command must print `linux`. A missing command, an unreachable daemon and a daemon in Windows mode are three different problems. If current Docker requirements cannot be met on this machine, use a supported x86_64 Linux Docker host and explicitly change provider endpoints; that remote configuration has not been tested.

## Supporting services and secrets

The Compose file uses SQL Server 2022 Developer and Qdrant 1.19.0, pinned by immutable registry digests. SQL Server Developer is for development/test use. Review its license before accepting. No real secret is committed or generated automatically during builds.

```powershell
# Only if deploy/.env does not already exist, and after reviewing the SQL license:
.\scripts\Initialize-LocalConfig.ps1 -AcceptSqlServerEula

# These commands download images; do this only after freeing disk space.
docker compose --env-file deploy/.env -f deploy/compose.yaml config --quiet
docker compose --env-file deploy/.env -f deploy/compose.yaml up -d
docker compose --env-file deploy/.env -f deploy/compose.yaml ps
```

The initializer uses CreateNew and refuses to overwrite an existing file. It creates a random SQL password without displaying it. If a local configuration already exists, keep it and ensure its `ACCEPT_EULA` and `MSSQL_SA_PASSWORD` values are configured. `.env.example` is blank by design. Avoid printing expanded Compose configuration because it contains the password; use `config --quiet`.

Ports bind only to `127.0.0.1`; named SQL and Qdrant volumes preserve data across restarts. SQL has a real `sqlcmd` health check. The Qdrant image is not assumed to contain curl/wget; readiness is verified using the provider probe. No existing volumes are deleted or reset. Stop with:

```powershell
docker compose --env-file deploy/.env -f deploy/compose.yaml stop
```

Native Ollama is intentionally outside Compose because it is already installed and running. Keep it bound locally. If stopped, open the installed Ollama application, or run `ollama serve` in another terminal; do not launch a second server if one is already running.

## Models

The observed digests and sizes are in [models.json](../deploy/models.json). `llama3.2:3b` already exists (about 2 GB, Q4_K_M). The M0 embedding spike uses `all-minilm:22m` (about 46 MB, F16, 384 dimensions, 512-token context), which was downloaded explicitly during setup. Do not pull a larger model automatically.

```powershell
ollama --version
ollama list
# Only on a new machine missing the small embedding model:
ollama pull all-minilm:22m
```

Chat uses a conservative 2,048-token requested context and CPU execution. This is an M0 compatibility choice for about 12 GB RAM and an older 2 GB GPU, not a quality/performance recommendation. The first attempt hit the default HTTP client's approximately 100-second cold-load timeout; the probe now has an explicit five-minute ceiling. A successful compatibility check does not imply the later 15-second product target is achieved. Do not treat the model's advertised 131,072-token context as a usable context budget on this hardware.

`all-minilm` is selected for this small compatibility spike only. Its context/tokenizer behavior, truncation protection and retrieval quality must be revisited before the planned 350-token RAG chunks are used. The Llama 3.2 Community License and the embedding model's bundled Apache-2.0 license need to remain part of redistribution review.

## Real provider checks

```powershell
# Does not require Docker or secrets:
.\scripts\Test-Stack.ps1 -Only pdf
.\scripts\Test-Stack.ps1 -Only ollama-embeddings
.\scripts\Test-Stack.ps1 -Only ollama-chat

# After supporting services are healthy; sets the secret only in this shell:
. .\scripts\Set-LocalSqlConnection.ps1
.\scripts\Test-Stack.ps1 -Only sql
.\scripts\Test-Stack.ps1 -Only qdrant
.\scripts\Test-Stack.ps1 -Only all
```

The runner returns **0 = all selected checks passed, 1 = at least one failed, 2 = at least one blocked, 64 = invalid arguments/configuration**. Results are saved under ignored `.local/m0-provider-*.json`. An unavailable service is not counted as a passing test. SQL probes create/apply/read/drop only a uniquely named `HavenSupport_M0_<guid>` database; Qdrant probes use only a new `haven_m0_<guid>` collection and clean it in `finally`. Use only a local development administrator credential for the SQL probe, since it needs create/drop-database privileges. The future application will use a less privileged login.

The runner reads environment variables directly; it does not load the API's user-secrets or a `.env` file automatically. API options can be overridden with `Providers__OllamaBaseUrl`, `Providers__ChatModel`, `Providers__EmbeddingModel`, `Providers__QdrantHost` and `Providers__QdrantGrpcPort`. The empty SQL configuration example is documentation only and is not loaded automatically. No auth/JWT secrets are needed in M0.

## Migration commands

```powershell
# Inspect SQL without a live server; build first so the snapshot is in the assembly:
dotnet build HavenSupport.slnx --configuration Release --no-restore
dotnet ef migrations script --project src/HavenSupport.Infrastructure --configuration Release --no-build --output .local/initial-foundation.sql
dotnet ef migrations has-pending-model-changes --project src/HavenSupport.Infrastructure --configuration Release --no-build

# Apply to the explicitly configured HavenSupport development database only:
. .\scripts\Set-LocalSqlConnection.ps1
dotnet ef database update --project src/HavenSupport.Infrastructure --configuration Release --no-build
```

API startup does not apply migrations or create production databases. The initial migration contains only a FoundationProbes marker table. Full customer/business schema is deliberately deferred to M1. The SQL smoke check applies this migration in its own isolated database independently of the last command above.

The basic CI workflow runs provider-independent builds/tests and the local PDF check. It does not silently treat unavailable SQL/Qdrant/Ollama checks as passed. Hosted CI execution, Linux integration and image startup still need actual verification.
