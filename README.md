```
 █████  ████████████████ ███████████
░░███  ░░███░░███░░░░░░█░█░░░███░░░█
 ░███   ░███ ░███   █ ░ ░   ░███  ░ 
 ░███   ░███ ░███████       ░███    
 ░███   ░███ ░███░░░█       ░███    
 ░███   ░███ ░███  ░        ░███    
 ░░████████  █████          █████   
  ░░░░░░░░  ░░░░░          ░░░░░    
```

# UFT Indonesia — Enterprise Resource Planning (ERP) System

**UFT Indonesia** is an open-source, modular ERP platform built for Indonesian businesses.
The system is designed to streamline and digitize core business operations — from day-to-day
workforce management all the way through to financial control and reporting.

## Project Goals

| Priority | Module | Status |
|----------|--------|--------|
| ⭐ Core | **Attendance** | Primary module — first to be developed |
| 2 | **Accounting** | Financial ledger, journal entries, and reconciliation |
| 3 | **Reporting** | Business intelligence and operational reports |
| 4 | **Inventory** | Stock management, warehousing, and procurement |
| 5 | **Payments** | Invoice processing and payment gateway integration |

## Tech Stack

### Backend (`apps/api`)

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, ASP.NET Core |
| API Framework | FastEndpoints 6.1 |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL 17 |
| Auth | ASP.NET Identity + JWT (access token + httpOnly refresh cookie) |
| Validation | FluentValidation |
| Logging | Serilog (console + rolling file) |
| Background Jobs | Hangfire (PostgreSQL-backed) |
| Messaging | Wolverine FX (PostgreSQL transport, EF Core integration) |
| API Docs | Scalar OpenAPI at `/scalar/v1` |
| Architecture | Clean Architecture (Ardalis) + DDD tactical patterns |

### Frontend (`apps/web`)

| Layer | Technology |
|-------|-----------|
| Framework | Next.js 15.1 (App Router) |
| UI Library | React 19 |
| Language | TypeScript 5.7 (strict) |
| Styling | Tailwind CSS v4, shadcn/ui |
| Data Fetching | TanStack Query v5 |
| Tables | TanStack Table v8 |
| Forms | react-hook-form + Zod |
| State | Zustand v5 |
| i18n | next-intl (id-ID locale) |
| Dates | date-fns v4, date-fns-tz |

### Hardware (optional)

- ESP32-S3 + R503 fingerprint sensor — HMAC-signed HTTP push to `/api/attendance/device-logs`

---

## Architecture

- **Outside Core** — Clean Architecture (Ardalis): `Erp.Web` → `Erp.Infrastructure` → `Erp.UseCases` → `Erp.Core` → `Erp.SharedKernel`
- **Inside Core** — DDD tactical (aggregates, value objects, domain events, specs)
- **Approvals** — generic `ApprovalEngine` + `IApprovalStrategy<T>` per module (Strategy pattern, no MediatR)

---

## Current Status

### Backend API

- **Authentication** — JWT access tokens + secure httpOnly refresh-token cookies; login, logout, refresh, and `/me` endpoints
- **Identity Seeding** — Owner/Manager/Staff roles auto-created on startup; initial owner seeded from configuration
- **Employees** — Full CRUD with pagination, search, and filters; domain events for salary, parent, and basic-info changes
- **Attendance** — HMAC-signed device log ingestion and manual entry with PunchType In/Out
- **Health Check** — `/health` endpoint
- **API Docs** — Scalar OpenAPI reference at `/scalar/v1`
- **Messaging** — Wolverine bus with PostgreSQL persistence and EF Core transaction integration

### Frontend

- **Login** — JWT session management with Zustand store
- **Employees** — List view with pagination, search, role/status filters; create, edit, delete
- **UI** — shadcn/ui component library, dark/light theming ready
- **i18n** — Indonesian locale (id-ID) via next-intl

### Tests

- **Unit Tests** (`Erp.UnitTests`) — 52+ tests covering domain entities, use cases, and infrastructure
- **Integration Tests** (`Erp.IntegrationTests`) — project scaffolded, awaiting tests

---

## Getting Started (Manual — No Docker)

### Prerequisites

Install the following tools manually before proceeding:

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 10.0.201 or later | https://dotnet.microsoft.com/download/dotnet/10.0 |
| Node.js | 22 LTS or later | https://nodejs.org |
| pnpm | 10.33.0 or later | `npm install -g pnpm@10.33.0` |
| PostgreSQL | 17 | https://www.postgresql.org/download |

> **Verify installs:**
> ```bash
> dotnet --version   # should print 10.x.x
> node --version     # should print v22.x.x
> pnpm --version     # should print 10.x.x
> psql --version     # should print 17.x
> ```

---

### Step 1 — Create the PostgreSQL database

Start the PostgreSQL service (method varies by OS), then run:

```bash
psql -U postgres
```

Inside the psql shell:

```sql
CREATE USER erp WITH PASSWORD 'erp_dev';
CREATE DATABASE erp OWNER erp;
\q
```

---

### Step 2 — Configure the backend

Open `apps/api/src/Erp.Web/appsettings.Development.json` and set the connection string:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=erp;Username=erp;Password=erp_dev"
  }
}
```

The file already contains a dev JWT signing key and seed owner credentials:

```json
{
  "Seed": {
    "Owner": {
      "Email": "owner@uft-juan.local",
      "Password": "Owner12345",
      "FullName": "System Owner"
    }
  }
}
```

Change these values before deploying to any shared or production environment.

---

### Step 3 — Run EF Core migrations

From the repo root (`erp/`):

```bash
dotnet restore Erp.slnx
dotnet ef database update \
  --project apps/api/src/Erp.Infrastructure \
  --startup-project apps/api/src/Erp.Web
```

This creates all tables, applies the schema, and seeds the initial roles and owner account.

---

### Step 4 — Start the backend API

```bash
dotnet run --project apps/api/src/Erp.Web
```

API listens on `http://localhost:5180` by default.

- Health check: http://localhost:5180/health
- API docs: http://localhost:5180/scalar/v1

---

### Step 5 — Configure the frontend

```bash
cp apps/web/.env.example apps/web/.env.development
```

`apps/web/.env.development` should contain:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5180
```

---

### Step 6 — Install frontend dependencies and start dev server

From the repo root (`erp/`):

```bash
pnpm install
pnpm --filter web dev
```

Frontend runs on http://localhost:3000.

---

### Optional Services

The following services are used but not required to start the app. Features that depend on them will be disabled or will fail gracefully.

| Service | Purpose | Manual setup |
|---------|---------|-------------|
| SMTP server | Email delivery | Any local SMTP (e.g. [smtp4dev](https://github.com/rnwood/smtp4dev), Mailpit). Set `Smtp.*` in appsettings. |
| MinIO | File/object storage | Install from https://min.io/download. Set `Minio.*` in appsettings. |

---

### Default Credentials (development)

| Field | Value |
|-------|-------|
| Email | `owner@uft-juan.local` |
| Password | `Owner12345` |

---

## License

See [LICENSE](LICENSE) for details.
