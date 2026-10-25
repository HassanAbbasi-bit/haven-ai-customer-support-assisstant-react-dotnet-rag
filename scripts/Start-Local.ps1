param(
    [ValidateSet('Services', 'Initialize', 'Seed', 'Api', 'Web', 'Verify')]
    [string]$Mode = 'Services',
    [switch]$AcceptSqlServerEula
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if ($Mode -eq 'Services') {
        $engine = $null
        try { $engine = docker info --format '{{.OSType}}' 2>$null }
        catch { throw 'Docker Desktop is not ready. Open Docker Desktop, wait for Engine running, then rerun -Mode Services. Do not reset or delete its data.' }
        if ($LASTEXITCODE -ne 0) { throw 'Docker Desktop engine is unavailable. Open Docker Desktop, wait for Engine running, then retry.' }
        if ($engine -ne 'linux') { throw 'Switch Docker Desktop to Linux containers before starting the services.' }
        if (-not (Test-Path -LiteralPath 'deploy/.env')) {
            if (-not $AcceptSqlServerEula) {
                throw 'Review the SQL Server Developer license, then pass -AcceptSqlServerEula for first setup.'
            }
            & "$PSScriptRoot/Initialize-LocalConfig.ps1" -AcceptSqlServerEula
        }
        docker compose --progress plain --env-file deploy/.env -f deploy/compose.yaml config --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Compose configuration validation failed.' }
        docker compose --progress plain --env-file deploy/.env -f deploy/compose.yaml up -d --wait --wait-timeout 300
        if ($LASTEXITCODE -ne 0) { throw 'Services are not ready. Preserve the volumes and inspect Docker logs.' }
        Write-Output 'Containers started. Run -Mode Verify for real SQL/Qdrant/model round trips.'
    }
    elseif ($Mode -eq 'Initialize') { & "$PSScriptRoot/Initialize-App.ps1" }
    elseif ($Mode -eq 'Seed') { & "$PSScriptRoot/Seed-Demo.ps1" }
    elseif ($Mode -eq 'Api') {
        $runningApi = $null
        try { $runningApi = Invoke-RestMethod -Uri 'http://127.0.0.1:5080/api/v1/status' -TimeoutSec 3 }
        catch { # No recognizable API responded; proceed with normal startup.
        }
        if ($null -ne $runningApi -and $runningApi.name -eq 'Haven Support') {
            Write-Output 'Haven API is already running at http://127.0.0.1:5080. No second instance was started.'
            Write-Output 'Open http://127.0.0.1:5173 to use the website. To reload backend code changes, stop the existing API with Ctrl+C in its terminal first.'
            return
        }
        if (-not (Test-Path -LiteralPath '.local/runtime.json')) { throw 'Run .\scripts\Start-Local.ps1 -Mode Initialize once before starting the API.' }
        if (Test-Path -LiteralPath 'deploy/.env') { . "$PSScriptRoot/Set-LocalSqlConnection.ps1" }
        dotnet run --project src/HavenSupport.Api --launch-profile http --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'API exited unsuccessfully.' }
    }
    elseif ($Mode -eq 'Web') {
        $runningWeb = $null
        try { $runningWeb = Invoke-WebRequest -Uri 'http://127.0.0.1:5173/' -TimeoutSec 3 -UseBasicParsing }
        catch { # No recognizable website responded; proceed with normal startup.
        }
        if ($null -ne $runningWeb -and $runningWeb.Content -match '<title>Haven Support[^<]*</title>') {
            Write-Output 'Haven website is already running. Open http://127.0.0.1:5173. No second instance was started.'
            return
        }
        npm.cmd --prefix src/haven-support-web run dev
        if ($LASTEXITCODE -ne 0) { throw 'Frontend exited unsuccessfully.' }
    }
    else {
        if (Test-Path -LiteralPath 'deploy/.env') { . "$PSScriptRoot/Set-LocalSqlConnection.ps1" }
        dotnet run --project tests/HavenSupport.Evaluation --configuration Release --no-restore -- --providers all
        if ($LASTEXITCODE -ne 0) { throw 'Provider verification failed or is blocked. See .local/m0-provider-all.json.' }
    }
}
finally { Pop-Location }
