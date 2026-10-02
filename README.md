# RepsRox Analytic — Backend

ASP.NET Core 8 Web API over SQL Server that takes in the monthly CSV exports from the
[Reps-Rox](https://github.com/LimMingYuen/Reps-Rox) Android app and serves the figures
the [analytics dashboard](https://github.com/LimMingYuen/RepsRox-Analytic-Frontend) draws.

## How data gets in

In the app, **Train → Export month** shares one CSV per sheet:

| File | Row is | Columns |
| --- | --- | --- |
| `repsrox-training-YYYY-MM.csv` | a set | Date, Session, Duration, Exercise, Set, Amount, Unit, Weight kg |
| `repsrox-meals-YYYY-MM.csv` | a meal | Date, Meal, Detail, Kcal, Protein g, Carbs g, Logged |
| `repsrox-races-YYYY-MM.csv` | a race sim | Date, Time, Complete, then one column per leg |
| `repsrox-weight-YYYY-MM.csv` | a weigh-in | Date, Weight kg |

Upload them (any number, in any order) to `POST /api/imports`. The sheet is recognised by
its **header**, not its file name, so renamed files still import. Rules:

- **A month's sheet replaces that month.** Importing `training` for August again removes
  the August training imported before, so re-sending a month never doubles it.
- Every row in a sheet must fall in one month (and agree with the month in the file name,
  when it has one). An empty sheet takes its month from the file name.
- Each file is checked on its own: a bad file is reported with the line at fault and does
  not stop the others.
- The app guards cells that open like a formula (`=`, `+`, `-`, `@`) with an apostrophe;
  the importer takes it off again.
- The training sheet has no session id, so a session is a run of consecutive rows sharing
  date, name and duration.

`samples/` holds four months of generated sheets in exactly the app's format, for trying
things out.

## Running locally

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download) and a SQL Server.

**SQL Server** — either:

- **LocalDB** (installed with Visual Studio on Windows): the default connection string in
  `appsettings.json` already points at `(localdb)\MSSQLLocalDB`; nothing to do.
- **Docker**: `docker compose up -d`, then set the connection string for the API:

  ```bash
  export ConnectionStrings__Analytics="Server=localhost,1433;Database=RepsRoxAnalytics;User Id=sa;Password=RepsRox!Dev123;TrustServerCertificate=True"
  ```

  (or `dotnet user-secrets set ConnectionStrings:Analytics "..." --project src/RepsRox.Analytics.Api`).

**API**:

```bash
dotnet run --project src/RepsRox.Analytics.Api --launch-profile http
```

It listens on `http://localhost:5080`, creates/migrates the database on start
(`Database:MigrateOnStartup`), and serves Swagger at `/swagger`. CORS allows the Angular dev
server (`http://localhost:4200`); change `Cors:AllowedOrigins` for other hosts.

Load the samples:

```bash
cd samples && curl $(for f in *.csv; do printf -- '-F files=@%s ' "$f"; done) http://localhost:5080/api/imports
```

**Tests** (no SQL Server needed — they host the API over in-memory SQLite):

```bash
dotnet test
```

## API

All analytics endpoints take optional inclusive `from` / `to` dates (`yyyy-MM-dd`).

| Method | Path | Returns |
| --- | --- | --- |
| `POST` | `/api/imports` | multipart `files` → a result per file (kind, month, rows, replaced, error) |
| `GET` | `/api/imports` | every imported sheet |
| `DELETE` | `/api/imports/{id}` | drops a sheet and its rows |
| `GET` | `/api/overview` | headline totals: training, nutrition, races, weight |
| `GET` | `/api/training/sessions` | sessions with sets, volume and top set |
| `GET` | `/api/training/sessions/{id}` | one session, exercise by exercise |
| `GET` | `/api/training/volume?interval=Week\|Month` | sessions, sets, volume and minutes per period |
| `GET` | `/api/training/exercises` | per exercise: sessions, sets, volume, best load, best estimated 1RM |
| `GET` | `/api/training/progress?exercise=Back%20squat` | one exercise session by session |
| `GET` | `/api/nutrition/daily` | per day: planned vs logged kcal, protein, carbs |
| `GET` | `/api/races` | race sims with leg splits and targets |
| `GET` | `/api/races/legs` | per leg: average, best, latest vs target |
| `GET` | `/api/weight` | weigh-ins with a trailing 7-day trend |

Figures follow the app: **volume** is reps × kg and leaves out sets logged in metres (sled,
carries); the **estimated 1RM** is Epley's, for loaded sets of 12 reps or fewer; race leg
**targets** mirror `LEGS` in the app's `DemoData.kt`.

## Layout

```
src/RepsRox.Analytics.Api/
├── Controllers/   ImportsController, AnalyticsController
├── Import/        Csv reader, SheetParser (mirrors MonthExport.kt), ImportService
├── Analytics/     AnalyticsService, DTOs, race-leg targets
└── Data/          Entities, AnalyticsDbContext, EF Core migrations (SQL Server)
tests/RepsRox.Analytics.Api.Tests/   parser tests and API tests
```

New schema changes: `dotnet tool restore && dotnet ef migrations add <Name> -p src/RepsRox.Analytics.Api -o Data/Migrations`.
