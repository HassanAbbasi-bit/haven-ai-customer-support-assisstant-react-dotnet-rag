param(
    [ValidateSet('Development','Holdout','All')][string]$Split = 'Development',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Name = 'development',
    [switch]$MeasureRetrieval
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$base = 'http://127.0.0.1:5080/api/v1'
$headers = @{ 'X-Haven-Request' = '1' }
function Save-Evaluation($Data, $Path) {
    $temporary = $Path + '.tmp'
    [IO.File]::WriteAllText($temporary, ($Data | ConvertTo-Json -Depth 20))
    # A null backup path is marshaled as an empty string by some PowerShell/.NET
    # combinations. Use a real backup so resumed runs remain atomic on Windows.
    if ([IO.File]::Exists($Path)) { [IO.File]::Replace($temporary, $Path, $Path + '.bak') } else { [IO.File]::Move($temporary, $Path) }
}
$adminHeaders = $null
if ($MeasureRetrieval) {
    $runtime = Get-Content -LiteralPath (Join-Path $root '.local/runtime.json') -Raw | ConvertFrom-Json
    $login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -Headers $headers -ContentType application/json -Body (@{email=$runtime.Bootstrap.Email;password=$runtime.Bootstrap.Password}|ConvertTo-Json)
    $adminHeaders = @{ 'X-Haven-Request'='1'; Authorization='Bearer ' + $login.accessToken }
    Remove-Variable runtime,login
}
$hashes = @('src/HavenSupport.Infrastructure/RagEngine.cs','src/HavenSupport.Application/EvidenceRules.cs','src/HavenSupport.Api/SupportWorker.cs','deploy/models.json') | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash }
$configHash = $hashes -join ':'
$folder = Join-Path $root '.local/evaluations'; New-Item -ItemType Directory -Force -Path $folder | Out-Null
$output = Join-Path $folder ($Name + '.json')
if (Test-Path -LiteralPath $output) {
    $run = Get-Content -LiteralPath $output -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($run.configHash -ne $configHash -or $run.split -ne $Split -or $run.retrievalMeasured -ne [bool]$MeasureRetrieval) { throw 'Configuration, split or retrieval measurement changed. Choose a new -Name so previous evidence is preserved.' }
} else { $run = [pscustomobject]@{ startedAtUtc=[DateTimeOffset]::UtcNow; split=$Split; configHash=$configHash; model='llama3.2:3b'; embeddingModel='all-minilm:22m'; promptProfile='extractive-source-selection-v4'; semanticGrading='pending_manual_review'; retrievalMeasured=[bool]$MeasureRetrieval; results=@() } }
Invoke-RestMethod -Method Post -Uri "$base/auth/guest-session" -Headers $headers -SessionVariable guest | Out-Null
$rows = Get-Content -LiteralPath (Join-Path $root 'docs/fixtures/evaluation-cases.md') -Encoding UTF8 | Where-Object { $_ -match '^\|\s*\d{2}\s*\|' }
foreach ($row in $rows) {
    $cells = $row.Split('|'); $id = $cells[1].Trim(); $number = [int]$id
    if (($Split -eq 'Development' -and $number -gt 20) -or ($Split -eq 'Holdout' -and $number -le 20)) { continue }
    if (@($run.results | Where-Object { $_.id -eq $id }).Count) { Write-Output "Preserving completed case $id"; continue }
    $question = $cells[2].Trim(); $expected = $cells[3].Trim(); $gold = @($cells[4].Trim() -split ',\s*' | Where-Object { $_ -ne 'None' })
    $turns = $question -split ' Follow-up: '
    $conversation = Invoke-RestMethod -Method Post -Uri "$base/conversations" -Headers $headers -WebSession $guest
    $started = [DateTimeOffset]::UtcNow; $turnResults = @(); $state = $null; $result = $null
    $previousQuestion = ''
    foreach ($turn in $turns) {
        $retrievedKeys = @()
        if ($MeasureRetrieval) {
            $query = if ($previousQuestion) { $previousQuestion + "`n" + $turn } else { $turn }
            $preview = Invoke-RestMethod -Method Post -Uri "$base/admin/retrieval-preview" -Headers $adminHeaders -ContentType application/json -Body (@{message=$query;clientRequestId=[guid]::NewGuid()}|ConvertTo-Json)
            $retrievedKeys = @($preview.passages | ForEach-Object { $_.sourceKey } | Select-Object -Unique)
        }
        $sent = [DateTimeOffset]::UtcNow
        $accepted = Invoke-RestMethod -Method Post -Uri "$base/conversations/$($conversation.id)/requests" -Headers $headers -WebSession $guest -ContentType application/json -Body (@{clientRequestId=[guid]::NewGuid().ToString();message=$turn}|ConvertTo-Json)
        $deadline = (Get-Date).AddMinutes(4)
        do {
            Start-Sleep -Seconds 2
            $state = Invoke-RestMethod -Uri "$base/conversations/$($conversation.id)/requests/$($accepted.id)" -WebSession $guest
            if ((Get-Date) -gt $deadline) { throw "Case $id exceeded the polling deadline. Its durable request can still be inspected in SQL." }
        } until ($state.state -in @('completed','failed','canceled'))
        $result = Invoke-RestMethod -Uri "$base/conversations/$($conversation.id)" -WebSession $guest
        $answer = $result.messages | Where-Object { $_.role -eq 'assistant' -and $_.requestId -eq $accepted.id } | Select-Object -Last 1
        $sources = @(); $quotesMatch = $true
        foreach ($citation in $answer.citations) {
            $source = Invoke-RestMethod -Uri "$base/messages/$($answer.id)/citations/$($citation.id)" -WebSession $guest
            if (-not $source.text.Contains($source.quote)) { $quotesMatch = $false }
            $sources += $source
        }
        $turnResults += [pscustomobject]@{question=$turn;state=$state.state;errorCode=$state.errorCode;kind=$answer.kind;text=$answer.text;durationMs=[int]([DateTimeOffset]::UtcNow-$sent).TotalMilliseconds;serverDurationMs=$state.durationMs;retrievedSourceKeys=$retrievedKeys;storedQuoteChecksPassed=$quotesMatch;sources=$sources}
        $previousQuestion = $turn
    }
    $entry = [pscustomobject]@{id=$id;input=$question;expectedFacts=$expected;goldSources=$gold;turns=$turnResults;totalDurationMs=[int]([DateTimeOffset]::UtcNow-$started).TotalMilliseconds;semanticGrade='pending_manual_review'}
    $run.results = @($run.results) + @($entry)
    Save-Evaluation -Data $run -Path $output
    Write-Output "Case $id : $($state.state) / $($turnResults[-1].kind) / $($entry.totalDurationMs) ms"
}
Write-Output "Evaluation evidence saved to .local/evaluations/$Name.json. Semantic answer quality and source support still require grading."
