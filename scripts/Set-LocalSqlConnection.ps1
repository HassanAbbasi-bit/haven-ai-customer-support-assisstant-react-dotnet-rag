# Dot-source this script to configure the current shell without logging the password.
$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy/.env'
$line = @(Get-Content -LiteralPath $path | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' })
if ($line.Count -ne 1) { throw 'Expected exactly one MSSQL_SA_PASSWORD line in deploy/.env.' }
$password = $line[0].Substring('MSSQL_SA_PASSWORD='.Length)
if ([string]::IsNullOrWhiteSpace($password)) { throw 'The local SQL password is empty.' }
$escaped = $password.Replace('"', '""')
$env:ConnectionStrings__SqlServer = "Server=127.0.0.1,1433;Database=HavenSupport;User ID=sa;Password=`"$escaped`";Encrypt=true;TrustServerCertificate=true;Connect Timeout=5"
Remove-Variable password, escaped, line
Write-Output 'SQL connection configured for this shell only; certificate trust bypass is local development only.'
