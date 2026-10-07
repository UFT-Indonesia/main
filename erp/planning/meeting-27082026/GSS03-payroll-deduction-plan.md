# GSS03 — Payroll: Potongan Cuti (Leave Salary Cuts)

Created 2026-09-02 from a grill session on "cuti tahunan exceeded → allowance cut". **Rewritten
2026-10-06** after a second grill session, once GSS02 (holidays, #36) and GSS08 (overtime and Gaji
Premi, #37) had landed. Several 2026-09-02 decisions were reversed; the reversals are called out
inline. **This doc is the current version.**

**Status: built (backend, migration `AddLeaveSalaryDeduction`, web page `/payroll/potongan-cuti`).**
Not yet run against the dev database; apply the migration first. Where the build differs from, or
narrows, the text below:

- Frozen figures are one row per leave **day** (`LeaveDeductionLine`), not per request per month. The
  day list on the page reads straight from them.
- Lock (decision 3): editing is refused for any request touching a closed month; cancelling is refused
  only for **Approved** leave. A Pending request costs nothing yet, so withdrawing it stays allowed.
- A free request approved late into a closed month writes no frozen line. It never carries rupiah, but a
  later quota adjustment could relabel that one day in the cap count.
- Holiday (decision 14): declaring and removing are refused in a closed month; renaming an existing one is not.
- The per-employee exception (decision 11) has no audit-log entry yet.
- `leave.payroll_closed` is also returned when filing or approving leave whose cut days would land in a
  closed month; free days there are allowed (decision 8).

**Scope:** leave that costs salary, shown and frozen per month on an Owner-only payroll page. A real
salary run (gaji pokok, tunjangan, BPJS, PPh 21, THR, payslips) is still **out of scope**; the
monthly figure here is one line such a run would later consume.

## What changed since 2026-09-02

| Then | Now |
|---|---|
| No payroll module, "nothing downstream to land in" | Owner-only Payroll menu exists (`sidebar.tsx:48`: *"Gaji Premi is its first page; the salary run will join it"*), with a period → close → freeze pattern (`GajiPremiPeriod`, `OvertimeAssignment.Freeze`) |
| `LeaveRequest.Workdays()` hardcoded Mon–Fri | Holiday-aware via `AttendanceDayPolicy.IsWorkday`. A leave spanning Lebaran is no longer charged for it |
| Approved leave was fixed once approved | It can still change: `HolidayCalendarChangedHandler` → `RecountWorkdays` after a holiday is added/removed, and `LeaveRequest.Edit` accepts `Approved` requests. This is why the 2026-09-02 "snapshot at approval" decision was dropped |
| Open question: put the figure on the attendance CSV | Ruled out — the export is open to Staff (own rows) and Managers (their Staff) (`ExportAttendanceDaysEndpoint.cs:13`); a money column would leak wage, breaking decision 10 |
| — | Leave on a date holding an OT assignment is refused (`leave.overtime_on_date`). No impact here |

## Terms

| Term | Meaning |
|---|---|
| **Cap** | An employee's yearly days for one leave type: Annual 12 (prorated by probation), Sick 30, Izin 6, Unpaid 30, or the Owner's per-employee quota adjustment (`EmployeeLeaveQuota`) |
| **Cut day** | A leave day (or fraction of one) that costs salary |
| **Free day** | A leave day within the cap; costs nothing |
| **Daily rate** | Salary ÷ divisor, or the employee's exception (decision 11) |
| **Divisor** | Company-wide number the salary is divided by. Default **20** |
| **Month** | Calendar month. A cut belongs to the month the leave day falls in |
| **Close** | The Owner freezes a month's figures after paying salaries. Irreversible |
| **Potongan Cuti** | The new Owner-only payroll page listing cuts per employee per month |

## Worked example

Budi, salary Rp 5.000.000, divisor 20 → daily rate **Rp 250.000**. Annual cap 12 for 2026.

| # | When | What happens | Annual used | Left |
|---|---|---|---|---|
| 1 | Thu 15 Jan | Trip **Mon 2 – Fri 13 Nov** approved (10 workdays) | 10 | 2 |
| 2 | Sun 1 Mar | Leave **Mon 9 – Thu 12 Mar** approved (4 workdays). 9–10 Mar free, **11–12 Mar cut** (decision 6) | 14 | 0 |
| 3 | Wed 1 Apr | Potongan Cuti → March shows Budi, 2 days, Rp 500.000. Owner pays salaries and clicks **Close March** | 14 | 0 |
| 4 | Mon 1 Jun | Budi cancels the November trip (November is open, so allowed). 11–12 Mar stay **cut** (decision 15); the trip's 10 days return | 4 used, of which 2 free | 10 |

Before this feature, step 2 was refused with `leave.quota_exceeded`.

## Decisions

### Money

| # | Decision |
|---|---|
| 1 | **Daily rate = salary ÷ divisor**, divisor default 20. Fixed divisor, not workdays in the month — a leave day costs the same in February as in July, and holidays don't move it. (Kept from 2026-09-02.) |
| 4 | **What costs money:** Annual days beyond the cap, Sick beyond 30, Izin beyond 6, and **every Unpaid day** (Cuti di Luar Tanggungan, probation staff only). Half days and hourly Izin count as their fraction (`LeaveRequest.ChargePerWorkday`). *Reverses 2026-09-02 "Annual only".* |
| 5 | **Refusals:** Annual, Sick and Izin are no longer refused for running out — the extra days are allowed and cut; the approver is the check. **Unpaid stays hard-capped at 30** (`leave.quota_exceeded` survives for Unpaid only). Annual during probation stays refused (`leave.probation_annual`, eligibility, not quota). Owners have no caps and are never cut |
| 6 | **Which days are cut: the request that pushed the employee over pays.** Approved requests use the cap in approval order (`DecidedAtUtc`); within a request, its **last** days by date are the cut ones. Approving one request never changes another approved request's cost. **Editing an approved request counts as approving it again at the edit time** (`EditedAtUtc`), so an extension pays for itself |
| 9 | **Rounding:** exact amounts per day; each employee's month total is rounded **down to the nearest Rp 1.000**, once |
| 11 | **Per-employee exception**, one setting per employee covering every leave type, Owner-only, on the "Penyesuaian kuota cuti" card (`probation-quota-card.tsx`): **flat amount** per cut day (Rp 0 = exempt), or **custom divisor**, or neither (company divisor). One field, not per type. (Shape kept from 2026-09-02; widened to all types) |
| 12 | **Salary history.** New table, one row per salary change with its effective date, written by `EmployeeSalaryChangedHandler` (today a TODO). Seeded from every employee's current `MonthlyWage`/`EffectiveSalaryFrom`. Each cut day is priced with the salary in effect **on that date** — a raise entered on 25 Mar effective 1 Apr does not touch March. A same-date correction (allowed by `ChangeSalary`) replaces the row, so a typo fix flows into open months |
| 13 | **Divisor** is changed on the Potongan Cuti page, Owner-only. A change recalculates open months; closed months keep their frozen figures. *Not on Attendance Settings, which Managers can edit (`settings/page.tsx:42`)* |

Rounding example: salary Rp 4.555.555 → rate Rp 227.777,75. March: 1 full + 1 half cut day = 1.5 ×
227.777,75 = Rp 341.666,625 → **Rp 341.000**.

### The month: live, then frozen

| # | Decision |
|---|---|
| 1b | **Potongan Cuti** (`/payroll/potongan-cuti`, `roles: ['Owner']`): month picker, Open/Closed, one row per employee (cut days by type, total). A row opens the day list: date, leave type, request, free/cut, salary used, rate, amount |
| 2 | **Live while open, frozen at close.** An open month recalculates on every read from current approved leave, holidays, salary history, divisor and exceptions. **Close [month]** writes the figures and they never change. *Reverses 2026-09-02 decision 4 (snapshot onto the leave at approval), which went stale on every holiday, edit, cancellation and earlier-leave change* |
| 2b | Close only after the month ends, **oldest first**, irreversible — same as Gaji Premi. **Every month must be closed, even one with no cuts**, because the locks below depend on it. The first closable month is the launch month (before it, over-cap leave was refused, so nothing exists to cut) |
| 3 | **Lock.** Leave with any day in a closed month cannot be approved, edited or cancelled → `leave.payroll_closed`. Mirrors GSS08 decision 22 |
| 7 | **Close is blocked while any leave with days in that month is `Pending`.** The page lists them; the Owner decides them (or chases the Manager) first |
| 8 | **Late filing into a closed month** is allowed only if it costs nothing (e.g. a Sick day within 30, filed with the doctor's note after close). If any of its closed-month days would be cut → `leave.payroll_closed`. The one exception to the lock: approving a **free** request; editing and cancelling stay locked |
| 14 | **Holiday add/remove on a date in a closed month is refused** → `holiday.payroll_closed` (`SaveHolidayHandler`, `RemoveHolidayHandler`) |
| 15 | **Closed days keep their label forever.** Close stamps each leave day of that month free or cut. Later changes elsewhere (an earlier request cancelled, a quota adjustment raised) never relabel them. **Cut days do not use up the cap**; the balance is `cap − free days used`. Quota adjustments are not per month, so changing one affects open months only |

### Who sees what

| # | Decision |
|---|---|
| 10 | **Days for everyone, rupiah for the Owner only.** The leave form warns the employee: *"2 of these days are over your Annual quota and will be cut from your salary."* The Manager sees *"2 days over quota (salary cut)"* on the request. Only the Owner sees amounts. Reason (kept from 2026-09-02): rate × divisor = salary, and salary is Owner-only (`EmployeeVisibility.CanReadWage`, `EmployeeVisibility.cs:40`) — even from the employee themselves |
| 10b | Authority to change the divisor, the exception and to close is **Owner only** (copy the gate in `SetLeaveQuotaHandler.cs:25`) |

### Sequencing

The refusals in `LeaveQuotaGuard` are lifted **in the same release** as the Potongan Cuti page.
Shipping "you may exceed quota" alone would make over-cap leave free, with nothing recorded to
backfill from. (Kept from 2026-09-02.)

## Resolved questions (from the 2026-09-02 list)

| # | Question | Answer (2026-10-06) |
|---|---|---|
| 1 | Do other types become deductible? | Yes: Sick over 30, Izin over 6, every Unpaid day (decision 4). Unpaid stays capped at 30, so no day is cut twice |
| 2 | Where is the figure consumed? | Owner-only Potongan Cuti page under Payroll, monthly (decision 1b). Not the attendance CSV |
| 3 | Is the cut reversible? | While the month is open it follows the leave live. After close, leave in that month is locked (decision 3) and labels are permanent (decision 15) |
| 4 | Rounding | Month total per employee, down to Rp 1.000 (decision 9) |
| 5 | Which year's quota | Unchanged: days are charged to the year they fall in; each year's cap is consumed in approval order (decision 6) |
| 6 | Does the employee see it before filing? | Days over quota, never rupiah (decision 10) |
| 7 | Retroactive salary change | Salary history priced by date (decision 12); open months follow a correction, closed months don't |

## Interaction with GSS02 (holidays) and GSS08 (overtime)

- **Holidays:** leave counting already skips holidays, so the days charged — and therefore cut —
  exclude days the office was shut. A holiday change in an **open** month just recalculates live; in
  a **closed** month it is refused (decision 14). Cuti bersama is a holiday: it never touches quota
  and is never cut.
- **Overtime:** no money interplay. Gaji Premi is overtime pay on its own 2-month cycle; this is a
  salary cut on a monthly cycle. They share only the Payroll menu and the close-and-freeze pattern.
  A cut is never netted against Gaji Premi.

## Files

| File | Change |
|---|---|
| `Erp.Core/Aggregates/Payroll/*` | new: `LeaveDeductionMonth` (closed month, like `GajiPremiPeriod`), frozen per-request-per-month lines (free days, cut days, salary, rate, exact amount), `PayrollSettings` singleton (divisor, first month), `LeaveDeductionCalculator` (pure: cap consumption in approval order, cut days, rate, rounding) |
| `Erp.Core/Aggregates/Employees/*` | `EmployeeSalaryHistory` rows; `LeaveDeductionException` owned value (mode + amount/divisor) on `Employee` |
| `Erp.Infrastructure/DomainEventHandlers/EmployeeSalaryChangedHandler.cs` | write salary history (replaces the TODO) |
| `Erp.UseCases/Leave/Common/LeaveQuotaGuard.cs` | return over-cap days instead of refusing (Unpaid still refused); add the closed-month check |
| `Erp.UseCases/Leave/Common/LeaveQuota.cs` | balance = cap − free days; closed months read frozen labels |
| `Erp.UseCases/Leave/{CreateLeaveRequest,DecideLeaveRequest,EditLeaveRequest}/*` | `leave.payroll_closed`; over-quota days on the response |
| `Erp.UseCases/Leave/GetLeaveBalance/*` | follow the new balance rule |
| `Erp.UseCases/Attendance/Holidays/{SaveHoliday,RemoveHoliday}/*` | `holiday.payroll_closed` |
| `Erp.UseCases/Payroll/*` | new: list month, day list, close month, set divisor, set exception |
| `Erp.Infrastructure/Persistence/*` | configurations + migration (seed salary history and settings) |
| `Erp.Web/Endpoints/Payroll/*` | Potongan Cuti endpoints in the existing `PayrollGroup` |
| `apps/web/src/app/payroll/potongan-cuti/*` | new page |
| `apps/web/src/components/layout/sidebar.tsx` | one item, `roles: ['Owner']` |
| `apps/web/src/components/leave/leave-dialogs.tsx` | over-quota days warning on the form and on the approval view |
| `apps/web/src/components/employees/probation-quota-card.tsx` | exception setting |
| `apps/web/messages/{en,id}.json` | keys |

`LeaveDeductionCalculator` is the money path. It gets one unit test per row of the worked example,
plus: cross-month request split, New Year split, half day and hourly Izin fractions, edit
re-queueing (decision 6), salary change mid-month (decision 12), exception modes including Rp 0,
rounding down to Rp 1.000, and a cancellation after close leaving labels intact (decision 15).
