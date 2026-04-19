# Jak przetestowac wszystkie funkcjonalnosci

API musi byc uruchomione (np. `docker compose up -d`).
Projekt korzysta z konfiguracji **tylko z `.env`** - przed pierwszym startem wykonaj `cp .env.example .env` i uzupelnij hasla.
W trybie Development dostepna jest tez dokumentacja: http://localhost:8080/scalar.

---

## Na Windowsie

- **PowerShell** - przykladowe komendy ponizej (`Invoke-RestMethod` / `Invoke-WebRequest`).
- **CMD / Git Bash** - uzyj `curl` (Windows 10+ ma `curl.exe`).

### PowerShell - kluczowe komendy

```powershell
# Health
Invoke-RestMethod -Uri http://localhost:8080/health

# Dashboard
Invoke-RestMethod -Uri http://localhost:8080/dashboard/stats
Invoke-RestMethod -Uri "http://localhost:8080/dashboard/sentiment-trend?period=30d"

# Upload CSV
$csvPath = ".\test.csv"
$form = @{ file = Get-Item -Path $csvPath }
$resp = Invoke-WebRequest -Uri http://localhost:8080/uploads -Method Post -Form $form
$upload = $resp.Content | ConvertFrom-Json
$uploadId = $upload.id

# Lista uploadow
Invoke-RestMethod -Uri http://localhost:8080/uploads

# Lista recenzji z filtrami
Invoke-RestMethod -Uri "http://localhost:8080/reviews?page=1&limit=20&priority=high,critical&sortBy=createdAt&sortOrder=desc"

# Symulacja callbacku AI workera
$body = @{
  results = @(
    @{
      reviewId = "<UUID-recenzji>"
      overallSentiment = "positive"
      aspectSentiments = @(@{ aspect = "fit"; sentiment = "positive"; confidence = 0.92 })
      churnProbability = 12
      churnCauses = @()
      priority = "low"
    }
  )
} | ConvertTo-Json -Depth 8
Invoke-RestMethod -Uri "http://localhost:8080/api/worker/uploads/$uploadId/results" -Method Post -Body $body -ContentType "application/json"

# Generacja raportu
$reportBody = @{
  title = "Raport Q1 2026 - Tops"
  filters = @{
    departmentName = "Tops"
    minRating = 1
    maxRating = 5
  }
} | ConvertTo-Json -Depth 4
Invoke-RestMethod -Uri http://localhost:8080/reports/generate -Method Post -Body $reportBody -ContentType "application/json"

# Pobranie PDF
Invoke-WebRequest -Uri "http://localhost:8080/reports/<UUID>/pdf" -OutFile raport.pdf
```

---

## 1. Health check

```bash
curl http://localhost:8080/health
```

Oczekiwane: `Healthy` (status 200).

---

## 2. Uploads

### 2.1 Lista uploadow

```bash
curl -s "http://localhost:8080/uploads?page=1&limit=20"
```

### 2.2 Upload CSV

Przygotuj `test.csv` z naglowkiem zgodnym z datasetem (`Clothing ID,Age,Title,Review Text,Rating,Recommended IND,Positive Feedback Count,Division Name,Department Name,Class Name`).

```bash
curl -X POST http://localhost:8080/uploads \
  -F "file=@test.csv"
```

Oczekiwane: 201 Created, body z `id`, `status` = `analyzing`, `totalRecords`. Backend zapisuje plik do MinIO i publikuje wiadomosci do kolejki `review-insights.analyze.reviews`.

### 2.3 Status uploadow

Polling przez `GET /uploads` - wartosci `analyzedRecords` rosna w miare przetwarzania. Po zakonczeniu `status` = `done`.

### 2.4 Cascade delete

```bash
curl -X DELETE http://localhost:8080/uploads/{UPLOAD_ID}
```

Usuwa upload, wszystkie powiazane recenzje (transakcja) oraz blob w MinIO. Oczekiwane: 204.

---

## 3. Reviews

```bash
# Filtry: search, rating, sentiment, priority (CSV), departmentName, recommended, ageMin/Max, dateFrom/To, uploadId, clothingId
curl -s "http://localhost:8080/reviews?priority=high,critical&sentiment=negative&page=1&limit=20"

# Detal
curl -s "http://localhost:8080/reviews/{REVIEW_ID}"
```

---

## 4. Products

```bash
curl -s "http://localhost:8080/products?page=1&limit=20&sortBy=averageRating&sortOrder=asc"

curl -s "http://localhost:8080/products/1234"

curl -s "http://localhost:8080/products/1234/reviews?page=1&limit=20"

curl -s "http://localhost:8080/products/1234/trends"

curl -s "http://localhost:8080/products/1234/aspects"
```

---

## 5. Reports

### 5.1 Generacja

```bash
curl -X POST http://localhost:8080/reports/generate \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Raport Q1 2026 - Tops",
    "filters": {
      "departmentName": "Tops",
      "minRating": 1,
      "maxRating": 5
    }
  }'
```

Oczekiwane: 201, status = `generating`. Wiadomosc trafia do kolejki `review-insights.generate.report`.

### 5.2 Status / detal

```bash
curl -s http://localhost:8080/reports
curl -s http://localhost:8080/reports/{REPORT_ID}
```

### 5.3 PDF

```bash
curl -s -o raport.pdf http://localhost:8080/reports/{REPORT_ID}/pdf
```

Wymaga statusu `completed`.

### 5.4 Usuniecie

```bash
curl -X DELETE http://localhost:8080/reports/{REPORT_ID}
```

---

## 6. Symulacja callbackow AI workera

### 6.1 Wyniki analizy

```bash
curl -X POST http://localhost:8080/api/worker/uploads/{UPLOAD_ID}/results \
  -H "Content-Type: application/json" \
  -d '{
    "results": [
      {
        "reviewId": "<UUID>",
        "overallSentiment": "positive",
        "aspectSentiments": [{"aspect": "fit", "sentiment": "positive", "confidence": 0.9}],
        "churnProbability": 12,
        "churnCauses": [],
        "priority": "low"
      }
    ]
  }'
```

### 6.2 Wynik raportu

```bash
curl -X POST http://localhost:8080/api/worker/reports/{REPORT_ID}/result \
  -H "Content-Type: application/json" \
  -d '{
    "summary": {
      "averageRating": 4.2,
      "recommendationRate": 78.5,
      "sentimentBreakdown": {"positive": 120, "neutral": 40, "negative": 15},
      "topChurnCauses": [{"cause": "sizing_issues", "count": 8}]
    },
    "insights": [
      {"id": "00000000-0000-0000-0000-000000000001", "type": "trend", "title": "Spadek satysfakcji", "description": "...", "severity": "high"}
    ],
    "suggestions": [
      {"id": "00000000-0000-0000-0000-000000000002", "action": "Zmien dostawce materialu", "reasoning": "...", "priority": "high", "relatedProducts": [1234]}
    ]
  }'
```

### 6.3 Bledy

```bash
curl -X POST http://localhost:8080/api/worker/uploads/{UPLOAD_ID}/error \
  -H "Content-Type: application/json" \
  -d '{"errorMessage": "LLM timeout"}'

curl -X POST http://localhost:8080/api/worker/reports/{REPORT_ID}/error \
  -H "Content-Type: application/json" \
  -d '{"errorMessage": "rate limit"}'
```

---

## 7. Dashboard

```bash
curl -s http://localhost:8080/dashboard/stats
curl -s "http://localhost:8080/dashboard/sentiment-trend?period=30d"
curl -s http://localhost:8080/dashboard/rating-distribution
curl -s http://localhost:8080/dashboard/department-stats
```

---

## 8. Pelny flow (PowerShell)

```powershell
.\scripts\test-full-flow.ps1
```

Skrypt wykonuje: upload pliku -> sprawdzenie listy uploadow -> symulacja callbackow workera -> wygenerowanie raportu -> sprawdzenie listy raportow.

---

## 9. Bledy do sprawdzenia

- `GET /reviews/00000000-0000-0000-0000-000000000000` -> 404
- `POST /uploads` bez pliku -> 400
- `POST /uploads` z plikiem `.txt` -> 415
- `POST /uploads` z plikiem >50 MB -> 413
- `POST /reports/generate` z `title` < 3 znaki -> 400
- `POST /reports/generate` z `maxRating < minRating` -> 400
- `POST /reports/generate` z filtrami nie zwracajacymi rekordow -> 422
- `GET /reports/{id}/pdf` dla raportu w statusie `generating` -> 400
