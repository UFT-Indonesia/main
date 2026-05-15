# ERP Architecture Modernization Specification

## Scope
Replace direct DbContext usage with Wolverine messaging + Ardalis Specification repositories + strongly-typed IDs for domain entities. Reset migrations for PascalCase table names.

## Decisions Locked In
- **HTTP Framework**: FastEndpoints (keep current; Wolverine.HTTP not used)
- **Typed IDs**: Domain entities only (`Employee`, `AttendanceLog`). Identity entities (`ApplicationUser`, `IdentityRole`, etc.) keep raw `Guid` to avoid Identity framework conflicts.

## Project Structure (Keep 4-Layer)
```
Erp.SharedKernel          → TypedId base, Result types, Specifications
Erp.Core                  → Domain entities with strongly-typed IDs, Aggregate roots, empty IRepository<T> interfaces
Erp.UseCases              → Wolverine command/query handlers, DTOs
Erp.Infrastructure        → EF repos, DbContext, configurations, Wolverine EF middleware, Identity
Erp.Web                   → FastEndpoints (dispatch to Wolverine), Program.cs wiring
```

## 1. Packages

### Directory.Packages.props additions:
```xml
<PackageVersion Include="WolverineFx" Version="5.39.0" />
<PackageVersion Include="WolverineFx.EntityFrameworkCore" Version="5.39.0" />
<PackageVersion Include="WolverineFx.Postgresql" Version="5.39.0" />
<PackageVersion Include="Ardalis.Specification" Version="9.1.0" />
<PackageVersion Include="Ardalis.Specification.EntityFrameworkCore" Version="9.1.0" />
```

### Erp.UseCases.csproj:
```xml
<PackageReference Include="WolverineFx" />
```

### Erp.Infrastructure.csproj:
```xml
<PackageReference Include="WolverineFx" />
<PackageReference Include="WolverineFx.EntityFrameworkCore" />
<PackageReference Include="Ardalis.Specification.EntityFrameworkCore" />
```

### Erp.Web.csproj:
```xml
<!-- WolverineFx included via UseCases ref chain, but add explicitly for IMessageBus usage -->
<PackageReference Include="WolverineFx" />
```

## 2. Strongly-Typed IDs

Create in `Erp.SharedKernel/Identity/`:

```csharp
public readonly record struct EmployeeId(Guid Value);
public readonly record struct AttendanceLogId(Guid Value);
// ApplicationUser.Id stays Guid (Identity framework constraint)
```

Update entities:
- `Employee` → `public EmployeeId Id { get; private set; }`
- `AttendanceLog` → `public AttendanceLogId Id { get; private set; }`
- Update constructors, factory methods, and all references.

EF Core value converters in `Persistence/ValueConverters/`:
```csharp
public class EmployeeIdConverter : ValueConverter<EmployeeId, Guid>
{
    public EmployeeIdConverter() : base(id => id.Value, v => new EmployeeId(v)) { }
}
```
Register converters in entity configurations.

## 3. Repository Abstraction (Ardalis.Specification)

### Erp.Core/Interfaces/IRepository.cs
```csharp
public interface IRepository<T> : IRepositoryBase<T> where T : class { }
public interface IReadRepository<T> : IReadRepositoryBase<T> where T : class { }
```

### Erp.Infrastructure/Persistence/EfRepository.cs
```csharp
public class EfRepository<T> : RepositoryBase<T>, IRepository<T>, IReadRepository<T>
    where T : class
{
    public EfRepository(AppDbContext dbContext) : base(dbContext) { }
}
```

Register in DI (`Erp.Infrastructure/DependencyInjection.cs`):
```csharp
services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
```

## 4. PascalCase Table Names

Update ALL `ToTable()` calls:
- `attendance_logs` → `AttendanceLogs`
- `employees` → `Employees`
- `auth_users` → `AuthUsers`
- `auth_roles` → `AuthRoles`
- `auth_user_roles` → `AuthUserRoles`
- `auth_user_claims` → `AuthUserClaims`
- `auth_user_logins` → `AuthUserLogins`
- `auth_role_claims` → `AuthRoleClaims`
- `auth_user_tokens` → `AuthUserTokens`

## 5. Migration Reset

1. Delete `Erp.Infrastructure/Persistence/Migrations/` entirely
2. Ensure `DbContext` has correct `Database.EnsureCreated()` or use `dotnet ef migrations add InitialCreate`
3. Generate new `InitialCreate` migration

## 6. Wolverine Configuration

### Erp.Web/Program.cs
Replace `builder.Host.UseSerilog(...)` block with Wolverine host builder:
```csharp
builder.Host.UseWolverine(opts =>
{
    // Full messaging scope
    opts.Durability.Mode = DurabilityMode.Balanced;

    // Auto-discover handlers from UseCases + Web
    opts.Discovery.IncludeAssembly(typeof(Erp.UseCases.AssemblyMarker).Assembly);
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // EF Core transactional middleware
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.UseDurableLocalQueues();
});
```

**Note**: Keep existing `UseSerilog`, `AddFastEndpoints`, `AddConfiguredJwtBearer`, `AddInfrastructure` — add Wolverine alongside.

## 7. Handler Pattern (Wolverine)

Each command/query = one handler class with static `Handle` method.

Example — Create Employee:

**Erp.UseCases/Employees/CreateEmployee.cs**:
```csharp
public record CreateEmployeeCommand(string Name, string Npwp, string? Phone, string? Address);
public record EmployeeResponse(EmployeeId Id, string Name, string Npwp);

public static class CreateEmployeeHandler
{
    public static async Task<EmployeeResponse> Handle(
        CreateEmployeeCommand cmd,
        IRepository<Employee> repository,
        CancellationToken ct)
    {
        var employee = Employee.Create(cmd.Name, cmd.Npwp, cmd.Phone, cmd.Address);
        await repository.AddAsync(employee, ct);
        await repository.SaveChangesAsync(ct);
        return new EmployeeResponse(employee.Id, employee.Name, employee.Npwp);
    }
}
```

**Pattern rules**:
- Use static methods with method injection for services
- Return DTOs directly (cascading messages for future domain events)
- Keep call stacks short — handler calls repository + domain logic, nothing deeper
- For fire-and-forget commands that don't need a response, return `Task` (no return type)

**Checkpoint (2026-05-14)**: Actual implementation uses instance classes with constructor injection instead of static classes with method injection. Example:
```csharp
public sealed class RecordManualLogHandler
{
    private readonly IReadRepository<Employee> _employees;
    private readonly IRepository<AttendanceLog> _attendanceLogs;

    public RecordManualLogHandler(
        IReadRepository<Employee> employees,
        IRepository<AttendanceLog> attendanceLogs)
    {
        _employees = employees;
        _attendanceLogs = attendanceLogs;
    }

    public Task<Result<AttendanceResult>> Handle(
        RecordManualLogCommand command,
        CancellationToken ct) =>
        AttendanceLogService.RecordAsync(...);
}
```
Rationale: Constructor injection provides better testability and aligns with Wolverine's support for both patterns. Future handlers should follow this instance class pattern.

## 8. Endpoint Refactor (FastEndpoints → Wolverine)

Each endpoint becomes a thin HTTP shell that dispatches to Wolverine.

**Before** (direct DbContext):
```csharp
public override async Task HandleAsync(ManualAttendanceLogRequest req, CancellationToken ct)
{
    // business logic here
    _dbContext.AttendanceLogs.Add(log);
    await _dbContext.SaveChangesAsync(ct);
}
```

**After** (Wolverine mediator):
```csharp
public sealed class RecordManualLogEndpoint : Endpoint<ManualAttendanceLogRequest, AttendanceLogResponse>
{
    private readonly IMessageBus _bus;

    public RecordManualLogEndpoint(IMessageBus bus) => _bus = bus;

    public override void Configure()
    {
        Post("/manual-logs");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(ManualAttendanceLogRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) { await SendUnauthorizedAsync(ct); return; }

        var result = await _bus.InvokeAsync<AttendanceLogResponse>(
            new RecordManualAttendanceLogCommand(uid, req.EmployeeId, req.PunchedAtUtc, req.PunchType, req.Note), ct);

        await SendAsync(result, cancellation: ct);
    }
}
```

## 9. Existing Features to Migrate

| Feature | Current | Target |
|---------|---------|--------|
| Auth Login | `LoginEndpoint` direct `UserManager` | Keep direct Identity in endpoint (auth is infrastructure, not domain) |
| Auth Me | `MeEndpoint` direct `UserManager` | Keep direct Identity in endpoint |
| Device Log | `RecordDeviceLogEndpoint` direct `DbContext` | Move to `RecordDeviceLogHandler` in UseCases, use `IRepository<AttendanceLog>` |
| Manual Log | `RecordManualLogEndpoint` direct `DbContext` | Move to `RecordManualLogHandler` in UseCases, use `IRepository<AttendanceLog>` |
| Health | `HealthEndpoint` | Keep as-is (infrastructure check) |

**Note**: Auth endpoints should stay direct because ASP.NET Core Identity is inherently infrastructure. Don't abstract Identity behind Wolverine handlers.

## 10. Folder Conventions

```
Erp.UseCases/
  Employees/
    CreateEmployee.cs
    GetEmployeeById.cs
    ListEmployees.cs
    UpdateEmployee.cs
    DeleteEmployee.cs
  Attendance/
    RecordDeviceLog.cs
    RecordManualLog.cs
    GetAttendanceLogById.cs
    ListAttendanceLogs.cs

Erp.Web/Endpoints/
  Auth/
    AuthGroup.cs
    LoginEndpoint.cs
    MeEndpoint.cs
  Attendance/
    AttendanceGroup.cs
    RecordDeviceLogEndpoint.cs
    RecordManualLogEndpoint.cs
    HealthEndpoint.cs
```

## 11. Testing Strategy

- **Unit tests**: Test handlers in isolation by calling static `Handle` methods with fake repositories (in-memory list backed `IRepository<T>`)
- **No mocks for Wolverine**: Use cascading messages + pure functions; test business logic directly
- **Integration tests**: Use `Alba` or `WolverineFx` test harness for full pipeline testing

## 12. Order of Implementation

1. Add packages to Directory.Packages.props
2. Create strongly-typed ID value objects in SharedKernel
3. Update Core entities to use typed IDs
4. Add EF value converters + update configurations with PascalCase tables
5. Add `IRepository<T>` / `IReadRepository<T>` interfaces to Core
6. Implement `EfRepository<T>` in Infrastructure
7. Reset migrations → generate new `InitialCreate`
8. Add Wolverine to `Program.cs` and DI
9. Create UseCases handlers for Attendance/Employee features
10. Refactor Web endpoints to dispatch to Wolverine handlers
11. Verify build + tests pass

## 13. Out of Scope (Future Branch)

- Domain Events (e.g., `AttendanceLogRecorded` → notification handler)
- Background job processing (Wolverine local queues + durable inbox)
- Sagas / long-running workflows
- Event sourcing with Marten/Polecat
<<<<<<< Updated upstream
=======
<<<<<<< Updated upstream
=======
>>>>>>> Stashed changes

## 14. Checkpoints

### 2026-05-14 — Employee CRUD landed

**Delivered**:
- Domain: `Employee.UpdateBasicInfo(string fullName, Npwp? npwp)` + `EmployeeBasicInfoChanged` domain event. Idempotent when nothing changes; rejects blank name and terminated employees. Trims input.
- Use cases (instance handlers, folder-per-use-case under `Erp.UseCases/Employees/`):
  - `Common/EmployeeResult.cs`, `Common/EmployeeMapper.cs` (internal mapper)
  - `CreateEmployee/{CreateEmployeeCommand, CreateEmployeeHandler}`
  - `GetEmployeeById/{GetEmployeeByIdQuery, GetEmployeeByIdHandler}`
  - `ListEmployees/{ListEmployeesQuery, ListEmployeesResult, ListEmployeesHandler, EmployeeListSpec}` — Ardalis specs with paging (default 20, max 100), case-insensitive search on `FullName`/`Nik`, role + status filters
  - `UpdateEmployee/{UpdateEmployeeCommand, UpdateEmployeeHandler}` — full-state PUT, dispatches to `UpdateBasicInfo` + `ChangeSalary` + `ChangeRole` + `AssignParent`, ordering tuned for invariants (Owner-target sets parent first; non-Owner-target sets role first)
  - `DeleteEmployee/{DeleteEmployeeCommand, DeleteEmployeeHandler}` — semantic delete = `Employee.Terminate()`; uses NodaTime `IClock` when termination date omitted
- FastEndpoints under `Erp.Web/Endpoints/Employees/`: `EmployeeGroup` (`/api/employees`), Create (POST), Get (GET `/{id}`), List (GET), Update (PUT `/{id}`), Delete (DELETE `/{id}`). All `[Authorize]` with `// TODO: Enforce RBS permission check` markers on mutating endpoints.
- Specs spec `EmployeeListSpec` lives in `Erp.UseCases` and uses `e.FullName.ToLower().Contains(...)` instead of `EF.Functions.ILike` to keep UseCases provider-agnostic.
- Tests: +44 unit tests (52 → 96, all passing) covering domain `UpdateBasicInfo` and all five handlers (success, validation errors, not-found, role/status/wage edge cases).

**Incidental cleanup**:
- Removed redundant `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Options.ConfigurationExtensions` from `Erp.Infrastructure.csproj` (NU1510 — covered by `Microsoft.AspNetCore.App` framework reference; was blocking restore under .NET 10 SDK).

**Known gaps surfaced during CRUD work** (not addressed — out of scope):
- **RBS permission checks**: Create/Update/Delete endpoints carry TODO markers; no role-based gating yet beyond `[Authorize]`.
- **Role transition Owner ↔ non-Owner**: `Employee.ChangeRole` requires invariant satisfied at call time, but cannot atomically change role + parent. Cross-tier transitions surface as `Result.Error` with the relevant domain code. Future work: add an atomic `Employee.PromoteToOwner()` / `Demote(parent)` API or relax `ChangeRole` to accept a parent argument.
- **NIK changes**: NIK is immutable post-creation (no domain method). Update endpoint does not expose it.
- **Hard delete**: Not supported. Aggregate exposes only `Terminate`. Add a separate command if hard-delete is ever required.
- **Employee parent depth ≤ 2 + cycle detection**: Not enforced anywhere; pre-existing gap noted in roadmap.
- **List query**: No `IncludeTerminated` flag yet — callers must pass `Status=Active` explicitly to exclude terminated employees.
- **Pagination**: Returns `TotalCount` but no `HasMore`/cursor; offset-based only.
<<<<<<< Updated upstream
=======

### 2026-05-15 — Frontend phase landed (P0.2 + P1a.5)

**Stack confirmed in `apps/web/`** (Next.js 15, React 19, Tailwind v4):
- Routing: App Router with `typedRoutes: true`
- Data: TanStack Query v5
- Forms: React Hook Form + Zod
- Auth state: Zustand with `persist` middleware (localStorage, key `erp-auth`)
- HTTP: Axios with bearer-token interceptor + 401 auto-logout redirect
- i18n: next-intl (id default, en alt; cookie-driven `NEXT_LOCALE`)
- Icons: lucide-react
- UI: hand-rolled shadcn-style primitives (no Radix dep) — `Button`, `Input`, `Label`, `Card`, `Badge`, `Skeleton`, `Table`, `Select`, `Dialog`, `Toaster`

**Delivered**:
- Theme: extended `src/styles/globals.css` with full shadcn-equivalent CSS variable set (`card`, `secondary`, `accent`, `destructive`, `success`, `warning`, `input`, `ring`, `radius`) and `@theme inline` exposure for Tailwind v4 utilities.
- API layer (`src/lib/api/`): `client.ts` (axios instance + request/response interceptors + `extractApiError` normaliser), `auth.ts` (`login`, `fetchMe`), `employees.ts` (`listEmployees`, `getEmployee`, `createEmployee`, `updateEmployee`, `deleteEmployee`), `types.ts` (DTOs mirroring backend contracts).
- Auth (`src/lib/auth/`): `store.ts` (zustand persisted store with `hydrated` flag), `use-auth.ts` (`useAuth`, `useRequireAuth`, `useRedirectIfAuthenticated`).
- Hooks (`src/hooks/`): `use-employees.ts` (5 react-query hooks with proper cache invalidation), `use-toast.ts` (zustand-backed toast store with `success`/`error`/`info` shorthands).
- UI primitives (`src/components/ui/`): 10 components, all CVA-driven where applicable, fully typed, ref-forwarded where needed.
- Layout (`src/components/layout/`): `Sidebar` (client-side active-route highlighting), `Topbar` (user info + logout), `AppShell` (wraps protected routes with `useRequireAuth` guard + skeleton fallback).
- Employee components (`src/components/employees/`): `EmployeeForm` (RHF + Zod, role/parent invariant validation matching backend), `EmployeeTable` (status badges, IDR formatting via `Intl.NumberFormat`, edit/delete actions), `EmployeeFilters` (search + role + status, debounce-friendly), `DeleteEmployeeDialog` (confirmation with optional termination date).
- Pages:
  - `app/login/page.tsx` — public, redirects authenticated users to `/`
  - `app/page.tsx` — protected dashboard placeholder
  - `app/employees/page.tsx` — list + filters + paging (default 20/page, server-side filtering)
  - `app/employees/new/page.tsx` — create form
  - `app/employees/[id]/page.tsx` — detail/edit + inline terminate
- i18n: `messages/en.json` and `messages/id.json` extended with `nav`, `login`, `employees.*` (form, create, detail, delete, filters, pagination) and `common` (back, previous, next).
- Toaster mounted globally in `app/layout.tsx`.

**Verification**:
- `pnpm --filter web typecheck`: ✅ 0 errors
- `pnpm --filter web build`: ✅ all 6 routes compile (`/`, `/login`, `/employees`, `/employees/new`, `/employees/[id]`, `/_not-found`); first-load JS 105 kB shared, largest page 196 kB.
- `pnpm --filter web lint`: ❌ pre-existing failure — `eslint-plugin-react-hooks` missing from lockfile (config references it but package not installed). Not introduced by this phase. Fix in a future devx pass.

**Frontend conventions established** (apply to all future modules):
- Pages: client-side, `'use client'` at top; protected pages render inside `<AppShell>` which gates on token + hydration.
- Server state: react-query keys namespaced per resource (`employeeKeys.all/list/detail`); mutations invalidate `lists()` and update `detail(id)` cache.
- Forms: zod schema + RHF; submit handlers use `mutateAsync` with toast on success/error and use `extractApiError` to normalize backend `{ code, message }` payloads.
- Auth: token+user+expiry in `useAuthStore`; axios interceptor injects bearer; 401 triggers `clear()` + hard redirect to `/login`.
- Routes: typed via `next` `Route` import; `Link` cast as `Route` for dynamic paths.
- i18n: namespace per page (`employees`, `login`, `nav`); options dictionaries (`roleOptions`, `statusOptions`) keyed by backend enum values.

**Known gaps surfaced during frontend work** (out of scope):
- **Refresh token flow**: backend has no refresh endpoint; FE just hard-logs-out on 401. Address with future Refresh phase.
- **Server actions / SSR data**: all FE pages are client-rendered; no Next.js server actions or RSC data fetching yet. Acceptable for internal ERP.
- **RBAC UI**: no role-gated rendering (e.g. hiding "Add employee" for Staff). Wire up after backend RBS lands.
- **Eslint plugin missing**: `eslint-plugin-react-hooks` referenced by `eslint-config-next` but not in `package.json`. Lint command broken until added.
- **Locale switcher**: no UI toggle yet; locale only switchable via cookie manually.
- **Dark mode**: theme CSS vars defined but no `.dark` overrides or toggle.
- **E2E tests**: `tests/e2e/` empty — Playwright config exists but no spec files.
- **Employee list UX**: no debounce on search input (every keystroke fires query); no column sort; no parent-name resolution (only shows parent ID).
- **Employee form**: parent picker is a free-text UUID field — should become a typeahead picker pulling from `listEmployees`.
- **Optimistic updates**: mutations rely on invalidation only; consider optimistic UI for create/update.
>>>>>>> Stashed changes
>>>>>>> Stashed changes
