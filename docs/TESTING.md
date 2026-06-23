# Testowanie backendu

Ten plik sluzy jako krotki runbook do sprawdzenia backendu po zmianach w API, integracji z workerem i obsludze uploadow.

Domyslnie komendy zakladaja API pod `http://localhost:8080` (wariant dockerowy). Przy lokalnym `dotnet run` ustaw `API_URL=http://localhost:5030`.

## Zanim zaczniesz

- backend musi byc uruchomiony — najpierw `cp .env.example .env`, potem `docker compose up --build -d`,
- smoke test (health, dashboard, listy) dziala bez workera,
- pelny flow (upload → analiza → raport → PDF) wymaga aktywnego workera z repo [`agent`](../../agent/docs/running.md) + kluczy API LLM,
- przy lokalnym `dotnet run` potrzebne jest tez `src/ReviewInsights.Api/appsettings.Development.json` (skopiuj z `appsettings.Development.Example.json`; hasla musza zgadzac sie z `.env`).

W trybie Development dostepne sa tez:

- Scalar: `http://localhost:8080/scalar`
- OpenAPI: `http://localhost:8080/openapi/v1.json`

## Szybki smoke test

### PowerShell

```powershell
$ApiUrl = if ($env:API_URL) { $env:API_URL } else { "http://localhost:8080" }

Invoke-RestMethod -Uri "$ApiUrl/health"
Invoke-RestMethod -Uri "$ApiUrl/dashboard/stats"
Invoke-RestMethod -Uri "$ApiUrl/uploads?page=1&limit=5"
Invoke-RestMethod -Uri "$ApiUrl/reviews?page=1&limit=5"
Invoke-RestMethod -Uri "$ApiUrl/reports?page=1&limit=5"
```

### Bash / Git Bash

```bash
API_URL="${API_URL:-http://localhost:8080}"

curl "$API_URL/health"
curl -s "$API_URL/dashboard/stats"
curl -s "$API_URL/uploads?page=1&limit=5"
curl -s "$API_URL/reviews?page=1&limit=5"
curl -s "$API_URL/reports?page=1&limit=5"
```

Oczekiwany wynik:

- `/health` zwraca `Healthy`,
- pozostale endpointy odpowiadaja `200`,
- odpowiedzi sa poprawnym JSON-em nawet przy pustej bazie.

## Pelny flow z workerem

Najpierw uruchom workera wedlug [`agent/docs/running.md`](../../agent/docs/running.md) (`python main.py` w katalogu `agent`).

Najprostsza opcja na Windowsie:

```powershell
.\scripts\test-full-flow.ps1
```

Skrypt:

1. sprawdza health,
2. robi upload `test.csv`,
3. czeka na zakonczenie analizy uploadu,
4. generuje raport,
5. czeka na wynik workera,
6. pobiera PDF,
7. sprzata dane testowe.

To jest najlepszy test po zmianach w kolejkach, payloadach i zapisie wyniku do bazy.

## Reczne sprawdzenie endpointow

### Upload

Przygotuj `test.csv` z naglowkiem:

`Clothing ID,Age,Title,Review Text,Rating,Recommended IND,Positive Feedback Count,Division Name,Department Name,Class Name`

```bash
API_URL="${API_URL:-http://localhost:8080}"

curl -X POST "$API_URL/uploads" \
  -F "file=@test.csv"
```

Oczekiwane:

- `201 Created`,
- w body jest `id`,
- `status` poczatkowo przechodzi do `analyzing`,
- rekord uploadu pojawia sie w `GET /uploads`,
- `GET /uploads/{id}` zwraca detal pojedynczego uploadu.

Status mozna sledzic na dwa sposoby:

```bash
curl -s "$API_URL/uploads?page=1&limit=20"
curl -s "$API_URL/uploads/{UPLOAD_ID}"
```

### Reviews

```bash
curl -s "$API_URL/reviews?priority=high,critical&page=1&limit=20"
curl -s "$API_URL/reviews/{REVIEW_ID}"
```

Sprawdz:

- paginacje,
- filtrowanie,
- pola AI w detalu,
- zwiazanie recenzji z `uploadId`.

### Products

```bash
curl -s "$API_URL/products?page=1&limit=20"
curl -s "$API_URL/products/1234"
curl -s "$API_URL/products/1234/reviews?page=1&limit=20"
curl -s "$API_URL/products/1234/trends"
curl -s "$API_URL/products/1234/aspects"
```

Sprawdz:

- czy agregaty produktowe zgadzaja sie z recenzjami,
- czy detal zwraca `priority`, `priorityRule` i `priorityReason`,
- czy trendy i aspekty nie zwracaja pustych pol przy istniejacych danych.

### Reports

Preview:

```bash
curl -X POST "$API_URL/reports/generate/preview" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Raport testowy",
    "filters": {
      "departmentName": "Tops",
      "minRating": 1,
      "maxRating": 5
    }
  }'
```

Generacja:

```bash
curl -X POST "$API_URL/reports/generate" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Raport testowy",
    "filters": {
      "departmentName": "Tops",
      "minRating": 1,
      "maxRating": 5
    }
  }'
```

Dalsze sprawdzenie:

```bash
curl -s "$API_URL/reports"
curl -s "$API_URL/reports/{REPORT_ID}"
curl -s -o raport.pdf "$API_URL/reports/{REPORT_ID}/pdf"
```

Oczekiwane:

- preview pokazuje liczbe rekordow i limit,
- `POST /reports/generate` zwraca `201`,
- raport ma status `generating`, a po wyniku workera `completed` albo `failed`,
- PDF dziala tylko dla raportu `completed`.

### History snapshot

Ten endpoint nie sluzy frontendowi. Uzywa go worker przy liczeniu priorytetu recenzji.

```bash
curl -s "$API_URL/history/snapshot?clothingId=767&className=Blouses&divisionName=General"
```

Warto go sprawdzic po zmianach w logice priorytetow albo w modelu danych `reviews`.

## Kolejki wynikowe workera

Backend odbiera wyniki i bledy z RabbitMQ:

- `review-insights.uploads.results`
- `review-insights.uploads.errors`
- `review-insights.reports.result`
- `review-insights.reports.errors`

Jesli upload albo raport zatrzymuje sie w stanie posrednim, najpierw sprawdz:

- czy worker konsumuje `review-insights.analyze.reviews` i `review-insights.generate.report`,
- czy publikuje wynik na odpowiedni routing key,
- czy backend ma polaczenie z RabbitMQ i dziala `WorkerResultsConsumer`.

## Bledy warte sprawdzenia

- `GET /reviews/00000000-0000-0000-0000-000000000000` -> `404`
- `POST /uploads` bez pliku -> `400`
- `POST /uploads` z plikiem `.txt` -> `415`
- `POST /uploads` z plikiem > 50 MB -> `413`
- `POST /reports/generate` z `title` krotszym niz 3 znaki -> `400`
- `POST /reports/generate` z `maxRating < minRating` -> `400`
- `POST /reports/generate` z filtrami bez rekordow -> `422`
- `GET /reports/{id}/pdf` dla raportu w statusie `generating` -> `400`
