# debil-be

Backend API dla systemu analizy danych przez AI. Zbudowany w .NET 10 Minimal API z PostgreSQL, MinIO i RabbitMQ.

---

## Stack technologiczny

| Komponent | Technologia |
|-----------|-------------|
| API | .NET 10 Minimal API |
| Baza danych | PostgreSQL 17 + EF Core 10 (JSONB dla zagnieżdżonych danych) |
| File storage | MinIO (S3-compatible, self-hosted) |
| Kolejka | RabbitMQ 4 |
| Dokumentacja API | Scalar (dostępna w trybie Development) |
| Konteneryzacja | Docker + Docker Compose |

---

## Wymagania

- **Docker Desktop** (z Docker Compose)
- **.NET 10 SDK** (tylko do lokalnego developmentu / generowania migracji)

---

## Pierwsze uruchomienie (setup)

Po sklonowaniu repozytorium musisz utworzyc dwa pliki z sekretami. **Nie sa one w repozytorium** (celowo -- gitignore).

### 1. Plik `.env` (w katalogu glownym, obok `compose.yaml`)

Skopiuj szablon i uzupelnij wartosci swoimi haslami:

```bash
cp .env.example .env
```

Struktura zmiennych jest opisana w `.env.example`. Ten plik jest uzywany przez `docker compose` do konfiguracji wszystkich kontenerow.

### 2. Plik `debil-be/appsettings.Development.json` (potrzebny tylko do lokalnego `dotnet run`)

Skopiuj szablon i uzupelnij wartosci:

```bash
cp debil-be/appsettings.Development.Example.json debil-be/appsettings.Development.json
```

Struktura konfiguracji jest opisana w `appsettings.Development.Example.json`. Hasla musza byc takie same jak w `.env` (dotycza tych samych serwisow).

---

## Uruchomienie

### Wszystko w Dockerze (zalecane)

Wymaga tylko pliku `.env`.

```bash
docker compose up --build
```

Po uruchomieniu dostepne sa:

| Serwis | URL |
|--------|-----|
| API | http://localhost:8080 |
| Swagger UI | http://localhost:8080/swagger |
| Dokumentacja API (Scalar) | http://localhost:8080/scalar/v1 |
| MinIO Console | http://localhost:9001 |
| RabbitMQ Management | http://localhost:15672 |
| Health check | http://localhost:8080/health |

### API lokalnie + infrastruktura w Dockerze

Wymaga obu plikow: `.env` i `appsettings.Development.json`.

```bash
# 1. Uruchom tylko infrastrukture
docker compose up -d postgres minio rabbitmq

# 2. Uruchom API lokalnie (wymaga .NET 10 SDK)
cd debil-be
dotnet run
```

API bedzie dostepne pod http://localhost:5000 (lub port z launchSettings).

---

## Resetowanie środowiska

Jeśli chcesz zacząć od zera (czysta baza, czyste dane MinIO):

```bash
docker compose down -v
docker compose up --build
```

Flaga `-v` usuwa wszystkie volumy (dane PostgreSQL, MinIO, RabbitMQ).

---

## Generowanie migracji (po zmianie modelu)

```bash
cd debil-be
dotnet ef migrations add NazwaMigracji --output-dir Data/Migrations
```

Aby usunac ostatnia migracje: `dotnet ef migrations remove`.

Migracje sa automatycznie aplikowane do bazy przy starcie aplikacji (`MigrateAsync()` w `Program.cs`).

---

## Architektura

```
Frontend / AI Worker
        ↓ HTTP
    debil-be API (port 8080)
    ├── PostgreSQL  (port 5432)  -- blueprinty, analizy, wiersze
    ├── MinIO       (port 9000)  -- pliki CSV
    └── RabbitMQ    (port 5672)  -- kolejka zadań dla AI workera
```

---

## API Endpoints

### Blueprints

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/blueprints` | Lista blueprintów |
| GET | `/api/blueprints/{id}` | Szczegóły blueprintu (z taskami) |
| POST | `/api/blueprints` | Utwórz blueprint |
| PUT | `/api/blueprints/{id}` | Zaktualizuj blueprint |
| DELETE | `/api/blueprints/{id}` | Usuń blueprint |

### Analyses

| Metoda | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/analyses` | Lista analiz |
| GET | `/api/analyses/{id}` | Szczegóły analizy (z paginacją wierszy) |
| POST | `/api/analyses` | Utwórz analizę (multipart: `file` + `blueprintId`) |
| DELETE | `/api/analyses/{id}` | Usuń analizę |

### Callback dla AI Workera

| Metoda | Endpoint | Opis |
|--------|----------|------|
| PUT | `/api/analyses/{id}/status` | Aktualizuj status analizy |
| POST | `/api/analyses/{id}/rows` | Dodaj przetworzone wiersze |

### Development (tylko w trybie Development)

| Metoda | Endpoint | Opis |
|--------|----------|------|
| POST | `/api/dev/seed` | Załaduj przykładowy blueprint (Customer Review Analysis) |

---

## Flow tworzenia analizy

```
1. POST /api/analyses
   - Plik CSV + blueprintId jako multipart/form-data
   - API uploaduje plik do MinIO
   - API tworzy rekord Analysis (status: Pending) w PostgreSQL
   - API publikuje wiadomość do RabbitMQ

2. AI Worker konsumuje wiadomość z RabbitMQ:
   { analysisId, blueprintId, fileStorageKey }
   - Pobiera CSV z MinIO (fileStorageKey)
   - Pobiera blueprint z API lub DB
   - Przetwarza rekordy przez agentów AI

3. AI Worker raportuje wyniki przez API:
   - PUT /api/analyses/{id}/status { status: "Processing" }
   - POST /api/analyses/{id}/rows  [{ input: {...}, output: {...} }, ...]
   - PUT /api/analyses/{id}/status { status: "Completed", recordCount: N }

4. Frontend pobiera wyniki:
   - GET /api/analyses/{id}?page=1&pageSize=50
```

---

## Format wiadomości RabbitMQ

```json
{
  "analysisId": "uuid",
  "blueprintId": "uuid",
  "fileStorageKey": "analyses/{uuid}/{filename}.csv"
}
```

- Exchange: `analysis.exchange` (type: direct)
- Queue: `analysis.requests`
- Routing key: `analysis.process`

---

## Schemat bazy danych

```
Blueprints
  id (uuid PK)
  name, description
  data_structure (jsonb)  -- { "kolumna": "opis", ... }
  created_at, updated_at

BlueprintTasks
  id (uuid PK)
  blueprint_id (FK -> Blueprints, CASCADE)
  task_type, task_name, description
  question, instruction
  values (jsonb)          -- [{ value, examples[] }, ...]
  format, max_length, temperature, model
  sort_order

Analyses
  id (uuid PK)
  blueprint_id (FK -> Blueprints, RESTRICT)
  blueprint_name, filename, file_storage_key
  status (Pending | Processing | Completed | Failed)
  record_count
  input_columns, output_columns (jsonb)
  created_at

AnalysisRows
  id (uuid PK)
  analysis_id (FK -> Analyses, CASCADE)
  row_index
  input_data (jsonb)      -- oryginalne kolumny CSV
  output_data (jsonb)     -- wyniki AI (task_name -> wartość)
```

---

## Typy zadań (task_type)

| Typ | Opis | Kluczowe pola |
|-----|------|---------------|
| `classification` | Klasyfikacja do jednej kategorii | `question`, `values` |
| `extraction` | Ekstrakcja tekstu | `instruction`, `format` |
| `generation` | Generowanie tekstu | `instruction`, `max_length` |
| `multi_select` | Wybór wielu kategorii | `question`, `values` |
| `boolean` | Odpowiedź tak/nie | `question` |

---

## Konfiguracja i sekrety

Sekrety (hasla, klucze) **nie sa przechowywane w repozytorium**. Konfiguracja jest rozdzielona na warstwy:

| Warstwa | Plik | W git? | Zawiera |
|---------|------|--------|---------|
| Bazowa | `appsettings.json` | Tak | Struktura konfiguracji, wartosci niesekretne (porty, nazwy kolejek) |
| Development | `appsettings.Development.json` | Nie | Hasla do lokalnych serwisow |
| Docker | `.env` | Nie | Hasla uzywane przez `docker compose` |
| Szablony | `.env.example`, `appsettings.Development.Example.json` | Tak | Szablony do skopiowania po klonie |

---

## Struktura projektu

```
debil-be/
├── compose.yaml
├── .env.example                    # Szablon sekretow dla Docker Compose
├── README.md
└── debil-be/
    ├── Program.cs                  # DI, middleware, routing
    ├── debil-be.csproj
    ├── appsettings.json            # Konfiguracja bazowa (bez sekretow)
    ├── appsettings.Development.Example.json  # Szablon sekretow dla dev
    ├── Configuration/              # Klasy ustawien (Options pattern)
    │   ├── MinioSettings.cs
    │   └── RabbitMqSettings.cs
    ├── Data/                       # EF Core
    │   ├── AppDbContext.cs
    │   └── Migrations/
    ├── Entities/                   # Modele bazy danych
    │   ├── Blueprint.cs
    │   ├── BlueprintTask.cs
    │   ├── Analysis.cs
    │   └── AnalysisRow.cs
    ├── DTOs/                       # Request/Response modele
    │   ├── BlueprintDto.cs
    │   ├── BlueprintTaskDto.cs
    │   └── AnalysisDto.cs
    ├── Endpoints/                  # Minimal API endpointy
    │   ├── BlueprintEndpoints.cs
    │   ├── AnalysisEndpoints.cs
    │   └── DevEndpoints.cs
    ├── Services/                   # Logika biznesowa + kontrakty
    │   ├── IBlueprintService.cs
    │   ├── BlueprintService.cs
    │   ├── IAnalysisService.cs
    │   ├── AnalysisService.cs
    │   ├── IFileStorageService.cs
    │   └── IQueueService.cs
    ├── Infrastructure/             # Implementacje zewnetrznych serwisow
    │   ├── MinioFileStorageService.cs
    │   └── RabbitMqService.cs
    └── Messaging/
        └── AnalysisRequestMessage.cs
```
