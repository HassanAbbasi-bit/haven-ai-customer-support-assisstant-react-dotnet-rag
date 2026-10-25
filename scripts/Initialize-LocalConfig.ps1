param([switch]$AcceptSqlServerEula)
$ErrorActionPreference = 'Stop'
if (-not $AcceptSqlServerEula) {
    throw 'Review the SQL Server Developer license first, then rerun with -AcceptSqlServerEula.'
}
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'deploy/.env'
$bytes = New-Object byte[] 24
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $random.GetBytes($bytes) }
finally { $random.Dispose() }
$password = [Convert]::ToBase64String($bytes) + 'Aa1!'
# CreateNew fails if configuration exists; no user secret is overwritten or printed.
$stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
try {
    $writer = [IO.StreamWriter]::new($stream)
    try { $writer.Write("ACCEPT_EULA=Y`nMSSQL_SA_PASSWORD=$password`n") }
    finally { $writer.Dispose() }
}
finally { $stream.Dispose() }
Write-Output 'Created deploy/.env with a unique local SQL password. Existing files are never overwritten.'
