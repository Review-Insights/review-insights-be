# Pelny flow testowy: upload pliku -> oczekiwanie na wynik workera -> generacja raportu -> oczekiwanie na wynik workera -> PDF -> cleanup
# Wymaga aktywnego workera, ktory konsumuje zadania z RabbitMQ i publikuje wyniki do kolejek wynikowych.
# Uruchom z katalogu glownego repo. API: http://localhost:8080 (lub ustaw $env:API_URL)

$ErrorActionPreference = "Stop"
$BaseUrl = if ($env:API_URL) { $env:API_URL } else { "http://localhost:8080" }
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$CsvPath = Join-Path $RepoRoot "test.csv"
$PollIntervalSeconds = if ($env:FLOW_POLL_INTERVAL_SECONDS) { [int]$env:FLOW_POLL_INTERVAL_SECONDS } else { 5 }
$WorkerTimeoutSeconds = if ($env:FLOW_TIMEOUT_SECONDS) { [int]$env:FLOW_TIMEOUT_SECONDS } else { 300 }
$uploadId = $null
$reportId = $null

if (-not (Test-Path $CsvPath)) {
    throw "Brak pliku test.csv w katalogu glownym projektu."
}

# Opcjonalnie zaladuj .env (do weryfikacji RabbitMQ / MinIO)
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

function Wait-ForUploadCompletion($id, $timeoutSeconds, $pollIntervalSeconds) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $upload = Invoke-RestMethod -Uri "$BaseUrl/uploads/$id"
        Write-Host "  Upload status: $($upload.status), analyzed: $($upload.analyzedRecords)/$($upload.totalRecords)" -ForegroundColor DarkGray
        if ($upload.status -eq "done") {
            return $upload
        }
        if ($upload.status -eq "error") {
            throw "Upload zakonczyl sie bledem: $($upload.errorMessage)"
        }
        Start-Sleep -Seconds $pollIntervalSeconds
    }

    throw "Przekroczono limit oczekiwania ($timeoutSeconds s) na zakonczenie uploadu. Upewnij sie, ze worker jest aktywny."
}

function Wait-ForReportCompletion($id, $timeoutSeconds, $pollIntervalSeconds) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $report = Invoke-RestMethod -Uri "$BaseUrl/reports/$id"
        Write-Host "  Report status: $($report.status)" -ForegroundColor DarkGray
        if ($report.status -eq "completed") {
            return $report
        }
        if ($report.status -eq "failed") {
            throw "Raport zakonczyl sie bledem: $($report.errorMessage)"
        }
        Start-Sleep -Seconds $pollIntervalSeconds
    }

    throw "Przekroczono limit oczekiwania ($timeoutSeconds s) na zakonczenie raportu. Upewnij sie, ze worker jest aktywny."
}

try {
    Write-Host "=== 1. Health ===" -ForegroundColor Cyan
    Invoke-RestMethod -Uri "$BaseUrl/health" | Out-Null
    Write-Host "OK`n" -ForegroundColor Green

    $cred = $null
    if ($env:RABBITMQ_DEFAULT_USER -and $env:RABBITMQ_DEFAULT_PASS) {
        $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($env:RABBITMQ_DEFAULT_USER):$($env:RABBITMQ_DEFAULT_PASS)"))
    }

    Write-Host "=== 2. Upload pliku CSV ===" -ForegroundColor Cyan
    $form = @{ file = Get-Item -Path $CsvPath }
    $createResp = Invoke-WebRequest -Uri "$BaseUrl/uploads" -Method Post -Form $form -UseBasicParsing
    $upload = $createResp.Content | ConvertFrom-Json
    $uploadId = $upload.id
    Write-Host "UploadId: $uploadId, Status: $($upload.status), TotalRecords: $($upload.totalRecords)" -ForegroundColor Green
    Write-Host "  (MinIO: plik CSV zapisany w bucket; RabbitMQ: wiadomosci 'analyze_reviews' opublikowane)`n" -ForegroundColor DarkGray

    if ($cred) {
        $depth = Get-QueueDepth -queue $AnalyzeQueue -cred $cred
        if ($depth -ne $null) {
            Write-Host "  RabbitMQ '$AnalyzeQueue' messages_ready: $depth`n" -ForegroundColor Green
        }
    }

    Write-Host "=== 3. MinIO health ===" -ForegroundColor Cyan
    try {
        $minioResp = Invoke-WebRequest -Uri $MinIOHealthUrl -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        Write-Host "  MinIO dostepny (HTTP $($minioResp.StatusCode)).`n" -ForegroundColor Green
    } catch {
        Write-Host "  MinIO health nieosiagalny pod $MinIOHealthUrl.`n" -ForegroundColor Yellow
    }

    Write-Host "=== 4. Oczekiwanie na zakonczenie uploadu ===" -ForegroundColor Cyan
    $uploadAfter = Wait-ForUploadCompletion -id $uploadId -timeoutSeconds $WorkerTimeoutSeconds -pollIntervalSeconds $PollIntervalSeconds
    Write-Host "Upload zakonczony: $($uploadAfter.status), analyzed: $($uploadAfter.analyzedRecords)/$($uploadAfter.totalRecords)`n" -ForegroundColor Green

    Write-Host "=== 5. Pierwsze recenzje uploadu ===" -ForegroundColor Cyan
    $reviews = Invoke-RestMethod -Uri "$BaseUrl/reviews?uploadId=$uploadId&limit=10"
    Write-Host "Liczba w odpowiedzi: $($reviews.data.Count) z $($reviews.total)`n" -ForegroundColor Green

    Write-Host "=== 6. Preview generacji raportu ===" -ForegroundColor Cyan
    $reportPreviewBody = @{
        title = "Raport testowy $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        filters = @{ minRating = 1; maxRating = 5 }
    } | ConvertTo-Json -Depth 4
    $preview = Invoke-RestMethod -Uri "$BaseUrl/reports/generate/preview" -Method Post -Body $reportPreviewBody -ContentType "application/json"
    Write-Host "CanGenerate: $($preview.canGenerate), ProcessedRecords: $($preview.processedRecords), Message: $($preview.message)`n" -ForegroundColor Green
    if (-not $preview.canGenerate) {
        throw "Preview raportu zwrocil canGenerate=false. Komunikat: $($preview.message)"
    }

    Write-Host "=== 7. Generacja raportu ===" -ForegroundColor Cyan
    $reportResp = Invoke-RestMethod -Uri "$BaseUrl/reports/generate" -Method Post -Body $reportPreviewBody -ContentType "application/json"
    $reportId = $reportResp.id
    Write-Host "ReportId: $reportId, Status: $($reportResp.status), TotalRecords: $($reportResp.totalRecords)" -ForegroundColor Green
    if ($cred) {
        $depth = Get-QueueDepth -queue $ReportQueue -cred $cred
        if ($depth -ne $null) {
            Write-Host "  RabbitMQ '$ReportQueue' messages_ready: $depth`n" -ForegroundColor Green
        }
    }

    Write-Host "=== 8. Oczekiwanie na zakonczenie raportu ===" -ForegroundColor Cyan
    $reportAfter = Wait-ForReportCompletion -id $reportId -timeoutSeconds $WorkerTimeoutSeconds -pollIntervalSeconds $PollIntervalSeconds
    Write-Host "Raport zakonczony: $($reportAfter.status)`n" -ForegroundColor Green

    Write-Host "=== 9. Pobranie PDF ===" -ForegroundColor Cyan
    $pdfPath = Join-Path $RepoRoot "raport-test.pdf"
    Invoke-WebRequest -Uri "$BaseUrl/reports/$reportId/pdf" -OutFile $pdfPath -UseBasicParsing
    Write-Host "Zapisano: $pdfPath`n" -ForegroundColor Green

    Write-Host "=== Flow zakonczony pomyslnie ===" -ForegroundColor Green
}
finally {
    if ($uploadId) {
        try {
            Write-Host "`n=== Cleanup uploadu ===" -ForegroundColor Cyan
            Invoke-RestMethod -Uri "$BaseUrl/uploads/$uploadId" -Method Delete | Out-Null
            Write-Host "Upload usuniety." -ForegroundColor Green
        } catch {
            Write-Host "Nie udalo sie usunac uploadu ${uploadId}: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    if ($reportId) {
        try {
            Write-Host "`n=== Cleanup raportu ===" -ForegroundColor Cyan
            Invoke-RestMethod -Uri "$BaseUrl/reports/$reportId" -Method Delete | Out-Null
            Write-Host "Raport usuniety." -ForegroundColor Green
        } catch {
            Write-Host "Nie udalo sie usunac raportu ${reportId}: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}
