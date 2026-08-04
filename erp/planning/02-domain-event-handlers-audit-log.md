# Branch: Domain Event Handlers → Audit Log

## Current state
Five stub handlers in `Erp.Infrastructure/DomainEventHandlers/` do nothing but `return Task.CompletedTask` (aside from `AttendanceLogRecordedHandler`, which does its one real job — the `AttendanceDay` recompute — and stubs the rest). None can send a real webhook, payroll notice, or dashboard push today because no such integration exists anywhere in the codebase yet (no outbound HTTP client config, no notification service, no analytics store, no real-time channel).

| Handler | TODO as written | Shippable now | Blocked on future integration |
|---|---|---|---|
| `EmployeeCreatedHandler` | Log creation, webhook, notify external systems | Structured log line | Webhook — no target system exists |
| `EmployeeBasicInfoChangedHandler` | Audit trail, compliance logging | **Yes** — event carries old/new FullName + Npwp | — |
| `EmployeeSalaryChangedHandler` | Payroll notification | Same audit log (old/new wage + effective date) | Actual payroll/accounting integration |
| `EmployeeParentChangedHandler` | Org chart update | Same audit log (who reported to whom, when) | Standalone org-chart projection — note `ParentId` *is* the live reporting structure already, correctly recalculated via `EmployeeHierarchyService`; there's nothing separate to "recalculate" today |
| `AttendanceLogRecordedHandler` | Real-time dashboard, notify supervisors, analytics | Nothing beyond existing recompute | All three — needs infra decisions (SignalR? polling? which analytics store?) |

## Plan
1. New `EmployeeAuditLog` table: `EmployeeId`, `EventType`, `OccurredAtUtc`, `OldValue`/`NewValue` (JSON or per-field columns), migration.
2. One handler (or three thin ones sharing the write path) subscribing to `EmployeeBasicInfoChanged`, `EmployeeSalaryChanged`, `EmployeeParentChanged` — writes a row each.
3. `EmployeeCreatedHandler`: replace stub with a real `Log.Information(...)` call. Leave webhook/external-notify explicitly out of scope (nothing to call).
4. `AttendanceLogRecordedHandler`: leave the dashboard/notify/analytics TODO as an explicitly separate future initiative — don't fold it into this pass.
5. Tests: handler-level coverage that each of the three change events produces the expected audit row.

## Frontend
None needed for this branch — audit log is backend-only unless/until there's a UI request to view it (not asked for).

## Open question
Is a shared `EmployeeAuditLog` table an acceptable v1, or should the audit trail live somewhere else (e.g. exported to an external compliance system later)? Not blocking — reasonable to default to the shared table and revisit if needed.
