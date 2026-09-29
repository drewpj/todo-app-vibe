# Todo — multi-user task manager

A small full-stack app where people register, sign in, and manage **only their own** tasks (create, view, edit,
delete, mark complete).

| Layer    | Stack                                                                 |
| -------- | --------------------------------------------------------------------- |
| Backend  | .NET 9 Web API, EF Core 9 + SQLite, JWT bearer auth                   |
| Frontend | Vue 3 (Composition API) + TypeScript, Vite, Pinia, Vue Router         |
| Tests    | xUnit (unit + integration via `WebApplicationFactory`), Vitest + Vue Test Utils |
| Delivery | Docker Compose (nginx serves the SPA and proxies `/api` to the API)   |

## Quick start (Docker)

Requires Docker with Compose v2.

```bash
docker compose up --build
```

Open **http://localhost:8080**, click *Create one*, and register. That's it — no other setup.

- Data is stored in a named Docker volume (`todo-data`) so it survives restarts. `docker compose down -v` wipes it.
- **Before using this anywhere but your own machine**, set a real token-signing secret (see
  [Configuration](#configuration)). Without one, Compose falls back to a public, insecure default so the
  one-command start works.

## Running without Docker

Prerequisites: .NET 9 SDK and Node 20.19+ (developed on Node 24).

```bash
# Terminal 1 — API on http://localhost:5000 (Development profile: uses a built-in dev-only signing key)
cd backend
dotnet run --project src/TodoApp.Api

# Terminal 2 — SPA on http://localhost:5173 (Vite proxies /api to the API)
cd frontend
npm install
npm run dev
```

The API creates and migrates a local `todo.db` SQLite file on startup.
Set `API_URL` if your API isn't on `http://localhost:5000` (`API_URL=http://localhost:7000 npm run dev`).
In Development, the OpenAPI document is served at `http://localhost:5000/openapi/v1.json`.

## Tests

```bash
cd backend  && dotnet test        # 55 tests
cd frontend && npm test           # 63 tests
cd frontend && npm run build      # also type-checks (vue-tsc)
```

What is covered:

- **Backend unit tests** — domain rules (`TodoItem.SetCompletion`), `TodoService` (ownership scoping, filtering,
  ordering, paging, LIKE-wildcard escaping), `AuthService` (email normalisation, duplicate handling, identical
  failure for unknown-user vs wrong-password), `TokenService`. These run against real in-memory SQLite, not the EF
  in-memory provider, so constraints and query translation match production.
- **Backend integration tests** — the whole HTTP pipeline (DI, middleware, JWT validation, migrations, model
  validation, ProblemDetails, rate limiting). `TodoOwnershipTests` proves user B can't read, edit, complete, delete
  or search user A's tasks.
- **Frontend tests** — HTTP client (auth header, 401 handling, error parsing), Pinia stores (including
  stale-response and empty-page handling), router guards, components (`TodoForm`, `TodoItem`) and `LoginView`
  (including open-redirect protection).

CI (`.github/workflows/ci.yml`) runs all of the above plus a Docker build on every push and PR.

## API

All routes are under `/api`. Errors use RFC 7807 `application/problem+json`.

| Method | Route                         | Auth | Description                                                   |
| ------ | ----------------------------- | ---- | ------------------------------------------------------------- |
| POST   | `/auth/register`              | –    | Create an account; returns a token (201). 409 if email taken. |
| POST   | `/auth/login`                 | –    | Returns a token. 401 on bad credentials.                      |
| GET    | `/auth/me`                    | ✔    | Current user.                                                 |
| GET    | `/todos`                      | ✔    | List your tasks. Query: `status=all\|active\|completed`, `search`, `page`, `pageSize` (≤100). |
| GET    | `/todos/{id}`                 | ✔    | One task.                                                     |
| POST   | `/todos`                      | ✔    | Create (`title`, `description?`, `dueDate?`).                 |
| PUT    | `/todos/{id}`                 | ✔    | Replace title/description/due date.                           |
| PATCH  | `/todos/{id}/completion`      | ✔    | `{ "isCompleted": true }` — sets/clears `completedAt`.        |
| DELETE | `/todos/{id}`                 | ✔    | 204.                                                          |
| GET    | `/health`                     | –    | Liveness + database check (used by the Compose healthcheck).  |

Send the token as `Authorization: Bearer <token>`.

## Configuration

Environment variables (ASP.NET Core `__` syntax) or `appsettings*.json`:

| Setting                         | Default                 | Notes                                                        |
| ------------------------------- | ----------------------- | ------------------------------------------------------------ |
| `Jwt__SigningKey`               | *(none — required)*     | ≥ 32 chars. **The app refuses to start without it.**         |
| `Jwt__ExpiryMinutes`            | `60`                    |                                                              |
| `ConnectionStrings__Default`    | `Data Source=todo.db`   | `/data/todo.db` in Docker.                                   |
| `RateLimiting__AuthPermitLimit` | `10` per 60 s per IP    | Applies to register + login.                                 |
| `Cors__AllowedOrigins__0`       | *(none)*                | Only needed if the SPA is hosted on a different origin.      |

For Docker, copy `.env.example` to `.env` and set `JWT_SIGNING_KEY` (e.g. `openssl rand -base64 48`) and,
optionally, `WEB_PORT`.

## Architecture

```
Browser ──► nginx (:8080) ──┬─► static Vue SPA
                            └─► /api/*  ──►  ASP.NET Core API (:8080, internal only) ──► SQLite (/data volume)
```

The API is not published to the host; nginx is the only entry point. Because the SPA and API share an origin,
there is no CORS surface in the default deployment.

### Backend (`backend/src/TodoApp.Api`)

```
Controllers/     thin HTTP layer: bind, authorise, call a service, shape the response
Services/        business logic (AuthService, TodoService) behind interfaces
Domain/          entities with behaviour (TodoItem.SetCompletion keeps CompletedAt consistent)
Data/            AppDbContext, UTC converters, EF migrations
Contracts/       request/response DTOs + validation attributes (entities never cross the API boundary)
Auth/            token issuing, claims helpers
Infrastructure/  DI composition, global exception -> ProblemDetails mapping, typed exceptions
Options/         strongly-typed, validated configuration
```

A single project with folders, rather than Domain/Application/Infrastructure assemblies: for an app this size the
extra projects add ceremony without adding real boundaries. The seams (interfaces on services, DTOs, options) are
where a split would happen if the codebase grows.

### Frontend (`frontend/src`)

```
api/         fetch wrapper (auth header, error parsing, 401 hook) + typed endpoint modules
stores/      Pinia: auth (session) and todos (list state, filters, paging, mutations)
views/       route-level screens (Login, Register, Todos)
components/  TodoForm (create + edit, incl. compact quick-add), TodoItem, TodoFilters, PaginationBar, FormField
router/      route table + auth guards
utils/       date helpers
```

## Design decisions & trade-offs

**Data isolation.** Every todo query filters on the caller's user id *in the query itself* (never fetch-then-check),
and the id comes from the validated token's `sub` claim — never from the request. Someone else's task returns
**404, not 403**, so ids can't be probed for existence.

**Authentication.** Passwords are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2, per-user salt,
upgradable). Emails are normalised (trim + lowercase) with a unique index; the index, not the pre-check, is the
source of truth for duplicates. Login returns the same error for unknown email and wrong password and burns
equivalent hashing time on both, to limit account enumeration. Register/login are rate-limited per IP.
*(Register does reveal whether an email is taken — the usual usability-vs-enumeration trade-off; the rate limit
mitigates bulk probing.)*

**Token storage.** The JWT is kept in `localStorage`, which is simple and works with the SPA/API split but is
readable by any script that runs on the page (XSS). Mitigations here: a strict CSP from nginx, no
`v-html`/dynamic HTML anywhere, and a 60-minute token life. The more robust option is an httpOnly, SameSite cookie
with CSRF protection plus refresh-token rotation — listed under future work.

**SQLite.** Chosen per the brief; it makes the project trivially runnable. It is a single-writer, single-node
database: fine for this MVP, and the reason the API runs as one instance. EF Core keeps the code provider-agnostic,
so moving to Postgres is a provider swap + connection string (+ regenerating migrations), not a rewrite.
Migrations are applied at startup, which is convenient here but should become a separate release step once there
is more than one instance.

**Dates.** Timestamps are stored and returned as UTC (SQLite drops `DateTimeKind`, so converters re-tag values on
read). Due dates are *calendar dates*: the UI sends UTC midnight and always formats in UTC, so "due Jan 15" is Jan
15 in every timezone. "Overdue" is computed against the user's local today.

**Listing.** Server-side filtering, search and paging (max page size 100) with a stable sort (open first, newest
first, id tie-breaker). Search escapes `%`/`_`, so searching for `100%` matches literally. The client discards
out-of-order responses and steps back a page if the last item on it disappears.

**Errors.** One global handler turns expected failures into typed ProblemDetails responses and everything else
into a generic 500 with the detail logged server-side only. Model validation returns standard 400
`ValidationProblemDetails`.

## Assumptions

- A task belongs to exactly one user; there is no sharing, teams, or admin role.
- Email is the login identifier; email verification and password reset are out of scope for the MVP.
- Password policy is length-based (8–128 chars) rather than composition rules.
- Task fields: title (≤200), optional description (≤2000), optional due date. No tags/priorities/subtasks.
- A single API instance behind a TLS-terminating proxy. TLS is assumed to be handled outside this repo (the
  container speaks plain HTTP).
- `X-Forwarded-For` is trusted from any peer — safe only because the API isn't directly reachable.

## Scalability

What breaks first, and the path forward:

1. **SQLite → PostgreSQL** (or SQL Server) to allow multiple API instances and concurrent writers. The
   `(UserId, CreatedAt)` index already matches the dominant query.
2. **Stateless API** — auth is a self-contained JWT, so horizontal scaling only needs the shared database. The
   in-process rate limiter would move to a shared store (e.g. Redis) at that point.
3. **Migrations** out of app startup into a deploy job; add EF concurrency tokens (`rowversion`/`xmin`) so two
   devices editing the same task can't silently overwrite each other.
4. **Paging** is offset-based, fine for personal task lists; keyset paging if lists grow very large.
5. **Static assets** are content-hashed and cacheable, so they can move to a CDN unchanged.

## What I'd do next

- Refresh tokens + httpOnly cookie sessions, logout-everywhere / token revocation, account lockout, and
  password-reset / email verification.
- Optimistic concurrency (ETags) for edits; optimistic UI updates on the client.
- Structured logging + OpenTelemetry traces/metrics, request correlation IDs, readiness vs liveness probes.
- Task priorities, tags, sorting options, recurring tasks; drag-to-reorder.
- End-to-end browser tests (Playwright) on top of the current unit/integration suites; a11y audit.
- Secrets from a vault/managed identity instead of env vars; container image scanning in CI; HSTS at the proxy.
- Backups for the database volume.

## Repository layout

```
backend/            .NET solution (src/TodoApp.Api, tests/TodoApp.Tests) + Dockerfile
frontend/           Vue app + Dockerfile + nginx config
docker-compose.yml  one-command local environment
.github/workflows/  CI
.env.example        Compose settings template
```
