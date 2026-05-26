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

## 🎯 Project Goals

| Priority | Module | Status |
|----------|--------|--------|
| ⭐ Core | **Attendance** | Primary module — first to be developed |
| 2 | **Accounting** | Financial ledger, journal entries, and reconciliation |
| 3 | **Reporting** | Business intelligence and operational reports |
| 4 | **Inventory** | Stock management, warehousing, and procurement |
| 5 | **Payments** | Invoice processing and payment gateway integration |

## 🏗️ Tech Stack

- **Backend**: .NET 10, FastEndpoints 6.1, EF Core 10, PostgreSQL 17, ASP.NET Identity + JWT, FluentValidation, Serilog, Hangfire, Wolverine messaging, Scalar OpenAPI
- **Frontend**: Next.js 15 (App Router), React 19, TypeScript strict, Tailwind CSS v4, shadcn/ui, TanStack Query/Table, Zod, react-hook-form, next-intl, Zustand
- **Locale**: id-ID (Asia/Jakarta), Currency IDR
- **Hardware**: ESP32-S3 + R503 fingerprint (HMAC-signed HTTP push)

## 🧱 Architecture

- **Outside Core** — Clean Architecture (Ardalis): `Erp.Web` → `Erp.Infrastructure` → `Erp.UseCases` → `Erp.Core` → `Erp.SharedKernel`
- **Inside Core** — DDD tactical (aggregates, VOs, domain events, specs)
- **Approvals** — generic `ApprovalEngine` + `IApprovalStrategy<T>` per module (Strategy Pattern, no MediatR)

## ✅ Current Status

### Backend API

- **Authentication** — JWT access tokens + secure httpOnly refresh-token cookies; login, logout, refresh, and `/me` endpoints
- **Identity Seeding** — Owner/Manager/Staff roles auto-created on startup; initial owner seeded from configuration (no public bootstrap endpoint)
- **Employees** — Full CRUD with pagination, search, and filters; domain events for salary, parent, and basic-info changes
- **Attendance** — HMAC-signed device log ingestion (`/api/attendance/device-logs`) and manual entry (`/api/attendance/manual-logs`) with PunchType In/Out
- **Health Check** — `/health` endpoint
- **API Docs** — Scalar OpenAPI reference at `/scalar/v1`
- **Messaging** — Wolverine bus with PostgreSQL message persistence and EF Core transaction integration

### Frontend (apps/web)

- **Login** — JWT session management with Zustand store
- **Employees** — List view with pagination, search, role/status filters; create, edit, and delete (with optional termination date)
- **UI** — shadcn/ui component library, dark/light theming ready
- **I18n** — Indonesian locale (id-ID) via next-intl

### Tests

- **Unit Tests** (`Erp.UnitTests`) — 52+ tests covering domain entities, use cases, and infrastructure (device ingest validator, refresh token service)
- **Integration Tests** (`Erp.IntegrationTests`) — project scaffolded, awaiting tests

## 📋 Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js + pnpm
- Docker & Docker Compose

### Environment

Copy the frontend environment file:

```bash
cp apps/web/.env.example apps/web/.env.development
```

The API reads configuration from `apps/api/src/Erp.Web/appsettings.Development.json` and optional `.env` files.

### Running the Stack

```bash
docker compose up -d                         # postgres, smtp4dev, minio
dotnet restore Erp.sln
dotnet ef database update -p apps/api/src/Erp.Infrastructure -s apps/api/src/Erp.Web
dotnet run --project apps/api/src/Erp.Web
pnpm --filter web dev
```

## 📄 License

See [LICENSE](LICENSE) for details.
