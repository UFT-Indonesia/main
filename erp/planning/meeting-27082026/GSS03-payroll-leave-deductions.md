# GSS03 — Payroll: Over-Quota Leave Deductions

Scoping note for a **future** feature. Created 2026-09-02 from a grill session on "cuti tahunan exceeded → allowance cut".

**Not started. No code written.** Decisions below were made in that session; open questions were not.

**Scope decision that created this doc:** GSS03 covers **leave deductions only** — over-quota Annual leave producing a salary cut. A real salary run (gaji pokok, tunjangan, BPJS, PPh 21, THR, payslips) is explicitly **out of scope** and needs its own doc. The leave deduction is designed to be one line item such a run would later consume.

## The gap

**There is no payroll module.** A grep for `payroll` / `allowance` / `deduction` across `.cs`/`.ts`/`.tsx` returns four hits, all comments:

| Location | What it says |
|---|---|
| `Erp.Infrastructure/DomainEventHandlers/EmployeeSalaryChangedHandler.cs:11` | `// TODO: payroll/accounting integration — no target system exists yet.` |
| `Erp.UseCases/Attendance/Common/LeaveAttendanceSync.cs:102` | "would otherwise reach the table and the payroll export" |
| `Erp.UseCases/Attendance/Common/AttendanceLogService.cs:43` | "corrections to existing ones stay open so payroll…" |
| `Erp.Core/Aggregates/Employees/Events/EmployeeCreated.cs:9` | "downstream consumers (HR sync, payroll, webhooks)" |

The only export that exists is attendance, and it carries no money:

```
Employee,Date,TapIn,TapOut,Status,LeaveType
```
`Erp.Web/Endpoints/Attendance/ExportAttendanceDaysEndpoint.cs:68`

So a deduction computed today has **nothing downstream to land in**. That is the reason this is a doc and not a feature.

## What already exists to build on

| Thing | Where | Note |
|---|---|---|
| `Employee.MonthlyWage` (`Money`: `decimal Amount` + `string Currency`) | `Erp.Core/Aggregates/Employees/Employee.cs` | the "Gaji" the formula divides |
| Wage is **Owner-redacted** | `Erp.Web/Endpoints/Employees/EmployeeResponseMapper.cs:25` (`showWage ? … : null`), `apps/web/src/lib/api/types.ts:33` | *"Null for non-Owner callers — pay is redacted server-side"* |
| Salary changes emit an event | `EmployeeSalaryChanged` → `EmployeeSalaryChangedHandler` | wages move over time — see "Snapshot, not live" below |
| Per-employee override pattern | `EmployeeLeaveQuota`, owned collection at `EmployeeConfiguration.cs:105` | the exception override should copy this shape |
| Override UI surface | `probation-quota-card.tsx:180-196` ("Penyesuaian kuota cuti") | Owner-only card; natural home for the deduction override |
| Owner-only write gate | `SetLeaveQuotaHandler.cs:25` (`command.Caller.Role != EmployeeRole.Owner`) | pattern to copy verbatim |
| The block to be lifted | `LeaveQuotaGuard.cs:74` → `leave.quota_exceeded` | today an over-quota request is refused outright |
| Annual entitlement maths | `LeaveQuota.cs:53` `AnnualEntitlement`, `:67` `Entitled` | decimal since 2026-09-01 (half-day/hourly work) |

## Decisions made

### 1. Formula

```
daily cut = Gaji ÷ divisor          divisor configurable, default 20
total cut = daily cut × over-quota days charged
```

**Fixed divisor, not actual workdays in the month.** A leave day then costs the same in February as in July, and a leave spanning a month boundary needs one rate rather than two. The divisor is a setting an Owner can change, not a constant — same treatment as `MaxIzinHours`.

Rejected: `Gaji ÷ (Mon–Fri days in that month)`. It prices identical leave differently by timing, and it would collide with GSS02 — once public holidays exist, the divisor itself starts moving.

### 2. Per-employee exception override — two modes

The Owner picks one, per employee:

| Mode | Stored | Meaning |
|---|---|---|
| **Flat amount** | Rp X | this employee is cut X per over-quota day, regardless of salary. `0` = exempt |
| **Custom divisor** | N | this employee is priced `Gaji ÷ N` instead of the company default |
| *(neither set)* | — | company default divisor applies |

Flat `0` doubling as "exempt" is deliberate — no separate exemption flag to keep in sync.

### 3. Authority: Owner only

Not Manager, despite "Owner/Manager" in the original ask.

**Reason:** `MonthlyWage` is already redacted from non-Owners. A deduction of "Rp 250.000/day" *is* `Gaji ÷ 20` — anyone who can see or set it multiplies by the divisor and recovers the salary the system deliberately hides. Letting a Manager read the figure would silently reverse an existing decision rather than fill a gap.

Consequence to accept: a Manager approving an over-quota leave will not see what it costs.

### 4. Snapshot at approval, never computed live

The cut must be **written onto the leave request when it is approved**, not derived on read.

`EmployeeSalaryChanged` exists and fires — a live calculation means a leave taken in March silently reprices itself after a June raise, and any payroll figure already paid out stops matching what the screen says. Snapshot the amount, the divisor (or override) used, and the wage it came from.

## What must change in the leave module (not yet done)

1. **Lift the block.** `LeaveQuotaGuard` currently returns `leave.quota_exceeded` for Annual over quota. That becomes: allowed, but the request is marked over-quota with the days that exceeded.
2. **Only Annual.** Sakit (30), Izin (6) and Unpaid (30) stay hard-capped — see open question 1.
3. **Partial days count.** Since 2026-09-01 a request can charge 0.5 or a fraction of a day (`LeaveRequest.ChargePerWorkday`). The over-quota portion is therefore decimal, and so is the cut. `Money` rounding rules need deciding — see open question 4.

**Deliberately sequenced:** the block is *not* lifted before the deduction exists. Shipping "you may now exceed quota" alone creates a window where over-quota leave is simply free, and leave filed in it has no recorded deduction to backfill from.

## Open questions (none answered)

1. **Do other types ever become deductible?** Sakit over 30 days is a real scenario (long illness) and arguably shouldn't cost salary at all. Unpaid is already unpaid — does an over-quota Unpaid day deduct twice?
2. **Where is the figure consumed?** A column on the attendance CSV, a new per-employee monthly summary export, or held until a real payroll run exists. Nothing consumes it today.
3. **Is the deduction reversible?** Cancelling an approved over-quota leave frees the quota (balances are derived, never stored). The snapshotted cut is *not* derived — cancellation must explicitly void it, or it outlives the leave that caused it.
4. **Rounding.** `Gaji ÷ 20` on an odd salary gives fractions of a rupiah, and half-day leave halves it again. Round per day or per request, and in whose favour?
5. **Which year's quota.** `LeaveQuota` charges days to the year they fall in, so a request over New Year can be within quota for one year and over for the other. The cut then applies to part of one request.
6. **Does the employee see it before filing?** The create dialog already shows remaining quota. Showing "this will cost you Rp X" pre-submit is honest but exposes the employee's own daily rate — which is their own salary, so probably fine, unlike showing it to a Manager.
7. **Retroactive salary change.** If a wage is corrected *backwards* (a typo fixed after leave was approved), does the snapshotted cut get recomputed or stand as-is?

## Interaction with GSS02

GSS02 (holidays / working week) does **not** block this, because the fixed divisor was chosen over actual-workdays. Had the divisor been "workdays in the month", declaring a public holiday would have changed every deduction in that month retroactively.

Still coupled in one place: `LeaveRequest.Workdays()` is hardcoded Mon–Fri, so the *number of days charged* — and therefore the number over quota — already ignores holidays. A leave spanning Lebaran is charged, and would be deducted, for days the office was shut.
