# review-insights-be

Backend API dla systemu analizy opinii klientow e-commerce, ktory wykorzystuje AI do wykrywania sentymentu, predykcji odejscia i generowania raportow z rekomendacjami.

Zbudowany w .NET 10 Minimal API z PostgreSQL, MinIO i RabbitMQ.

---

## Stack technologiczny

| Komponent | Technologia |
|-----------|-------------|
| API | .NET 10 Minimal API |
| Baza danych | PostgreSQL 17 + EF Core 10 (JSONB dla pol AI) |
| File storage | MinIO (S3-compatible, self-hosted) |
| Kolejka | RabbitMQ 4 (dwie kolejki: analiza recenzji + generacja raportu) |
| PDF | QuestPDF (Community License) |
| Dokumentacja API | Scalar (dostepna w trybie Development) |
| Konteneryzacja | Docker + Docker Compose |

---

## Wymagania

- **Docker Desktop** (z Docker Compose)
- **.NET 10 SDK** (tylko do lokalnego developmentu)

---

## Pierwsze uruchomienie (setup)

Projekt dziala w trybie **tylko `.env`**. `docker-compose.override.yml` nie nadpisuje juz hasel.

1. Skopiuj szablon:

```bash
cp .env.example .env
```

2. Ustaw w `.env` swoje dane dla Postgresa/MinIO/RabbitMQ.

Plik `.env` jest w `.gitignore` i nie trafi na GitHub.

### Lokalny `dotnet run`

Skopiuj szablon i uzupelnij wartosci:

```bash
cp src/ReviewInsights.Api/appsettings.Development.Example.json src/ReviewInsights.Api/appsettings.Development.json
```

---

## Uruchomienie

### Wszystko w Dockerze (zalecane)

```bash
docker compose up --build -d
```

| Serwis | URL |
|--------|-----|
| API | http://localhost:8080 |
| Dokumentacja API (Scalar) | http://localhost:8080/scalar |
| OpenAPI JSON | http://localhost:8080/openapi/v1.json |
| MinIO Console | http://localhost:9001 |
| RabbitMQ Management | http://localhost:15672 |
| Health check | http://localhost:8080/health |

### API lokalnie + infrastruktura w Dockerze

```bash
docker compose up -d postgres minio rabbitmq
cd src/ReviewInsights.Api
dotnet run
```

API bedzie dostepne pod http://localhost:5030.

---

## Resetowanie srodowiska

```bash
docker compose down -v
docker compose up --build -d
```

Flaga `-v` usuwa wszystkie volumy (Postgres, MinIO, RabbitMQ).

---

## Schemat bazy

Schemat bazy jest aktualizowany przy starcie aplikacji przez EF Core migrations (`db.Database.MigrateAsync()`).

> UWAGA: po zmianie modelu nalezy dodac nowa migracje. `docker compose down -v` (albo reczne `DROP DATABASE`) jest potrzebne tylko wtedy, gdy chcesz zaczac od czystego srodowiska, a nie do standardowej ewolucji schematu.

Tabele:

- `file_uploads` - rekord na kazdy upload (status, total/analyzed records, storage key)
- `reviews` - oryginalne kolumny z datasetu + pola AI (`overall_sentiment`, `aspect_sentiments` jsonb, `priority`, `priority_rule`, `priority_reason`, `analyzed_at`)
- `reports` - raporty AI (`filters`, `scope`, `summary`, `insights`, `suggestions`; wszystko JSONB)

Indeksy: `reviews(upload_id)`, `reviews(clothing_id)`, `reviews(priority)`, `reviews(overall_sentiment)`, `reviews(created_at)`, `reviews(department_name)`, `reviews(rating)`.

Tabela `products` celowo nie istnieje - listy/agregaty produktowe sa liczone on-the-fly z `reviews` po `clothing_id`.

---

## Endpointy

### Dashboard

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/dashboard/stats` | 4 karty (total reviews, average rating, recommendation rate, high priority) |
| GET | `/dashboard/sentiment-trend?period=7d|30d|90d|all` | Trend pozytywny/neutralny/negatywny w czasie |
| GET | `/dashboard/rating-distribution` | Histogram ocen 1-5 |
| GET | `/dashboard/department-stats` | Agregaty per departament |

### Reviews

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/reviews` | Lista z paginacja, sortowaniem i 16 filtrami (`search`, `rating`, `sentiment`, `priority` (CSV), `departmentName`, `divisionName`, `className`, `recommended`, `ageMin/Max`, `dateFrom/To`, `uploadId`, `clothingId`, ...) |
| GET | `/reviews/{id}` | Pelne pola (lacznie z polami AI) |

### Products

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/products` | Agregaty po `clothing_id` (paginacja, sort, filtry) |
| GET | `/products/{clothingId}` | Detal: dystrybucja sentymentu/ocen, aspekty, trendy miesieczne |
| GET | `/products/{clothingId}/reviews` | Recenzje dla produktu (reuse Reviews z dolaczonym `clothingId`) |
| GET | `/products/{clothingId}/trends` | Trendy miesieczne (rating, sentiment, count) |
| GET | `/products/{clothingId}/aspects` | Agregaty aspektow (material, sizing, fit, color, price) |

Priorytet produktu nie jest juz liczony jako historyczne `max(review.priority)`. Backend stosuje recency-weighted priorytety recenzji, Wilson lower bound dla malych probek, prog "stale" po 365 dniach oraz porownanie do baseline'u klasy produktu.

### Reports

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/reports` | Lista historycznych raportow |
| GET | `/reports/{id}` | Detal (summary, insights, suggestions) |
| POST | `/reports/generate/preview` | Podglad liczby rekordow, limitu i informacji, czy raport moze zostac wygenerowany |
| POST | `/reports/generate` | Tworzy raport (status=`generating`) i wysyla do AI workera |
| DELETE | `/reports/{id}` | Usun raport |
| GET | `/reports/{id}/pdf` | PDF wygenerowany przez QuestPDF (tylko dla `completed`) |

### Uploads

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/uploads` | Lista uploadow (paginacja, filtr `status`) |
| GET | `/uploads/{id}` | Detal pojedynczego uploadu |
| POST | `/uploads` | Multipart `file` (CSV/JSON, max 50 MB). Parsuje, zapisuje do MinIO, tworzy `Review` rekordy, publikuje do RabbitMQ. |
| DELETE | `/uploads/{id}` | Cascade delete: reviews + upload + blob (transakcja) |

### History

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/history/snapshot?clothingId=...&className=...&divisionName=...` | Snapshot historyczny dla produktu, klasy i segmentu |

### Worker integration

Backend nie wystawia publicznych endpointow callbackowych dla workera. Wyniki i bledy sa odbierane przez `WorkerResultsConsumer` z kolejek RabbitMQ:

- `review-insights.uploads.results`
- `review-insights.uploads.errors`
- `review-insights.reports.result`
- `review-insights.reports.errors`

---

## Format wiadomosci RabbitMQ

### Analiza recenzji (`review-insights.analyze.reviews`)

```json
{
  "taskType": "analyze_reviews",
  "uploadId": "uuid",
  "reviews": [
    {
      "id": "uuid",
      "clothingId": 1234,
      "age": 35,
      "title": "...",
      "reviewText": "...",
      "rating": 5,
      "recommendedInd": true,
      "divisionName": "...",
      "departmentName": "...",
      "className": "..."
    }
  ]
}
```

Backend dzieli recenzje na batche o rozmiarze `RabbitMQ:BatchSize` (domyslnie 200).

### Generacja raportu (`review-insights.generate.report`)

```json
{
  "taskType": "generate_report",
  "reportId": "uuid",
  "filters": { ... },
  "reviews": [ /* AnalyzedReviewPayload (z polami AI) */ ]
}
```

Maksymalnie `ReportLimits:MaxReviewsPerReport` (domyslnie 10000) rekordow w jednej wiadomosci.

### Wyniki workera

Wyniki analizy uploadow i raportow wracaja przez kolejki wynikowe/bledow skonfigurowane w sekcji `RabbitMQ`, a nie przez publiczne endpointy HTTP.

---

## Architektura

```
Frontend
   |  HTTP
   v
review-insights-be  (port 8080)
   +-- PostgreSQL  (port 5432)  -- file_uploads, reviews, reports
   +-- MinIO       (port 9000)  -- pliki CSV/JSON
   +-- RabbitMQ    (port 5672)  -- kolejki zadan i wynikow dla AI workera
                                    -> analyze.reviews
                                    -> generate.report
                                    <- uploads.results / uploads.errors
                                    <- reports.result / reports.errors
AI Worker (Python/LangChain) -- poza tym repo
```

---

## Konfiguracja i sekrety

| Warstwa | Plik | W git? | Zawiera |
|---------|------|--------|---------|
| Bazowa | `src/ReviewInsights.Api/appsettings.json` | Tak | Struktura konfiguracji, niesekretne wartosci (porty, nazwy kolejek, limity) |
| Development | `src/ReviewInsights.Api/appsettings.Development.json` | Nie | Hasla do lokalnych serwisow |
| Docker | `.env` | Nie | Hasla dla `docker compose` |
| Szablony | `.env.example`, `appsettings.Development.Example.json` | Tak | Szablony do skopiowania po klonie |

---

## Struktura projektu

```
review-insights-be/
+-- compose.yaml
+-- docker-compose.override.yml
+-- .env.example
+-- ReviewInsights.slnx
+-- README.md
+-- TESTING.md
+-- src/
    +-- ReviewInsights.Api/
        +-- Program.cs
        +-- ReviewInsights.Api.csproj
        +-- Dockerfile
        +-- appsettings.json
        +-- Common/                  (PaginatedResponse, EnumParser, ErrorResponse, ...)
        +-- Configuration/           (MinioSettings, RabbitMqSettings, ReportLimits)
        +-- Data/AppDbContext.cs
        +-- Domain/
        |   +-- Enums/               (Sentiment, Priority, AspectKey, UploadStatus, ReportStatus, InsightType)
        |   +-- Entities/            (FileUpload, Review, Report)
        |   +-- ValueObjects/        (AspectSentiment, ReportFilters, ReportScope, ReportSummary, ReportInsight, ReportSuggestion)
        +-- Features/
        |   +-- Dashboard/
        |   +-- History/
        |   +-- Reviews/
        |   +-- Products/
        |   +-- Reports/             (zawiera PdfReportRenderer)
        |   +-- Uploads/             (zawiera CsvJsonReviewParser)
        |   +-- Worker/              (serwis przetwarzania wynikow workera)
        +-- Infrastructure/          (MinioFileStorageService, RabbitMqService)
        +-- Messaging/               (AnalyzeReviewsMessage, GenerateReportMessage)
```
