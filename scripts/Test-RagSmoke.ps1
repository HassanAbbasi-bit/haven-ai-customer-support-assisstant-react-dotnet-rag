$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5080/api/v1'
$headers = @{ 'X-Haven-Request' = '1' }
Invoke-RestMethod -Method Post -Uri "$base/auth/guest-session" -Headers $headers -SessionVariable guest | Out-Null
$conversation = Invoke-RestMethod -Method Post -Uri "$base/conversations" -Headers $headers -WebSession $guest
$body = @{ clientRequestId = [guid]::NewGuid().ToString(); message = 'Can I return an opened item?' } | ConvertTo-Json
$request = Invoke-RestMethod -Method Post -Uri "$base/conversations/$($conversation.id)/requests" -Headers $headers -WebSession $guest -ContentType application/json -Body $body
Write-Output 'Real RAG request accepted. Waiting for validated answer.'
$deadline = (Get-Date).AddMinutes(4)
do {
    Start-Sleep -Seconds 3
    $state = Invoke-RestMethod -Uri "$base/conversations/$($conversation.id)/requests/$($request.id)" -WebSession $guest
    if ((Get-Date) -gt $deadline) { throw 'RAG smoke-test timeout.' }
} until ($state.state -in @('completed','failed','canceled'))
$result = Invoke-RestMethod -Uri "$base/conversations/$($conversation.id)" -WebSession $guest
[IO.File]::WriteAllText((Join-Path (Split-Path $PSScriptRoot -Parent) '.local/rag-smoke.json'), ($result | ConvertTo-Json -Depth 12))
Write-Output "Request state: $($state.state); code: $($state.errorCode)"
$result.messages | Where-Object role -eq 'assistant' | Select-Object text,kind,@{n='sourceCount';e={$_.citations.Count}} | Format-List
if ($state.state -ne 'completed') { throw 'Real RAG answer did not complete. Inspect worker diagnostics.' }
$answer = $result.messages | Where-Object role -eq 'assistant' | Select-Object -Last 1
if ($answer.kind -ne 'answered' -or $answer.citations.Count -eq 0 -or $answer.text -notmatch 'cannot be returned') { throw 'Supported-answer regression: expected a cited opened-item policy answer.' }
$source = $answer.citations[0]
$detail = Invoke-RestMethod -Uri "$base/messages/$($answer.id)/citations/$($source.id)" -WebSession $guest
if (-not $detail.text.Contains($detail.quote)) { throw 'Citation quote is not contained in the stored source passage.' }
Write-Output 'Supported answer and exact stored citation passed.'
