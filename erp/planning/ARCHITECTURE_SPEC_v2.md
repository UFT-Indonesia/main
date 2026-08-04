# ERP Architecture Modernization Specification (v2)

> Supersedes `ARCHITECTURE_SPEC.md` as the living document. Sections 1–15 are carried over verbatim (with the merge-conflict markers from the old file cleaned up — no content was dropped, both stashed checkpoints are kept in sequence). Sections 16–18 are new: an implementation plan for the two open TODO groups you asked to finish, plus three new backlog items from your 2026-08-04 notes. **Nothing in this document has been implemented yet — planning only.**

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
- **RBS permission checks**: Create/Update/Delete endpoints carry TODO markers; no role-based gating yet beyond `[Authorize]`. *(See §16 — this is one of the two items you asked to finish.)*
- **Role transition Owner ↔ non-Owner**: `Employee.ChangeRole` requires invariant satisfied at call time, but cannot atomically change role + parent. Cross-tier transitions surface as `Result.Error` with the relevant domain code. Future work: add an atomic `Employee.PromoteToOwner()` / `Demote(parent)` API or relax `ChangeRole` to accept a parent argument.
- **NIK changes**: NIK is immutable post-creation (no domain method). Update endpoint does not expose it.
- **Hard delete**: Not supported. Aggregate exposes only `Terminate`. Add a separate command if hard-delete is ever required.
- **Employee parent depth ≤ 2 + cycle detection**: Not enforced anywhere; pre-existing gap noted in roadmap. *(Note: this was subsequently closed — see the 2026-05-18 entry in §15.)*
- **List query**: No `IncludeTerminated` flag yet — callers must pass `Status=Active` explicitly to exclude terminated employees.
- **Pagination**: Returns `TotalCount` but no `HasMore`/cursor; offset-based only.

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
- **Refresh token flow**: backend has no refresh endpoint; FE just hard-logs-out on 401. Address with future Refresh phase. *(Note: `RefreshToken` aggregate + `AddRefreshTokens` migration exist in the codebase now — verify this is closed before treating it as open.)*
- **Server actions / SSR data**: all FE pages are client-rendered; no Next.js server actions or RSC data fetching yet. Acceptable for internal ERP.
- **RBAC UI**: no role-gated rendering (e.g. hiding "Add employee" for Staff). Wire up after backend RBS lands. *(See §16.)*
- **Eslint plugin missing**: `eslint-plugin-react-hooks` referenced by `eslint-config-next` but not in `package.json`. Lint command broken until added.
- **Locale switcher**: no UI toggle yet; locale only switchable via cookie manually.
- **Dark mode**: theme CSS vars defined but no `.dark` overrides or toggle.
- **E2E tests**: `tests/e2e/` empty — Playwright config exists but no spec files.
- **Employee list UX**: no debounce on search input (every keystroke fires query); no column sort; no parent-name resolution (only shows parent ID).
- **Employee form**: parent picker is a free-text UUID field — should become a typeahead picker pulling from `listEmployees`.
- **Optimistic updates**: mutations rely on invalidation only; consider optimistic UI for create/update.

## 15. Known Scaling TODOs

### Hierarchy mutation advisory lock (`Erp.Infrastructure/Persistence/Hierarchy/PgEmployeeHierarchyLookup.cs`)
- **Current**: single global `pg_advisory_xact_lock(7_982_465_318_127_493_021)` for every employee Create/AssignParent that touches the parent chain. Held for the lifetime of the Wolverine handler transaction; auto-released on commit/rollback.
- **Rationale**: ensures depth/cycle checks see a consistent ancestry snapshot. Acceptable at current scale (≪ 1 reparent/sec, single tenant).
- **Triggers to revisit**:
  - **Multi-tenancy**: shard key by tenant id, e.g. `hashtextextended('hier:' || tenantId, 0)`.
  - **Sustained > 5 reparents/sec**: shard key by Owner-subtree root id so unrelated trees don't serialize.
  - **Heavy subtree mutations**: switch to `ltree` materialized path; cycle becomes structurally impossible and the lock can be dropped entirely.
- **Safety cap**: `EmployeeHierarchyPolicy.MaxAncestryWalk = 8`. CTE truncates ancestry chains beyond this; `EmployeeHierarchyService` raises `employee.hierarchy_corrupted` so corrupted/cyclic data fails loud instead of silently passing depth validation.

### 2026-05-18 — RBS depth + cycle validation landed
- Domain: `Employee.Create` and `Employee.AssignParent` now accept an optional `parentAncestors` collection and reject `employee.depth_exceeded` (depth > `MaxDepth = 2`) and `employee.parent_cycle` (self appears in chain).
- Abstraction: `IEmployeeHierarchyLookup` in `Erp.Core/Interfaces` (lock + ancestor read).
- Use case: `EmployeeHierarchyService.ResolveAncestorsForParentAsync` orchestrates lock-then-read, returns the candidate parent's ancestors only (not the parent itself), and surfaces `employee.hierarchy_corrupted` when the safety cap fires so callers pass the candidate parent separately to avoid over-counting depth.
- Handlers: `CreateEmployeeHandler` and `UpdateEmployeeHandler` resolve ancestors via the service before calling the aggregate. Update skips the lock when parent is unchanged.
- Infrastructure: `PgEmployeeHierarchyLookup` runs a recursive CTE bounded by `MaxAncestryWalk` and uses `pg_advisory_xact_lock` (see TODO above).
- Tests: 96 → 108 unit tests; new coverage for depth/cycle on the aggregate, service ordering and corruption propagation, and handler-level depth rejection.

---

## 16. Planned Work — RBS Permission Enforcement (Employee Update/Delete)

**Current state**: `UpdateEmployeeEndpoint` and `DeleteEmployeeEndpoint` both already carry `[Authorize(Roles = "Owner")]`, plus a `// TODO: Enforce RBS permission check` comment left over from the 2026-05-14 checkpoint. The attribute already blocks Manager and Staff outright — so the TODO is stale in the narrow sense (there *is* a check), but the real gap it was pointing at is still open: there's no scoped permission model, just a blanket Owner-only gate. Get/List Employee endpoints are the same (`[Authorize(Roles = "Owner")]`), though those weren't called out in your TODO list.

**Precedent already in the codebase**: `Erp.Web/Endpoints/Accounts/AccountContracts.cs` defines exactly this kind of rule for account provisioning:
```csharp
public static class AccountRules
{
    // Owner manages any account; Manager only Staff-role accounts.
    public static bool CanManage(ClaimsPrincipal caller, EmployeeRole targetRole) =>
        caller.IsInRole(nameof(EmployeeRole.Owner))
        || (caller.IsInRole(nameof(EmployeeRole.Manager)) && targetRole == EmployeeRole.Staff);
}
```
`CreateAccountEndpoint` and `ProvisionCandidatesEndpoint` both call `AccountRules.CanManage`. Employee mutation endpoints don't follow this pattern yet — they just refuse Manager entirely.

**Proposed plan**:
1. Relax `[Authorize(Roles = "Owner")]` to `[Authorize(Roles = "Owner,Manager")]` on `UpdateEmployeeEndpoint` and `DeleteEmployeeEndpoint`.
2. Add an `EmployeeRules.CanManage(ClaimsPrincipal caller, EmployeeRole targetRole)` helper mirroring `AccountRules.CanManage` verbatim (Owner → any role; Manager → Staff only).
3. In each endpoint's `HandleAsync`, look up the target employee's current role before dispatching the command, call `EmployeeRules.CanManage(User, employee.Role)`, and `SendForbiddenAsync` if it fails — same shape as `CreateAccountEndpoint`. (Needs one extra read — either a lightweight query before the command, or have the handler itself return a `Forbidden` result variant.)
4. Replace both `// TODO` comments with a short doc comment pointing at `AccountRules` for consistency.
5. Tests: extend the existing Update/Delete handler test suites with Owner-can-manage-anyone, Manager-can-manage-Staff-only (forbidden on Owner/Manager targets), Staff-has-no-access (already covered by `[Authorize]`) cases — mirroring whatever test coverage exists for `CreateAccountEndpoint`.

**Open decision for you**: should Manager gain *any* Update/Delete rights on Employees, or should Employee mutation stay Owner-only permanently (unlike Accounts)? If the latter, "finishing" this TODO is just deleting the comment and documenting the decision in code — no behavior change needed. My inclination is to mirror `AccountRules` for consistency across the two modules, but this is your call, not a technical one.

## 17. Planned Work — Domain Event Handlers

All five stub handlers currently do nothing but return `Task.CompletedTask` (or, for `AttendanceLogRecordedHandler`, do the one real thing they're needed for — the `AttendanceDay` recompute — and stub the rest). None of them can send a real webhook or notify payroll/supervisors today because **no such external integration exists anywhere in the codebase yet** — no outbound HTTP client config, no notification service, no analytics store, no real-time channel. So "finish the TODO" splits into two buckets: things buildable now with what already exists, and things that need a new integration decided first.

| Handler | TODO as written | What's realistically shippable now | What's blocked on a future decision |
|---|---|---|---|
| `EmployeeCreatedHandler` | Log creation, send webhook, notify external systems | Structured log line (`Log.Information` with EmployeeId/FullName/Role) | Webhook/external notify — no subscriber registry or target system exists yet |
| `EmployeeBasicInfoChangedHandler` | Audit trail, compliance logging | **Yes** — event already carries old/new FullName + Npwp; write to a new `EmployeeAuditLog` table | — |
| `EmployeeSalaryChangedHandler` | Payroll notification, accounting system | Route into the same `EmployeeAuditLog` table (old/new wage + effective date) as an interim measure | Actual payroll/accounting system integration — nothing to call yet |
| `EmployeeParentChangedHandler` | Org chart update, recalculate reporting structure | Route into the same `EmployeeAuditLog` table (who reported to whom, when) | A standalone "org chart" projection — note that `ParentId` **is** the live reporting structure already, correctly recalculated at the domain level (`EmployeeHierarchyService`) the moment `AssignParent` succeeds. There's no separate cache/projection anywhere to "recalculate" — building one would be new scope, not a stub fix, unless there's a concrete org-chart UI/report planned |
| `AttendanceLogRecordedHandler` | Real-time dashboard push, notify supervisors, update analytics | Nothing beyond the existing recompute — this TODO needs infrastructure decisions first (SignalR hub vs. polling? which analytics store? what counts as "supervisor notify"?) | All three |

**Recommended shape**: build one generalized `EmployeeAuditLogHandler` (or three small handlers sharing one `EmployeeAuditLog` table/repository) covering `BasicInfoChanged`, `SalaryChanged`, `ParentChanged` — this is concrete, has no external dependency, and directly answers 3 of the 5 stubs. Make `EmployeeCreatedHandler` a real log statement (webhook explicitly deferred). Leave `AttendanceLogRecordedHandler`'s dashboard/notify/analytics TODO explicitly open pending an infrastructure decision — treat it as a separate initiative rather than folding it into this pass.

**Open question for you**: is a shared `EmployeeAuditLog` table (EmployeeId, ChangedAtUtc, EventType, OldValue, NewValue as JSON or per-column) an acceptable v1, or do you want the audit trail to live somewhere else (e.g. exported to an external compliance system)?

## 18. New Backlog Notes (added 2026-08-04)

These three come from your notes today. None of the underlying features exist in the codebase yet — I've grounded each one in what's already built (or explicitly not built) so the gaps are concrete rather than guesses.

### 18.1 Leave Quota Engine

Your shorthand: 12 hari cuti/tahun per employee; mulai dihitung dari bulan masuk (join month), berlaku setelah lulus probation; untuk tahun pertama, sisa bulan dalam tahun sejak lulus probation menentukan jumlah hari cuti yang didapat (prorata).

**Current state**: there is no `LeaveQuota`/`LeaveBalance` concept anywhere — `LeaveRequest` only validates date range and workday count; it never checks a balance or caps annual leave at 12 days. `Employee` has no `HireDate`/join-date field or probation window at all — `EffectiveSalaryFrom` is the closest existing date, but it's semantically about salary (and moves on every raise via `ChangeSalary`), so anchoring a quota calculation to it would silently corrupt the math the next time someone gets a raise.

**What needs to exist before this can be built**:
1. Add `HireDate` (join date) and a probation window (e.g. `ProbationMonths`, configurable) to `Employee`.
2. A `LeaveQuota` concept per employee per year: entitled days, and used days (either stored or derived by summing approved `LeaveRequest.WorkdayCount` where `Type == Annual` for that year).
3. A proration function: while on probation, entitlement is 0; once probation passes, the first calendar year prorates by remaining months (your "sisa tahun (bulan)" — e.g. join in month 6, probation clears mid-year, remaining months of that year determine the fraction of the full 12); subsequent full years get the full 12.
4. `CreateLeaveRequestHandler` needs to check remaining quota before allowing an `Annual`-type request to succeed.

**Open questions for you**:
1. Is probation length fixed company-wide, or configurable per employee/contract?
2. Does entitlement accrue monthly (1/12 per month worked) or land all at once the moment probation passes?
3. Does unused leave carry over into the next year, or expire Dec 31?
4. Do `Sick` / `Permission` / `Unpaid` leave types draw against any quota, or are they unlimited / judgment calls (only `Annual` seems to map to your "12 hari" note)?

### 18.2 Account Provisioning — Employee Self-Service Leave Requests

Good news: account provisioning is already built and working. `Erp.Web/Endpoints/Accounts/` (`CreateAccountEndpoint`, `ProvisionCandidatesEndpoint`, `ListAccountsEndpoint`, `SetAccountEnabledEndpoint`, `ResetAccountPasswordEndpoint`) links an `ApplicationUser` to an `Employee` via `ApplicationUser.EmployeeId`, and any employee — including Staff — can already be given login credentials today.

**The gap**: `LeaveRequest.cs`'s own doc comment currently reads *"no self-service yet — employees have no login accounts"* — that's now stale, since accounts exist. But `CreateLeaveRequestEndpoint` is still `[Authorize(Roles = "Owner,Manager")]`, so a Staff employee with a login still can't submit their own leave request; only an Owner/Manager can file one on their behalf.

**To close this gap**:
1. A self-service path (new endpoint, or a relaxed existing one) that lets an authenticated user submit a `LeaveRequest` for *their own* linked employee — resolved from `ApplicationUser.EmployeeId` via the JWT claim, never from an arbitrary `EmployeeId` in the request body (otherwise a Staff account could file leave on someone else's behalf).
2. Decide whether Staff can also cancel their own pending request (worth checking whether `CancelLeaveRequestEndpoint` is Owner/Manager-only too).
3. Update the stale doc comment in `LeaveRequest.cs` once this ships.

### 18.3 Attendance & Leave — Table/Status/Export Should Be an FK

My reading of "should be an FK," grounded in what's actually in the code: `AttendanceDay.Status` only distinguishes `Complete`/`Incomplete` from punches — there's no concept of "this day was an approved leave day." Separately, `EmployeeStatus.OnLeave` already exists as an enum value and is fully wired through the frontend (filters, badges, translations in `en.json`/`id.json`), but **nothing in the backend ever sets an `Employee`'s status to `OnLeave`** — `LeaveRequest.Approve` / `Deny` / `Cancel` raise no domain event at all today, so nothing downstream ever reacts to a decision. That disconnect is very likely what you're flagging.

**Proposed shape**: introduce a real relational link instead of inferring "on leave" ad hoc:
1. `LeaveRequest.Approve` and `.Cancel` should raise domain events (`LeaveRequestApproved`, `LeaveRequestCancelled`) — they currently raise none.
2. A new handler reacts to `LeaveRequestApproved` and either (a) sets `Employee.Status = OnLeave` for the request's date range with a matching handler on cancel/expiry to revert to `Active`, and/or (b) stamps the covered `AttendanceDay` rows with a nullable `LeaveRequestId` foreign key, so the attendance table, status badge, and CSV export can show "On Leave" backed by a real relation instead of a derived string.

**Open questions for you**:
1. Should `Employee.Status` flip to `OnLeave` for the whole leave range (simple, but a manual attendance correction wouldn't un-flip it early), or should the FK live strictly at the `AttendanceDay` grain and `Employee.Status` drop `OnLeave` entirely (keeping just Active/Terminated)?
2. Multi-day leave crossing a weekend — do we materialize `AttendanceDay` rows for weekend days under leave, or only workdays (matching `LeaveRequest.CountWorkdays`, which already skips Sat/Sun)?
3. Should the export gain a "Leave type / reason" column when a row is FK-linked to an approved leave request?
