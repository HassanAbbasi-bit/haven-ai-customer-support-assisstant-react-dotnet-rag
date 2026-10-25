$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$local = Join-Path $root '.local'
New-Item -ItemType Directory -Force -Path $local | Out-Null
$runtime = Join-Path $local 'runtime.json'
if (-not (Test-Path -LiteralPath $runtime)) {
    $signingBytes = New-Object byte[] 48
    $passwordBytes = New-Object byte[] 24
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($signingBytes); $rng.GetBytes($passwordBytes) } finally { $rng.Dispose() }
    $adminPassword = [Convert]::ToBase64String($passwordBytes) + 'aA1!'
    $settings = @{ Auth = @{ SigningKey = [Convert]::ToBase64String($signingBytes) }; Bootstrap = @{ Email = 'admin@haven.local'; Password = $adminPassword } }
    $stream = [IO.File]::Open($runtime, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try { $writer = [IO.StreamWriter]::new($stream); try { $writer.Write(($settings | ConvertTo-Json -Depth 4)) } finally { $writer.Dispose() } } finally { $stream.Dispose() }
    $credentialPath = Join-Path $local 'admin-credentials.txt'
    [IO.File]::WriteAllText($credentialPath, "Local Haven administrator`r`nEmail: admin@haven.local`r`nPassword: $adminPassword`r`nKeep this file private. These credentials are not included in the browser or repository.`r`n")
    Remove-Variable adminPassword,settings
    Write-Output 'Created private local signing secret and administrator credentials in .local/admin-credentials.txt.'
}
. "$PSScriptRoot/Set-LocalSqlConnection.ps1"
Push-Location $root
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run --project src/HavenSupport.Api --configuration Release --no-restore --no-launch-profile -- --initialize
    if ($LASTEXITCODE -ne 0) { throw 'Application initialization failed. No existing volumes were removed.' }
} finally { Pop-Location }
