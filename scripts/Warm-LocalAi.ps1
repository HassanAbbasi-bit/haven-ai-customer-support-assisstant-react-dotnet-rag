param([ValidateRange(30, 600)][int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$settings = Get-Content -LiteralPath (Join-Path $root 'src/HavenSupport.Api/appsettings.json') -Raw | ConvertFrom-Json
$base = $settings.Providers.OllamaBaseUrl.TrimEnd('/')
$model = $settings.Providers.ChatModel
if ($env:Providers__OllamaBaseUrl) { $base = $env:Providers__OllamaBaseUrl.TrimEnd('/') }
if ($env:Providers__ChatModel) { $model = $env:Providers__ChatModel }
Write-Output "Loading local chat model $model. Cold disk reads can take several minutes."
$body = @{ model=$model; messages=@(@{role='user';content='Reply OK.'}); stream=$false; keep_alive='15m'; options=@{num_predict=1;num_ctx=4096;num_gpu=0} } | ConvertTo-Json -Depth 6
try {
    $response = Invoke-RestMethod -Method Post -Uri "$base/api/chat" -ContentType application/json -Body $body -TimeoutSec $TimeoutSeconds
    if (-not $response.done) { throw 'The model did not finish its startup probe.' }
    Write-Output 'Local chat model loaded. This checks startup, not policy-answer quality.'
}
catch { throw "Ollama model startup failed. Check that Ollama is running and $model is installed. Details: $($_.Exception.Message)" }
