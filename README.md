# Review Insights Backend

Backend przyjmuje pliki CSV i JSON z opiniami, zapisuje je w PostgreSQL i MinIO, zleca analize workerowi przez RabbitMQ, a potem udostepnia dane do dashboardu, widokow produktowych i raportow PDF.

To repo zawiera jedno API w `src/ReviewInsights.Api`.

## Stack

| Obszar | Technologia |
|--------|-------------|
| API | .NET 10 Minimal API |
| Baza danych | PostgreSQL 17 + EF Core 10 |
| Storage plikow | MinIO |
| Kolejki | RabbitMQ 4 (`review-insights.exchange`) |
| PDF | QuestPDF |
| Dokumentacja API | OpenAPI + Scalar w trybie Development |
| Kontenery | Docker Compose |

W RabbitMQ backend korzysta z jednego exchange'a, dwoch kolejek zadan i czterech kolejek wynikowych:

- zadania do workera: `review-insights.analyze.reviews`, `review-insights.generate.report`
- wyniki i bledy odbierane przez backend: `review-insights.uploads.results`, `review-insights.uploads.errors`, `review-insights.reports.result`, `review-insights.reports.errors`

## Wymagania

| Wariant | Co jest potrzebne |
|---------|-------------------|
| Docker Compose (zalecany) | Docker + Docker Compose |
| API lokalnie (`dotnet run`) | .NET 10 SDK + Docker (Postgres, MinIO, RabbitMQ) |
| Pelny flow (upload, analiza, raport, PDF) | powyzsze + worker z repo [`agent`](../agent/docs/running.md) + klucze API LLM |

## Uruchomienie

### Wariant dockerowy (zalecany)

1. Skopiuj szablon konfiguracji:

```bash
cp .env.example .env
```

Domyslne wartosci w `.env.example` do lokalnego dev.

2. Uruchom stack:

```bash
docker compose up --build -d
```

Smoke test API: `curl http://localhost:8080/health` (oczekiwany wynik: `Healthy`). Wiecej w `docs/TESTING.md`.

Po starcie:

| Serwis | URL |
|--------|-----|
| API | http://localhost:8080 |
| Scalar (Development) | http://localhost:8080/scalar |
| OpenAPI JSON | http://localhost:8080/openapi/v1.json |
| Health | http://localhost:8080/health |
| MinIO Console | http://localhost:9001 |
| RabbitMQ Management | http://localhost:15672 |

### API lokalnie, infrastruktura w Dockerze

1. Przygotuj `.env` (jesli jeszcze go nie masz):

```bash
cp .env.example .env
```

2. Uruchom Postgresa, MinIO i RabbitMQ:

```bash
docker compose up -d postgres minio rabbitmq
```

3. Skopiuj szablon konfiguracji developerskiej:

```bash
cp src/ReviewInsights.Api/appsettings.Development.Example.json src/ReviewInsights.Api/appsettings.Development.json
```

Hasla w `appsettings.Development.json` musza odpowiadac wartosciom z `.env` (domyslne z `.env.example` juz pasuja).

4. Uruchom API:

```bash
dotnet run --project src/ReviewInsights.Api
```

Przy `dotnet run` API nasluchuje domyslnie na `http://localhost:5030`.

Jesli frontend ma laczyc sie z lokalnym backendem uruchomionym poza Dockerem, ustaw w `debil-fe` `NEXT_PUBLIC_API_BASE_URL=http://localhost:5030`.

## Sekrety i konfiguracja

| Plik | Rola |
|------|------|
| `src/ReviewInsights.Api/appsettings.json` | bazowa konfiguracja aplikacji (bez sekretow) |
| `.env.example` | szablon z domyslnymi wartosciami dev — skopiuj do `.env` przed pierwszym uruchomieniem |
| `.env` | lokalna konfiguracja dla `docker compose` — **nie** w repo (`.gitignore`) |
| `src/ReviewInsights.Api/appsettings.Development.Example.json` | szablon lokalnej konfiguracji dla `dotnet run` |
| `src/ReviewInsights.Api/appsettings.Development.json` | lokalna konfiguracja dla `dotnet run` — **nie** w repo (`.gitignore`) |

Domyslne hasla w `.env.example` (`postgres`, `minioadmin`, `guest`) sa tylko do lokalnego dev. W produkcji ustaw wlasne wartosci w `.env`.

## Dane i migracje

Schemat bazy jest aktualizowany przy starcie aplikacji przez `db.Database.MigrateAsync()`.

Glowne tabele:

- `file_uploads` - metadane uploadu i postep analizy
- `reviews` - surowe dane opinii plus pola AI (`overall_sentiment`, `aspect_sentiments`, `priority`, `priority_rule`, `priority_reason`, `analyzed_at`)
- `reports` - wygenerowane raporty (`filters`, `scope`, `summary`, `insights`, `suggestions`)

Tabela `products` nie istnieje. Lista i detal produktu sa liczone na biezaco z `reviews` po `clothing_id`.

## Integracja z workerem

Worker (Python/Celery) jest w osobnym repo [`agent`](../agent/docs/running.md). Bez niego uploady zostaja w statusie `analyzing`, a raporty w `generating`.

Backend nie wystawia publicznych endpointow callbackowych typu `/api/worker/*`.

Przeplyw jest taki:

1. backend publikuje zadania do RabbitMQ,
2. worker pobiera zadania i publikuje wynik lub blad z powrotem do RabbitMQ,
3. `WorkerResultsConsumer` odbiera wynik i zapisuje go do bazy.

Dodatkowo worker wykonuje synchroniczne wywolanie:

- `GET /history/snapshot?clothingId=...&className=...&divisionName=...`

To wywolanie sluzy do pobrania historycznych statystyk potrzebnych przy liczeniu priorytetu recenzji.

## Format wiadomosci RabbitMQ

### `review-insights.analyze.reviews`

Backend publikuje wiadomosc z `uploadId` i lista recenzji do analizy. Kazda recenzja zawiera:

- `id`
- `clothingId`
- `age`
- `title`
- `reviewText`
- `rating`
- `recommendedInd`
- `divisionName`
- `departmentName`
- `className`

Recenzje sa dzielone na batche wedlug `RabbitMQ:BatchSize` (domyslnie `200`).

### `review-insights.generate.report`

Backend publikuje:

- `reportId`
- `filters`
- liste przeanalizowanych recenzji z polami AI:
  - `overallSentiment`
  - `aspectSentiments`
  - `priority`
  - `createdAt`
  - `analyzedAt`

Liczba rekordow w jednej wiadomosci jest ograniczona przez `ReportLimits:MaxReviewsPerReport` (domyslnie `10000`).

## Endpointy

### Operacyjne

| Metoda | Endpoint | Uwagi |
|--------|----------|-------|
| GET | `/health` | healthcheck aplikacji i polaczenia z PostgreSQL |
| GET | `/openapi/v1.json` | specyfikacja OpenAPI |
| GET | `/scalar` | interaktywny podglad API w trybie Development |

### Dashboard

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/dashboard/stats` | karty z podstawowymi metrykami |
| GET | `/dashboard/sentiment-trend?period=7d|30d|90d|all` | trend sentymentu w czasie |
| GET | `/dashboard/rating-distribution` | rozklad ocen 1-5 |
| GET | `/dashboard/department-stats` | agregaty per departament |

### Reviews

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/reviews` | lista opinii z filtrami i paginacja |
| GET | `/reviews/{id}` | detal opinii |

### Products

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/products` | lista produktow liczona z `reviews` |
| GET | `/products/{clothingId}` | detal produktu |
| GET | `/products/{clothingId}/reviews` | recenzje dla jednego produktu |
| GET | `/products/{clothingId}/trends` | trendy miesieczne |
| GET | `/products/{clothingId}/aspects` | agregaty aspektow |

Backend liczy priorytet produktu na podstawie priorytetow recenzji z uwzglednieniem czasu, progu stale i porownania do baseline'u klasy produktu. Szczegoly sa w `docs/product-priority.md`.

### Reports

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/reports` | lista raportow |
| GET | `/reports/{id}` | detal raportu |
| POST | `/reports/generate/preview` | wstepna walidacja i liczba rekordow do raportu |
| POST | `/reports/generate` | utworzenie raportu i wyslanie zadania do workera |
| DELETE | `/reports/{id}` | usuniecie raportu |
| GET | `/reports/{id}/pdf` | PDF dla raportu w statusie `completed` |

### Uploads

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/uploads` | lista uploadow |
| GET | `/uploads/{id}` | detal pojedynczego uploadu |
| POST | `/uploads` | przyjecie pliku CSV lub JSON |
| DELETE | `/uploads/{id}` | usuniecie uploadu, powiazanych recenzji i pliku w storage |

### History

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/history/snapshot?clothingId=...&className=...&divisionName=...` | historyczne metryki dla produktu, klasy i segmentu |

## Struktura projektu

```
debil-be/
+-- compose.yaml
+-- .env.example
+-- README.md
+-- docs/
|   +-- product-priority.md
|   +-- TESTING.md
+-- src/
    +-- ReviewInsights.Api/
        +-- Program.cs
        +-- ReviewInsights.Api.csproj
        +-- appsettings.json
        +-- Data/
        +-- Domain/
        +-- Features/
        |   +-- Dashboard/
        |   +-- History/
        |   +-- Products/
        |   +-- Reports/
        |   +-- Reviews/
        |   +-- Uploads/
        |   +-- Worker/          # zapis wyniku workera do bazy
        +-- Infrastructure/
        |   +-- MinioFileStorageService.cs
        |   +-- RabbitMqService.cs
        |   +-- WorkerResultsConsumer.cs
        +-- Messaging/
        +-- Migrations/
```
