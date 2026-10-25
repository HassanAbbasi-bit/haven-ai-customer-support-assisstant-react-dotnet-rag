# Haven Support

A local customer-support web app built with React, Vite, Tailwind, ASP.NET Core, Microsoft.Extensions.AI, Ollama, Qdrant, SQL Server/EF Core, PdfPig, and Identity/JWT.

Repository name: **haven-ai-support-react-dotnet-rag**.

## Technology stack

| Layer | Technology |
| --- | --- |
| Frontend | React, TypeScript, Vite, Tailwind CSS |
| Backend | ASP.NET Core Web API, C# |
| AI integration | Microsoft.Extensions.AI, OllamaSharp |
| Local models | Ollama: llama3.2:3b and all-minilm:22m |
| Vector search | Qdrant |
| Application data | SQL Server, Entity Framework Core |
| PDF extraction | PdfPig |
| Authentication | ASP.NET Core Identity, JWT |
| Testing | xUnit, API integration tests, real-provider evaluation scripts |
| Supporting services | Docker Compose |

## Current implementation

The app implements the real document-to-answer workflow. Administrators upload PDF/TXT policies, inspect extracted passages and indexing results, then explicitly publish a version. Customer questions are embedded and searched in Qdrant against SQL's active-version allowlist. The local model selects relevant source IDs; the server returns complete stored policy passages and persists their citations in SQL. This extractive approach preserves conditions and exceptions that the earlier generated summaries sometimes omitted. Relevance and completeness still require evaluation.

Customer chat/history, source inspection, feedback, reviewed local tickets, accounts, document administration, a support inbox and measured activity cards are implemented. This is a local demonstration using fictional Haven Goods policies. It is not a completed production release; see [verification and remaining gates](docs/reports/functional-implementation.md).

## Run locally

Start Docker Desktop (Linux engine) and Ollama. Install the .NET/Node versions pinned in `global.json` and `src/haven-support-web/.node-version`, and install the two Ollama models listed below. The original development machine already has these tools and models. SQL Server and Qdrant run in Docker; no separate Windows installations of them are needed. API and Vite run on the host.

From PowerShell in the repository:

```powershell
cd "H:\Ai Customer Support Assisstant"
.\scripts\Start-Local.ps1 -Mode Services
```

On first setup, review the SQL Server Developer license and pass `-AcceptSqlServerEula` to the Services command to generate the ignored local configuration. Subsequent starts preserve that configuration and the Docker volumes.

For a fresh checkout, restore the pinned dependencies first:

```powershell
dotnet restore HavenSupport.slnx --locked-mode
npm.cmd --prefix src/haven-support-web ci
```

Initialize the application database and unique local administrator once, before starting the API:

```powershell
.\scripts\Start-Local.ps1 -Mode Initialize
```

Run initialization for a fresh clone; the original development workspace is already initialized. The initializer preserves existing accounts and passwords. Administrator credentials are in ignored `.local/admin-credentials.txt`; keep that file private. Signing/bootstrap secrets are in ignored `.local/runtime.json`. SQL's development secret is in ignored `deploy/.env`. Never commit these files. Databases, uploaded files, credentials and installed models are not included in this repository.

Start the backend:

```powershell
.\scripts\Start-Local.ps1 -Mode Api
```

After a reboot, a cold model load can exceed the chat request deadline on this machine. Before asking the first question, run `.\scripts\Warm-LocalAi.ps1` in another terminal and wait for its success message. It loads the existing model without downloading anything; startup failures remain visible rather than extending every customer's request deadline.

In a second PowerShell terminal:

```powershell
cd "H:\Ai Customer Support Assisstant"
.\scripts\Start-Local.ps1 -Mode Web
```

Open [Haven Support](http://127.0.0.1:5173). Startup detects an already-running Haven instance instead of launching a duplicate. If Docker reports a missing engine pipe, open Docker Desktop and wait for **Engine running** before retrying Services. Do not reset Docker or delete volumes to fix startup.

## Use the app

- Guests can ask questions immediately. History belongs to the guest cookie and expires after 24 hours. Customer accounts have separate history; sign-in does not merge guest conversations.
- Sign in with the generated administrator credentials to open **Knowledge**. Upload a text-based PDF or UTF-8 TXT (up to 20 MiB), wait for indexing, select **Review**, inspect its passages, then **Publish policy**. Uploading alone does not publish it.
- Ask a policy question, open a source chip to inspect the exact passage/version/page, and mark an answer helpful or provide optional negative-feedback details.
- **Get human help** opens an editable draft. Review the excerpts and contact details, check consent, then select **Submit ticket**. This saves a local case; it does not send email or perform order actions.
- Administrators can review approved ticket details, update statuses with concurrency checks, and see actual volume, votes, unknown answers and measured answer times. Empty samples stay empty.

The original development database contains five published PDF demo policies. A fresh clone starts without that database content. To seed canonical TXT policies into a fresh installation, run this after the API starts:

```powershell
.\scripts\Start-Local.ps1 -Mode Seed
```

Optional reproducible PDF fixtures and PDF ingestion:

```powershell
dotnet run --project tests/HavenSupport.Evaluation --no-restore -- --export-demo-pdfs
.\scripts\Seed-Demo.ps1 -ReindexFixtures -UsePdf
```

Seed operations use the protected upload/review/publish endpoints. They do not insert fake answers or metrics. Existing matching content is reused.

## Verify and stop

```powershell
# Provider-independent checks:
dotnet test HavenSupport.slnx --configuration Release --filter 'Category!=Sql'

# Include real, isolated SQL integration tests:
. .\scripts\Set-LocalSqlConnection.ps1
dotnet test HavenSupport.slnx --configuration Release

# Real provider checks and a supported answer with a stored citation:
.\scripts\Start-Local.ps1 -Mode Verify
.\scripts\Test-RagSmoke.ps1

# Canonical cases; saves incremental evidence and resumes a matching run:
.\scripts\Test-CanonicalRag.ps1 -Split Development -Name development
# Holdout / All are available; semantic grading is a separate required step.

npm.cmd --prefix src/haven-support-web run build
npm.cmd --prefix src/haven-support-web run lint
npm.cmd --prefix src/haven-support-web run format:check
```

Use Ctrl+C in the API and web terminals. If those terminals were lost, `scripts/Stop-Local.ps1 -Mode Api`, `-Mode Web`, or `-Mode All` stops only verified processes belonging to this checkout. It preserves Docker containers and data. To stop supporting services without deleting their volumes:

```powershell
docker compose --env-file deploy/.env -f deploy/compose.yaml stop
```

Native Ollama models are `llama3.2:3b` and `all-minilm:22m`. No model is pulled automatically from a customer request. Local CPU answers can take tens of seconds; the provisional 15-second performance target is not proven. Accepted work is persisted with SQL leases; the UI polls progress and can recover after reload. Generation has a three-minute ceiling for this hardware profile.

## Requirements and design

The numbered documents remain the specifications: [01 product requirements](docs/01-product-requirements.md), [02 architecture](docs/02-architecture.md), [03 data/API](docs/03-data-and-api.md), [04 ingestion/RAG](docs/04-rag-and-ingestion.md), [05 UI/UX](docs/05-ui-ux.md), [06 security](docs/06-security-and-privacy.md), [07 testing](docs/07-testing-and-evaluation.md), [08 roadmap](docs/08-implementation-roadmap.md), [09 operations](docs/09-operations.md), [10 risks](docs/10-risks-and-decisions.md), and [11 coverage](docs/11-coverage-checklist.md).

The earlier M0 and help-center reports are historical. Current implementation evidence and explicit remaining work are recorded in the functional implementation report, rather than treating the previous policy-browser preview as the finished product.
