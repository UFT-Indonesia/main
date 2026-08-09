# Branch: Domain Event Handlers → Audit Log

## Current state
Four stub handlers in `Erp.Infrastructure/DomainEventHandlers/` do nothing but `return Task.CompletedTask` (`EmployeeCreatedHandler`, `EmployeeBasicInfoChangedHandler`, `EmployeeSalaryChangedHandler`, `EmployeeParentChangedHandler`), aside from `AttendanceLogRecordedHandler` which does its one real job (the `AttendanceDay` recompute) and stubs the rest. Handlers are plain static classes with a `Handle(TEvent, ...)` method — Wolverine discovers them by assembly scan, no DI registration needed. `EmployeeDomainEventPublisher` already forwards all Employee events to the bus; no publisher changes needed since we're not adding new event types.

| Handler | TODO as written | Shippable now | Blocked on future integration |
|---|---|---|---|
| `EmployeeCreatedHandler` | Log creation, webhook, notify external systems | Audit log row (initial snapshot) | Webhook — no target system exists |
| `EmployeeBasicInfoChangedHandler` | Audit trail, compliance logging | **Yes** — event carries old/new FullName + Npwp | — |
| `EmployeeSalaryChangedHandler` | Payroll notification | Audit log row (old/new wage + effective date) | Actual payroll/accounting integration |
| `EmployeeParentChangedHandler` | Org chart update | Audit log row (who reported to whom, when) | Standalone org-chart projection — `ParentId` *is* the live reporting structure already, correctly recalculated via `EmployeeHierarchyService`; nothing separate to "recalculate" |
| `AttendanceLogRecordedHandler` | Real-time dashboard, notify supervisors, analytics | Nothing beyond existing recompute | All three — needs infra decisions (SignalR? polling? which analytics store?) — **explicitly out of scope for this branch** |

## Plan

### Backend
1. New `EmployeeAuditLogs` table (plain `Entity`, not an aggregate — same shape as `AttendancePolicyHistory`): `Id`, `EmployeeId`, `EventType` (string), `OccurredAtUtc` (`Instant`), `OldValueJson`, `NewValueJson` (nullable `jsonb`/text). **Decision: JSON blob, not per-field columns** — one table shape works for all event types without a migration every time a new audited field appears; UI renders a small per-`EventType` formatter instead.
2. Names embedded in the JSON (e.g. old/new parent name, the employee's own name at time of change) are **snapshotted at write time**, not resolved live from the current `Employee` table. The audit trail must show what was true at that moment — a later rename or a deleted employee must not rewrite history.
3. Four thin handlers (`EmployeeCreatedHandler` now included, not left as a Serilog-only stub) each subscribing to their one event, calling a shared write helper (e.g. `EmployeeAuditLogWriter.Write(employeeId, eventType, oldValue, newValue)`). `EmployeeCreatedHandler` writes `NewValue` = initial field snapshot, `OldValue` = null. Keeps the existing one-handler-per-event-type convention (matches `EmployeeRoleChangedHandler` as a live example) rather than one consolidated handler.
4. `AttendanceLogRecordedHandler`: leave the dashboard/notify/analytics TODO as an explicitly separate future initiative — not touched by this branch.
5. Two new Owner-only FastEndpoints, modeled on `ListEmployeesHandler`/`EmployeeListSpec` (paginated spec) and `ExportAttendanceDaysEndpoint` (CSV string-body export):
   - `GET /api/employees/audit-log` — paginated, filters: `employeeId`, `dateFrom`/`dateTo`, `eventType`. `[Authorize(Roles = "Owner")]`.
   - `GET /api/employees/audit-log/export` — CSV, same filters, **capped at 10,000 rows** (error prompting the Owner to narrow filters above that, same shape as `ExportAttendanceDaysHandler`'s row cap). `[Authorize(Roles = "Owner")]`.
   - Server-side role enforcement is required regardless of the frontend gate — a Manager/Staff could otherwise hit the API directly.
6. Tests: handler tests (each of the 4 events produces the expected audit row), endpoint tests (403 for non-Owner, pagination/filtering correctness, export row-cap behavior).

### Frontend — Owner-only Audit Log UI
7. New route `/employees/audit-log`, gated with `useHasRole('Owner')` + fallback message, same pattern as `attendance/devices/page.tsx`. Nav entry added under the Employees section in `sidebar.tsx`, `roles: ['Owner']`.
8. Global paginated list (not a per-employee-only tab), filterable by employee / date range / event type, also linkable from an employee's detail page for a pre-filtered view.
9. Each row renders an **inline formatted summary** per event type (e.g. "Wage: 5,000,000 → 5,500,000", "Parent: Jane Doe → John Smith") — one small formatter per `EventType`, not a generic row + click-to-expand.
10. CSV export button: respects the currently active filters (same query as on-screen, just unpaginated up to the 10k cap), using the existing `downloadBlob` (`src/lib/csv.ts`) + blob-response pattern from `attendance/page.tsx`'s export flow.
11. New shared `EmployeePicker` component (wraps `Combobox` + `useEmployees`) for the employee filter. This is the third near-identical copy of the same hand-rolled search-and-map block (`add-manual-log-dialog.tsx`, `leave-dialogs.tsx`, and now this) — extracted now per rule-of-three, with the first two call sites refactored to use it too.
12. No new frontend test infra — this repo has none today; adding one just for this feature is out of scope.

## Open questions
None outstanding — resolved via grill session:
- Storage shape: JSON blob (was open in v1 of this doc).
- Table location: shared `EmployeeAuditLogs` table is an acceptable v1; revisit only if an external compliance export is requested later.
