# Ledger API - task management backend

ASP.NET Core 8 + EF Core + SQLite. Serves the [Ledger frontend](https://github.com/alex-maeda/ledger-frontend) (separate repo).

## Running it

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd TaskManager.Api
dotnet run --launch-profile http
```

- API: http://localhost:5171
- Swagger UI: http://localhost:5171/swagger (dev only)

The SQLite file (`app.db`) is created on first run; EF Core migrations are applied at startup, so there is no manual database setup. CORS is open to `http://localhost:5173` for the frontend.

### If `dotnet run` is blocked by an Application Control policy

On Windows machines with Smart App Control or WDAC enabled, `dotnet run` fails with `Win32Exception (4551): An Application Control policy has blocked this file`. The build succeeds; what is blocked is the unsigned `TaskManager.Api.exe` launcher that the SDK generates. Launch the DLL through the (signed) `dotnet` host instead:

PowerShell:

```powershell
dotnet build
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5171"
dotnet bin\Debug\net8.0\TaskManager.Api.dll
```

cmd:

```bat
dotnet build
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5171
dotnet bin\Debug\net8.0\TaskManager.Api.dll
```

Use the syntax that matches your shell. `set VAR=value` is cmd-only: in PowerShell it silently sets nothing, so the app starts without the Development environment and stops with `Jwt:Key is not configured`.

Both variables are required. `launchSettings.json` is only read by `dotnet run`, so launching the DLL directly means supplying the environment yourself, and without `Development` there is no JWT key - the app then fails fast at startup by design. (`dotnet run -p:UseAppHost=false` does *not* help: `dotnet run` still launches the `.exe`.)

## Tests

```bash
dotnet test
```

29 integration tests that boot the real app (`WebApplicationFactory` + in-memory SQLite, real JWT auth, real validation pipeline - no mocks). They target the two things that would actually hurt if someone changed this code:

- **Ownership** (`OwnershipTests`): user A cannot read, edit, delete, list, clear, or count user B's tasks.
- **Validation** (`ValidationTests`): blank/whitespace/over-long titles, malformed dates, and unknown enum values are all rejected.

## API

| Method | Route | Notes |
| --- | --- | --- |
| POST | `/api/auth/register` | 409 if the email is taken |
| POST | `/api/auth/login` | Returns a JWT (7-day expiry) |
| GET | `/api/tasks` | `status`, `priority`, `search`, `sortBy`, `sortDir` |
| GET | `/api/tasks/{id}` | |
| POST | `/api/tasks` | |
| PUT | `/api/tasks/{id}` | Full replace of editable fields |
| DELETE | `/api/tasks/{id}` | |
| GET | `/api/tasks/stats` | Requires `today=yyyy-MM-dd` (see below) |
| DELETE | `/api/tasks/completed` | Bulk-deletes done tasks, returns the count |

All `/api/tasks*` routes require `Authorization: Bearer <token>` and are scoped to the token's user.

## Assumptions and trade-offs

**Authentication is implemented.** JWT bearer tokens; passwords hashed with ASP.NET Identity's `PasswordHasher` (PBKDF2). Duplicate emails are prevented by a unique index rather than a check-then-insert, so there is no race.

**Ownership returns 404, not 403.** Every task query filters on the user ID from the token. A task that exists but belongs to someone else is indistinguishable from one that does not exist, so the API never leaks which IDs are real.

**Due dates are `DateOnly`, not timestamps.** A due date is a calendar date, not an instant, so it is stored as `yyyy-MM-dd` with no timezone conversion anywhere. Because "overdue" and "due today" depend on the *user's* calendar, the client sends its local date (`GET /api/tasks/stats?today=2026-07-13`) instead of the server guessing. A missing or malformed `today` is a 400.

**One project, no repository layer.** Controllers use `DbContext` directly. An abstraction with exactly one implementation is complexity, not architecture; at this size the `DbContext` *is* the data layer. DTOs in `Contracts/` still keep entities off the wire.

**PUT is a full replace** of the editable fields. Simpler than PATCH-merge semantics, and the UI always sends the complete task.

**Enums are strings only.** `JsonStringEnumConverter(allowIntegerValues: false)` means `"status": 99` is a 400, not silently stored garbage.

**`CompletedAt` is server-owned**, set when a task becomes done and cleared if it is reopened. Clients cannot forge it.

**The dev JWT signing key is committed** in `appsettings.Development.json` and labelled as such, so the app runs on a fresh clone. Startup fails loudly if no key is configured; in production it would come from a secret store.

## Deliberately left out

- **Pagination** - a personal task list is small. The query already filters on an indexed `UserId`, so adding `Skip/Take` later is mechanical.
- **Refresh tokens / revocation** - a 7-day access token is enough here. The trade-off is that a stolen token cannot be revoked server-side.
- **Rate limiting on `/api/auth/*`** - would be the first thing I added for a real deployment.
- **Concurrency control** - last write wins. No `rowversion` check, so two simultaneous edits to the same task can clobber each other.
- **CI/CD, Docker, observability** - out of scope per the brief.

## Scalability

The API is stateless (JWT, no server-side session), so it scales horizontally behind a load balancer as-is. The realistic limit is SQLite, which is a single-writer file database: swapping the provider for Postgres is a connection-string and provider change because EF Core is the only thing touching the database. The list query is already indexed on `UserId`; the next steps under real load would be pagination, a composite index on `(UserId, DueDate)`, and caching the stats endpoint.

## With another day

1. Refresh tokens with revocation, plus rate limiting on the auth endpoints.
2. Pagination and optimistic concurrency (`rowversion`) on updates.
3. Structured logging and a `/health` endpoint.
4. Recurring tasks - the `DateOnly` model makes this straightforward.

## Layout

```
TaskManager.Api/          # the API (Controllers, Models, Data, Contracts, Services)
TaskManager.Api.Tests/    # xUnit integration tests
```
