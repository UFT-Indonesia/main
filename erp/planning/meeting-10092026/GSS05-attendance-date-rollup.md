# GSS05 — Attendance Date Roll-Up

Design decided in a grilling session on 2026-09-10, seeded by two screenshots of Propan's
HC Online "Attendance History" page. **Implemented the same day.**

## The change in one line

`/attendance` flips grain: a row stops being one employee-day and becomes **one calendar
date for the whole company**, expandable to the employees underneath it.

| Level | Today | After |
|---|---|---|
| L1 | employee-day rows, paginated | **date rows over a period** |
| L2 | punch details dialog | **employee rows, inline expansion** |
| L3 | — | punch details dialog (exists, unchanged) |

Only L2 is new, and it is not a page. No new route is added.

## What the reference actually contributes

The Propan screenshots are a **per-employee monthly timesheet**. We are not copying that
layout — we chose a company-wide roll-up instead — but four of its ideas carry over:

- every date in the period gets a row, including days nobody punched
- an employee who did not turn up is visible, rather than silently absent from the data
- schedule and actual sit side by side (we do In/Out only; no per-weekday schedule exists here)
- the period is an explicit from/to, not a filter row

Deliberately **not** taken: the seven-letter status codes (PRS/OFF/PHL/ICF/BTI/FMA/IGF),
overtime hour and index columns, barcode export, print preview. Overtime belongs with
`GSS03-payroll-deduction-plan.md`, not here.

## Decisions

| # | Decision | Note |
|---|---|---|
| 1 | Rows are dates, whole company, one page | replaces the flat list |
| 2 | Date row expands inline to employee rows | no route, no modal, several dates can be open at once |
| 3 | Absent is derived, never stored | see below |
| 4 | Every date in the period appears; weekends read `Off` | reuses the hardcoded Mon–Fri rule |
| 5 | Staff see the same table; the summary cell shows their own status | their scope is always one person |
| 6 | Filter builder keeps employee fields only, plus a problems-only toggle | see below |
| 7 | Summary shows exceptions only, or `All present` | no ratios, nothing to subtract |
| 8 | One request per period, no pagination | expansion is client-side |
| 9 | Export the period as shown; checkbox selection deleted | contract moves to a date range |
| 10 | Future dates render blank; today reads as in progress | see below |
| 11 | An Absent row opens the details dialog empty, with Add punch | reuses both existing dialogs |
| 12 | Employee row shows Employee, In, Out, Status | separates the two kinds of `Incomplete` |
| 13 | Employees sort problems first, then alphabetical within each group | |
| 14 | Default period is month-to-date, dates newest first | today is the first row, no blank future rows |

## Absent (decision 3)

`AttendanceDay.Create` throws on an empty punch list and `CreateForLeave` requires a leave
id, so **no punch and no leave means no row is ever written**. An employee who did not come
in is currently invisible to the system.

The count comes from subtraction, not lookup:

```
expected = employees where HireDate <= d
                       and (TerminationDate is null or TerminationDate > d)
absent   = expected − employees with a row on d
```

`HireDate`/`TerminationDate` rather than `EmployeeStatus`, because status holds only today's
value. Someone terminated last week reads `Terminated` now but was genuinely expected at work
in March; using status would shrink every historical denominator.

`Absent` is therefore **a read-model value only**. `AttendanceDayStatus` in the domain stays
`Complete | Incomplete | OnLeave`. No migration, no nightly job, no rows written. `Off` and
`Upcoming` are likewise presentation-only.

## Today and future dates (decision 10)

Status on today is provisional: it changes on its own as the clock moves, with nobody doing
anything. A normal employee reads `Absent` at 07:00, `Incomplete` from 07:17 to 16:40, and
`Complete` only after clocking out. Reporting today in the same words as yesterday would show
almost the whole company as `Incomplete` every working day until evening.

- **Before** `ShiftEnd + ClockOutGraceMinutes`, today counts arrivals: *45 clocked in · 2 not in yet*, marked in progress.
- **After** that instant, the row flips to the normal exception summary and never changes again.
- Dates after today get a row but an empty summary. Expanding one shows the roster with no status.

Both thresholds already exist on `AttendancePolicy`. Nothing new is configured.

## Filters (decision 6)

`ATTENDANCE_DAY_FILTER_FIELDS` loses `date` (the period picker owns it) and `status`, `tapIn`,
`tapOut` (they describe an employee-day, which is no longer what a row is; filtering by them
would make counts describe a subset while still reading like totals). `employeeName` and
`employeeId` stay and narrow the counted population. On the server the map is rebuilt over
`Employee` as `AttendanceCalendarFilterFields`, replacing `AttendanceDayFilterFields`.

A single **Only dates with problems** toggle recovers the useful part of the status filter. A
problem is `Absent` or `Incomplete`. `OnLeave` is not a problem, `Off` days are excluded, and
today is excluded while in progress.

The flat list is deleted rather than kept as a second view. Its main query — *"every Incomplete
day Budi had this quarter"* — is reproduced by the employee filter plus the toggle plus a
quarter-wide period.

## Shape

```
Period: [01-09] – [10-09]    [☑ Only dates with problems]
Filters: Employee is "Budi Santoso"                        [ Export period ↓ ]

Thu, 10-09   45 clocked in · 2 not in yet      In progress
Wed, 09-09   All present
▾ Tue, 08-09   1 absent · 2 incomplete
     Employee          In      Out     Status
     Rina Wijaya        —       —      Absent        → dialog: no punches, [+ Add punch]
     Amelia Dewi      08:22   18:14    Incomplete    → late in
     Budi Santoso     07:30     —      Incomplete    → never clocked out
     Sari Lestari       —       —      On Leave
     Ahmad Fauzi      07:20   16:35    Complete
     Zidan Pratama    07:17   16:41    Complete
Mon, 07-09   All present · 2 on leave
Sat, 05-09   Off
Fri, 04-09   1 incomplete
```

Staff, scoped to themselves, get the same table with the summary cell showing their own day:

```
Thu, 10-09   07:56 – …        In progress
Wed, 09-09   07:17 – 16:41    Complete
Sat, 05-09   Off
```

## API

`ListAttendanceDays` is **reshaped in place**, not added alongside — the page is its only
consumer. Pagination is deleted.

```
GET /api/attendance/days?from=2026-09-01&to=2026-09-10&…employee filters

[ { date, isWorkday, isFuture, isInProgress,
    employees: [ { employeeId, fullName, status, tapInUtc, tapOutUtc,
                   leaveType, canWrite } ] } ]
```

Everything for the period arrives in one response and expansion is pure render. For a 50-person
company over a month that is roughly 1,500 small entries. Cap the period at about a quarter and
leave a `ponytail:` comment naming the ceiling, so it can be split into summary-plus-expansion
if headcount grows.

`ApplyCallerScope` is unchanged: Owner and Manager get the company, Staff get themselves. Per-row
`canWrite` still comes from `AttendanceRules.CanWriteFor`, so the Add punch button appears only
where the caller may write.

Export moves from a key list to a date range, which retires the 500-key cap. Absent employees
appear in the file as absent rows, matching what is on screen.

## Files

| File | Change |
|---|---|
| `apps/web/src/app/attendance/page.tsx` | rewrite: period picker, toggle, no pagination, no selection |
| `apps/web/src/components/attendance/attendance-calendar-table.tsx` | new date table with inline expansion; replaces `attendance-day-table.tsx` |
| `apps/web/src/components/attendance/view-log-details-dialog.tsx` | empty state plus Add punch |
| `apps/web/src/components/attendance/add-manual-log-dialog.tsx` | accept a prefilled employee and date |
| `apps/web/src/lib/filters/fields.ts` | trim `ATTENDANCE_DAY_FILTER_FIELDS` to the two employee fields |
| `apps/web/src/lib/api/attendance.ts`, `hooks/use-attendance.ts` | reshape the day query, drop paging params |
| `apps/api/.../ListAttendanceDays/*` | reshape: date-grouped, headcount join, no paging |
| `apps/api/.../ExportAttendanceDays/*` | from/to contract, absent rows |
| `apps/api/.../Endpoints/Attendance/ListAttendanceDaysEndpoint.cs` | new query string |
| `apps/web/messages/{en,id}.json` | new status and summary keys |

## Known gaps, accepted

**Public holidays are still not modelled.** Thursday 17 September, Independence Day, will read
as an ordinary workday and show the whole company absent. This is the exact hole documented in
`planning/meeting-27082026/GSS02-attendance-holiday-workaround.md`, and decision 4 makes it more
visible than it is today rather than fixing it. Weekends are covered only because the Mon–Fri
rule is already hardcoded in `LeaveRequest.Workdays()` and `countWorkdays()` in
`leave-dialogs.tsx`; those two must still change together.

**Lateness in minutes does not exist.** `Incomplete` covers both arriving late and never
clocking out. Decision 12 separates them visually via the In and Out columns, but no number is
computed. The reference has a Late In (Minute) column; adding one is cheap (tap-in against
`ShiftStart + ClockInGraceMinutes`, stored nowhere) and was deliberately deferred.

**No per-weekday schedule.** The reference shows Friday ending at 17:00 and other days at 16:30.
`AttendancePolicy` holds one `ShiftStart`/`ShiftEnd` pair for every day.

**No attendance-request workflow.** The reference has per-day correction requests with start,
end and remark, approved and shown inline. We have log notes on punches, which is a weaker thing.

## Resolved during implementation

1. **Period cap.** 92 days, enforced server-side in `AttendancePeriod.TryValidate` and shared by
   the list and the export. No UI cap; the picker allows any range and the server refuses.
2. **Problems-only hides `Off` rows.** `hasProblem` requires a settled workday, so weekends,
   future dates and today-in-progress all drop out of the filtered list.
3. **New status vocabulary.** `ClockedIn` and `NotInYet` were added for today-in-progress, on top
   of the `Absent`/`Upcoming` the design named. All four are read-model only.

## Still open

Narrow-screen layout for the four-column expansion is unresolved. The tables scroll but were
not designed for it.

## Verification

API unit tests pass (602, including 8 new `AttendanceCalendarTests` covering absence derivation,
weekends, hire dates, the in-progress boundary and sort order). The web app typechecks, lints and
builds. Integration tests compile but were not run — Docker was not available, so Testcontainers
could not start Postgres. The two export tests there were rewritten for the period contract and
have not executed.
