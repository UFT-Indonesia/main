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

- **Backend**: .NET 10, FastEndpoints, EF Core 10, PostgreSQL, ASP.NET Identity + JWT, Hangfire, FluentValidation, Serilog, MailKit, MinIO, Wolverine, Specifications (Query)
- **Frontend**: Next.js 15 (App Router), TypeScript strict, Tailwind, shadcn/ui, TanStack Query/Table, Zod, react-hook-form, next-intl
- **Locale**: id-ID (Asia/Jakarta), Currency IDR
- **Hardware**: ESP32-S3 + R503 fingerprint (HMAC-signed HTTP push)

## 🧱 Architecture

- **Outside Core** — Clean Architecture (Ardalis): `Erp.Web` → `Erp.Infrastructure` → `Erp.UseCases` → `Erp.Core` → `Erp.SharedKernel`
- **Inside Core** — DDD tactical (aggregates, VOs, domain events, specs)
- **Approvals** — generic `ApprovalEngine` + `IApprovalStrategy<T>` per module (Strategy Pattern, no MediatR)

## 📋 Getting Started

⚠️ Make sure `.env` file is setup before implementing. Please use the `.env.example` file for reference.

```bash
docker compose up -d                         # postgres, smtp4dev, minio
dotnet restore Erp.sln
dotnet ef database update -p apps/api/src/Erp.Infrastructure -s apps/api/src/Erp.Web
dotnet run --project apps/api/src/Erp.Web
pnpm --filter web dev
```

## 📄 License

See [LICENSE](LICENSE) for details.
