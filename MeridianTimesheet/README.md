# Meridian Timesheet — Backend (.NET 8 Web API)

Connects to the existing `Carbynetech_TimeSheet_Application` SQL Server database
(the schema/seed scripts you already ran) — this project does **not** create
or migrate that schema. It maps to what's already there.

## Architecture

Four projects, each with one clear reason to exist (Clean/Onion Architecture):

```
Meridian.Domain          Entities + enums. No dependencies on anything.
Meridian.Application     DTOs, repository interfaces, service interfaces,
                          service implementations (business logic),
                          validation. Depends only on Domain.
Meridian.Infrastructure   EF Core DbContext, entity configurations,
                          repository implementations. Depends on
                          Application (implements its interfaces) + Domain.
Meridian.Api              Controllers, Program.cs, auth, middleware.
                          Depends on Application + Infrastructure.
```

Dependencies point inward — `Api → Infrastructure/Application → Domain`.
Domain and Application know nothing about EF Core, SQL Server, or ASP.NET
Core, which is what makes them fully unit-testable and is why they're the
only two projects with zero external NuGet dependencies.

### How SRP was applied
- **One service per concern**, not one giant "TimesheetService":
  `EmployeeService` (org/hierarchy lookups), `MasterDataService` (read-only
  reference data), `TimesheetService` (entries + day types — the data),
  `WeekApprovalService` (submit/approve/reject workflow — a *separate*
  concern from the data itself).
- **`TimesheetValidator`** is a static, dependency-free class — pure
  business rules, trivially unit-testable, deliberately kept separate from
  the service that calls it.
- **One repository per aggregate** (`IEmployeeRepository`,
  `ITimeEntryRepository`, `IWeekRecordRepository`, etc.) rather than one
  repository doing everything.
- **One `IEntityTypeConfiguration<T>` class per entity** rather than
  configuring the whole model in one `OnModelCreating` method.
- **Exception-to-HTTP-status mapping lives in one middleware**
  (`ExceptionHandlingMiddleware`), not repeated try/catch in every action.

## ⚠️ Important: this was written without network access to NuGet

This solution was built in a sandboxed environment that couldn't reach
`nuget.org`, so **only `Meridian.Domain` and `Meridian.Application` were
actually compiled and verified here** (both build clean, 0 errors/0
warnings). `Meridian.Infrastructure` and `Meridian.Api` depend on EF Core,
Microsoft.Identity.Web, and Swashbuckle — none of which could be restored,
so that code was written carefully but **not locally compiled**.

**Please run this first**, and paste back any errors:
```bash
dotnet restore
dotnet build
```

## Getting started

1. **Check the connection string** in `src/Meridian.Api/appsettings.json`
   — update the server name to match your actual SQL Server instance:
   ```json
   "MeridianDatabase": "Server=YOUR_SERVER;Database=Carbynetech_TimeSheet_Application;Trusted_Connection=True;TrustServerCertificate=True;"
   ```

2. **Restore and build:**
   ```bash
   dotnet restore
   dotnet build
   ```

3. **Run it:**
   ```bash
   cd src/Meridian.Api
   dotnet run
   ```
   Swagger UI will open at `https://localhost:<port>/swagger`.

4. **Dev-mode auth is ON by default** (`Authentication:DevMode: true` in
   appsettings.json) — no real Microsoft login needed yet. Every request
   needs a header:
   ```
   X-Dev-Employee-Code: CBT1267
   ```
   Try that in Swagger's "Authorize" dialog, or with curl:
   ```bash
   curl -H "X-Dev-Employee-Code: CBT1267" https://localhost:<port>/api/timesheet/CBT1267/2026-07-27
   ```

## Switching on real Microsoft login

Once you have a real Entra App Registration (see the frontend's `.env.example`
for the same values), set in `appsettings.json`:
```json
"Authentication": { "DevMode": false },
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<your tenant id>",
  "ClientId": "<this API's app registration client id>",
  "Audience": "<same client id, or an exposed API scope>"
}
```
Note: Admin status is **not** trusted from any claim — it's always checked
live against `Carbynetech_EmployeeRole` in the database, so this works the
same way in dev mode and with real Entra tokens.

## API surface (this pass)

| Area | Endpoints |
|---|---|
| Employees | `GET /api/employees/me`, `/{code}`, `/{code}/manager`, `/{code}/direct-reports` |
| Master data | `GET /api/masterdata/{departments,locations,accounts,projects,modules,tasks}` |
| Timesheet | `GET /api/timesheet/{code}/{weekStart}`, `POST .../entries`, `PUT/DELETE entries/{id}`, `PUT {code}/day-type/{date}`, `POST .../copy-last-week` |
| Approvals | `GET /api/approvals/validate/{code}/{weekStart}`, `POST .../submit`, `/recall`, `/approve-level1`, `/approve-level2`, `/reject`, `GET /api/approvals/pending?level2=` |

Self-service endpoints (Timesheet) enforce "your own data, or Admin."
Approval actions always act as *you* — there's no way to approve on someone
else's behalf, Admin included, since approving is inherently "I did this."

## Not built yet

- Controllers/services for **Team Compliance, Reports, Notifications** —
  matching the still-placeholder frontend screens.
- **KEKA integration** — Holiday/LeaveRecord tables are ready to receive
  synced data, but no adapter exists yet (needs your KEKA API docs/creds).
- **EF Core Migrations** — this project maps to the existing schema as-is;
  if you want future schema changes managed through EF migrations rather
  than hand-written SQL, that needs a baseline migration set up first.
- Unit tests — `TimesheetValidator` and the services are structured to be
  easy to test (no static state, dependencies via constructor injection),
  but no test project exists yet.
