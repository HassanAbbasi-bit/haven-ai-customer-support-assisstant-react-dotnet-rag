param([ValidateSet('all', 'pdf', 'sql', 'qdrant', 'ollama-chat', 'ollama-embeddings')][string]$Only = 'all')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $root 'tests/HavenSupport.Evaluation') -- --providers $Only
exit $LASTEXITCODE
