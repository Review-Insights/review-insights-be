# Pelny flow testowy: upload pliku -> symulacja workera -> generacja raportu -> weryfikacja
# Uzywa MinIO (upload CSV w kroku 2, usuniecie w kroku 9) oraz RabbitMQ (publikacja w krokach 2 i 6).
# Uruchom z katalogu glownego repo. API: http://localhost:8080 (lub ustaw $env:API_URL)

$ErrorActionPreference = "Stop"
$BaseUrl = if ($env:API_URL) { $env:API_URL } else { "http://localhost:8080" }
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$CsvPath = Join-Path $RepoRoot "test.csv"
if (-not (Test-Path $CsvPath)) { throw "Brak pliku test.csv w katalogu glownym projektu." }

# Opcjonalnie zaladuj .env (do weryfikacji RabbitMQ)
$EnvPath = Join-Path $RepoRoot ".env"
if (Test-Path $EnvPath) {
    Get-Content $EnvPath | ForEach-Object {
        if ($_ -match '^\s*([^#=]+)=(.*)$') {
            [System.Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim().Trim('"'), 'Process')
        }
    }
}

$RabbitMgmtUrl = if ($env:RABBITMQ_MANAGEMENT_URL) { $env:RABBITMQ_MANAGEMENT_URL } else { "http://localhost:15672" }
$AnalyzeQueue = if ($env:RABBITMQ_ANALYZE_QUEUE) { $env:RABBITMQ_ANALYZE_QUEUE } else { "review-insights.analyze.reviews" }
$ReportQueue = if ($env:RABBITMQ_REPORT_QUEUE) { $env:RABBITMQ_REPORT_QUEUE } else { "review-insights.generate.report" }
$MinIOHealthUrl = if ($env:MINIO_HEALTH_URL) { $env:MINIO_HEALTH_URL } else { "http://localhost:9000/minio/health/live" }

function Get-QueueDepth($queue, $cred) {
    try {
        $vhostEncoded = "%2F"
        $info = Invoke-RestMethod -Uri "$RabbitMgmtUrl/api/queues/$vhostEncoded/$queue" -Headers @{ Authorization = "Basic $cred" } -ErrorAction Stop
        return $info.messages_ready
    } catch {
        return $null
    }
}

Write-Host "=== 1. Health ===" -ForegroundColor Cyan
Invoke-RestMethod -Uri "$BaseUrl/health" | Out-Null
Write-Host "OK`n" -ForegroundColor Green

Write-Host "=== 2. Upload pliku CSV ===" -ForegroundColor Cyan
$form = @{ file = Get-Item -Path $CsvPath }
$createResp = Invoke-WebRequest -Uri "$BaseUrl/uploads" -Method Post -Form $form -UseBasicParsing
$upload = $createResp.Content | ConvertFrom-Json
$uploadId = $upload.id
Write-Host "UploadId: $uploadId, Status: $($upload.status), TotalRecords: $($upload.totalRecords)" -ForegroundColor Green
Write-Host "  (MinIO: plik CSV zapisany w bucket; RabbitMQ: wiadomosci 'analyze_reviews' opublikowane)`n" -ForegroundColor DarkGray

# Weryfikacja kolejki analizy
if ($env:RABBITMQ_DEFAULT_USER -and $env:RABBITMQ_DEFAULT_PASS) {
    $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($env:RABBITMQ_DEFAULT_USER):$($env:RABBITMQ_DEFAULT_PASS)"))
    $depth = Get-QueueDepth -queue $AnalyzeQueue -cred $cred
    if ($depth -ne $null) { Write-Host "  RabbitMQ '$AnalyzeQueue' messages_ready: $depth`n" -ForegroundColor Green }
}

Write-Host "=== 3. MinIO health ===" -ForegroundColor Cyan
try {
    $minioResp = Invoke-WebRequest -Uri $MinIOHealthUrl -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
    Write-Host "  MinIO dostepny (HTTP $($minioResp.StatusCode)).`n" -ForegroundColor Green
} catch {
    Write-Host "  MinIO health nieosiagalny pod $MinIOHealthUrl.`n" -ForegroundColor Yellow
}

Write-Host "=== 4. Lista uploadow ===" -ForegroundColor Cyan
$uploads = Invoke-RestMethod -Uri "$BaseUrl/uploads"
Write-Host "Liczba uploadow: $($uploads.total), na stronie: $($uploads.data.Count)`n" -ForegroundColor Green

Write-Host "=== 5. Pierwsze 3 recenzje uploadu ===" -ForegroundColor Cyan
$reviews = Invoke-RestMethod -Uri "$BaseUrl/reviews?uploadId=$uploadId&limit=10"
Write-Host "Liczba w odpowiedzi: $($reviews.data.Count) z $($reviews.total)" -ForegroundColor Green
$firstThree = $reviews.data | Select-Object -First 3

Write-Host "=== 6. Symulacja callbackow workera (analyze_reviews) ===" -ForegroundColor Cyan
$results = @()
foreach ($r in $firstThree) {
    $results += @{
        reviewId = $r.id
        overallSentiment = "positive"
        aspectSentiments = @(
            @{ aspect = "fit"; sentiment = "positive"; confidence = 0.9 },
            @{ aspect = "material"; sentiment = "neutral"; confidence = 0.7 }
        )
        churnProbability = 15
        churnCauses = @()
        priority = "low"
    }
}
$body = @{ results = $results } | ConvertTo-Json -Depth 8
Invoke-RestMethod -Uri "$BaseUrl/api/worker/uploads/$uploadId/results" -Method Post -Body $body -ContentType "application/json" | Out-Null
Write-Host "OK (przetworzono $($firstThree.Count) recenzji)`n" -ForegroundColor Green

Write-Host "=== 7. Sprawdzenie statusu uploadu ===" -ForegroundColor Cyan
$uploadAfter = (Invoke-RestMethod -Uri "$BaseUrl/uploads").data | Where-Object { $_.id -eq $uploadId }
Write-Host "Status: $($uploadAfter.status), AnalyzedRecords: $($uploadAfter.analyzedRecords) / $($uploadAfter.totalRecords)`n" -ForegroundColor Green

Write-Host "=== 8. Generacja raportu ===" -ForegroundColor Cyan
$reportBody = @{
    title = "Raport testowy $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    filters = @{ minRating = 1; maxRating = 5 }
} | ConvertTo-Json -Depth 4
$reportResp = Invoke-RestMethod -Uri "$BaseUrl/reports/generate" -Method Post -Body $reportBody -ContentType "application/json"
$reportId = $reportResp.id
Write-Host "ReportId: $reportId, Status: $($reportResp.status), TotalRecords: $($reportResp.totalRecords)" -ForegroundColor Green
if ($env:RABBITMQ_DEFAULT_USER -and $env:RABBITMQ_DEFAULT_PASS) {
    $depth = Get-QueueDepth -queue $ReportQueue -cred $cred
    if ($depth -ne $null) { Write-Host "  RabbitMQ '$ReportQueue' messages_ready: $depth`n" -ForegroundColor Green }
}

Write-Host "=== 9. Symulacja callbacku raportu ===" -ForegroundColor Cyan
$reportResultBody = @{
    summary = @{
        averageRating = 3.8
        recommendationRate = 72.5
        sentimentBreakdown = @{ positive = 5; neutral = 2; negative = 1 }
        topChurnCauses = @(@{ cause = "sizing_issues"; count = 3 })
    }
    insights = @(
        @{ id = [guid]::NewGuid().Guid; type = "trend"; title = "Wzrost negatywnych opinii"; description = "Test"; severity = "medium" }
    )
    suggestions = @(
        @{ id = [guid]::NewGuid().Guid; action = "Sprawdzic dostawce materialu"; reasoning = "Test"; priority = "high"; relatedProducts = @() }
    )
} | ConvertTo-Json -Depth 8
Invoke-RestMethod -Uri "$BaseUrl/api/worker/reports/$reportId/result" -Method Post -Body $reportResultBody -ContentType "application/json" | Out-Null
Write-Host "OK`n" -ForegroundColor Green

Write-Host "=== 10. Pobranie PDF ===" -ForegroundColor Cyan
$pdfPath = Join-Path $RepoRoot "raport-test.pdf"
Invoke-WebRequest -Uri "$BaseUrl/reports/$reportId/pdf" -OutFile $pdfPath -UseBasicParsing
Write-Host "Zapisano: $pdfPath`n" -ForegroundColor Green

Write-Host "=== 11. Cascade delete uploadu ===" -ForegroundColor Cyan
Invoke-RestMethod -Uri "$BaseUrl/uploads/$uploadId" -Method Delete | Out-Null
Write-Host "OK`n" -ForegroundColor Green

Write-Host "=== 12. Usuniecie raportu ===" -ForegroundColor Cyan
Invoke-RestMethod -Uri "$BaseUrl/reports/$reportId" -Method Delete | Out-Null
Write-Host "OK`n" -ForegroundColor Green

Write-Host "=== Flow zakonczony pomyslnie ===" -ForegroundColor Green
