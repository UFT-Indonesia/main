# GSS06 — Transactional Handlers App-Wide

Follow-up split out of the GSS05 PR review (grilling session, 2026-09-25). **Not started.**

**Blocked on:** the GSS05 PR merging with its four rollback tests green
(`AttendanceTransactionTests.cs`). Those tests are the pilot — they prove Wolverine finds the
DbContext behind `IRepository<T>` on three handlers before this flips the switch on all of them.

## The change in one line

Add `options.Policies.AutoApplyTransactions()` to `Program.cs`, so every Wolverine handler that
touches the DbContext runs in one EF Core transaction by default — opt-out instead of opt-in.

## The gap

`Program.cs:56` calls `UseEntityFrameworkCoreTransactions()`. That only **registers** the
middleware; it applies only to handlers marked `[Transactional]` or when
`AutoApplyTransactions()` is on. Before GSS05, neither existed anywhere, so no handler ran in a
transaction — while the code (and one comment) assumed they did.

Ardalis `RepositoryBase.AddAsync` / `UpdateAsync` / `DeleteAsync` each call `SaveChangesAsync`
immediately. Without a transaction, every repository call commits on its own, so a handler
with two writes can fail between them and leave half its work saved.

GSS05 fixed three handlers by hand with `[Transactional]`:

| Handler | What was split |
|---|---|
| `RecordManualLogHandler` | punch ↔ day recompute (a retry after a failure duplicated the punch) |
| `UpdateAttendanceLogHandler` | edited punch ↔ recompute of old/new day |
| `UpdateAttendancePolicyHandler` | history row ↔ policy save (audit trail could record a change that never happened) |

## Still exposed after GSS05

| Handler | Risk |
|---|---|
| `LeaveRequestApprovedHandler` → `LeaveAttendanceSync.MaterializeAsync` + `ReconcileEmployeeStatusAsync` | leave approved, but only some attendance days flipped to OnLeave |
| `LeaveRequestCancelledHandler` → `ReleaseAsync` + `ReconcileEmployeeStatusAsync` | leave cancelled, days still held OnLeave |
| `EditLeaveRequestHandler` (release + materialize + reconcile) | old days released, new days never materialized |
| `EmployeeTerminatedAttendanceHandler` → `DropLeaveDaysAfterAsync` | partial drop of post-termination leave days |
| `DecideProbationExtensionRequestHandlers` (2 writes) | request decided, probation end date not moved (or reverse) |
| `RecordDeviceLogHandler` | punch committed, then the `AttendanceLogRecorded` envelope saved separately — a crash in between loses the recompute (see below) |

The other ~12 write handlers do a single write; a transaction changes nothing for them.

### Device punch: lost recompute is never healed

`Program.cs:59` already admits it: *"the enqueue is still a separate transaction from the
aggregate save, so a crash in between can still lose a row."* For a device punch that means the
`AttendanceDay` row is never created, and since GSS05 derives Absent from missing rows, **the
employee shows Absent despite having tapped in.**

`RecomputeAttendanceDaysJob` would repair it (it rebuilds from the union of punches and existing
days) but only runs when the policy changes — `AttendancePolicyUpdatedHandler.cs:17` is its sole
trigger. No recurring schedule.

With `AutoApplyTransactions()`, `AddDbContextWithWolverineIntegration` (`DependencyInjection.cs:86`)
enrols the outbox envelope in the same transaction as the punch, closing this.

## Scope

1. `Program.cs`: add `options.Policies.AutoApplyTransactions();`. Fix the `:59` comment.
2. Remove the three now-redundant `[Transactional]` attributes from GSS05, keeping their comments'
   *why* (move it to a short note, or drop if the Program.cs comment covers it).
3. Audit every handler for ones that must **not** hold a transaction open (external calls:
   email, file storage, HTTP) and mark them `[NonTransactional]`.
4. Rollback tests, same pattern as `AttendanceTransactionTests` (swap one repository for a
   throwing one on a derived host):
   - leave approval: `IRepository<AttendanceDay>` throws → no days materialized, employee status unchanged
   - probation decision: second write throws → request still undecided
   - device punch: assert the envelope and the punch commit together (or at least that a failing
     recompute still leaves the punch — device path stays async by design)
5. Full integration run across Employees, Leave, Probation, Devices, Attendance.

## Not covered by this switch

Hangfire jobs are not Wolverine handlers, so `AutoApplyTransactions()` does not reach them:

- `RecomputeAttendanceDaysJob` — loops `RecomputeAsync` per key; a mid-run failure leaves some
  days on the new policy and some on the old. Rerunnable, so arguably fine.
- `SyncEmployeeLeaveStatusJob` — calls `ReconcileEmployeeStatusAsync` per employee.

Decide separately whether either needs an explicit `BeginTransactionAsync` (precedent:
`RefreshTokenService.cs:73`).

## Open questions

- Should `RecomputeAttendanceDaysJob` also run on a nightly schedule as a safety net for any
  lost recompute, independent of this change?
- Does any handler call out to an external service mid-way? (Answer during step 3.)
