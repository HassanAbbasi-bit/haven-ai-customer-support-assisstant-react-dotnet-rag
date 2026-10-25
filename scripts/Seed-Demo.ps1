param([switch]$ReindexFixtures, [switch]$UsePdf)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
function Send-PolicyUpload($Uri, $Headers, $Form) {
    $http = [System.Net.Http.HttpClient]::new()
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    try {
        foreach ($entry in $Headers.GetEnumerator()) { $http.DefaultRequestHeaders.Add($entry.Key, [string]$entry.Value) }
        foreach ($entry in $Form.GetEnumerator()) {
            if ($entry.Key -ne 'file') { $multipart.Add([System.Net.Http.StringContent]::new([string]$entry.Value), $entry.Key) }
        }
        $stream = [IO.File]::OpenRead($Form.file.FullName)
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new($(if ($Form.file.Extension -eq '.pdf') { 'application/pdf' } else { 'text/plain' }))
        $multipart.Add($fileContent, 'file', $Form.file.Name)
        $response = $http.PostAsync($Uri, $multipart).GetAwaiter().GetResult()
        try { return @{ Code = [int]$response.StatusCode; Text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() } }
        finally { $response.Dispose() }
    } finally { $multipart.Dispose(); $http.Dispose() }
}
$root = Split-Path $PSScriptRoot -Parent
$runtime = Get-Content -LiteralPath (Join-Path $root '.local/runtime.json') -Raw | ConvertFrom-Json
$base = 'http://127.0.0.1:5080/api/v1'
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -Headers @{ 'X-Haven-Request' = '1' } -ContentType 'application/json' -Body (@{ email = $runtime.Bootstrap.Email; password = $runtime.Bootstrap.Password } | ConvertTo-Json)
$headers = @{ 'X-Haven-Request' = '1'; Authorization = "Bearer $($login.accessToken)" }
$folder = Join-Path $root '.local/demo-policies'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$content = (Get-Content -LiteralPath (Join-Path $root 'docs/fixtures/knowledge-base.md') -Raw -Encoding UTF8).Replace("`r`n", "`n")
$existing = Invoke-RestMethod -Uri "$base/admin/documents" -Headers $headers
foreach ($section in ($content -split "`n## Document: " | Select-Object -Skip 1)) {
    $title = $section.Substring(0, $section.IndexOf("`n")).Trim()
    $matched = @($existing | Where-Object { $_.title -eq $title }); if ($matched.Count -and -not $ReindexFixtures) { Write-Output "Preserving existing document: $title"; continue }
    $path = Join-Path $folder (($title -replace '[^a-zA-Z0-9]', '-') + '.txt')
    [IO.File]::WriteAllText($path, ( $(if ($ReindexFixtures) { "Haven Goods policy: $title`n`n" } else { "" }) + $section.Substring($section.IndexOf("`n") + 1) ), [Text.UTF8Encoding]::new($false))
    $form = @{ title = $title; category = 'Demo policies'; version = '1.0'; effectiveDate = '2026-10-01'; file = Get-Item -LiteralPath $path }
    if ($UsePdf) { $pdfPath = Join-Path $root (".local/demo-pdfs/" + ($title -replace '[^a-zA-Z0-9]', '-') + '.pdf'); $form.file = Get-Item -LiteralPath $pdfPath }
    if ($matched.Count) { $form.documentId = $matched[0].id }
    $upload = $null
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        $response = Send-PolicyUpload -Uri "$base/admin/documents" -Headers $headers -Form $form
        if ($response.Code -ge 200 -and $response.Code -lt 300) { $upload = $response.Text | ConvertFrom-Json; break }
        if ($response.Code -ne 429 -or $attempt -eq 5) { throw "Upload failed (HTTP $($response.Code)). Inspect the admin Knowledge screen before retrying." }
        Write-Output 'Upload rate limit reached; waiting before retrying the same fixture.'; Start-Sleep -Seconds 15
    }
    Write-Output "Queued $title for real embedding and indexing."
    $deadline = (Get-Date).AddMinutes(18)
    do {
        Start-Sleep -Seconds 2
        $review = Invoke-RestMethod -Uri "$base/admin/documents/$($upload.documentId)/versions/$($upload.versionId)" -Headers $headers
        if ($review.version.state -eq 'failed') { throw "Indexing failed for $title : $($review.version.errorCode)" }
        if ((Get-Date) -gt $deadline) { throw "Indexing timed out for $title. Inspect it in Knowledge; no draft was published." }
    } until ($review.version.state -eq 'ready')
    if ($review.document.activeVersionId -eq $upload.versionId) { Write-Output "Already published: $title"; continue }
    $publishHeaders = $headers.Clone(); $publishHeaders['If-Match'] = '"' + $review.document.rowVersion + '"'
    Invoke-RestMethod -Method Post -Uri "$base/admin/versions/$($upload.versionId)/publish" -Headers $publishHeaders | Out-Null
    Write-Output "Published $title with $($review.version.chunkCount) verified source passages."
}
Remove-Variable runtime,login,headers
