# GSS08 — Overtime (Lembur) and Gaji Premi

Design decided in a grilling session on 2026-09-28/29, seeded by the owner's message:

> Sistemnya bikin kaya pengajuan aja berarti biar gampang. Nanti harus approve owner. […] harus
> input hari apa and dari jam berapa sampe jam berapa lemburnya. Ada rule di sistemnya kalau 2 jam
> dapet 25rb, 2-6 jam 50rb, diatas 6 jam 70rb per hari. Jangan dibedain lembur weekend sama
> weekday. […] nanti ke record berapa jam lembur dalam sebulan yg ngaruh ke bayaran tambahan buat
> mereka di akhir bulan.˝

The rules were later revised by the owner (tiers, who initiates, payout cycle). **This doc is the
revised version; the quote above is history.**

**Implemented** (backend, migration `AddOvertimeAndGajiPremi`, web pages `/overtime` and `/payroll/gaji-premi`).
Choices made while building, where this doc was silent: a period closes only after it ends and only
in order (Q1); a correction time must fall inside the assigned window; rapel work date must be in a
closed period, an Owner adding one must set the amount (Q7); leave date pickers do not yet grey out
OT dates (the server refuses with `leave.overtime_on_date`); the attendance calendar's
`WorkedOnDayOff` badge is unchanged (Q8).

Follow-up (done): Q8 resolved — the attendance calendar now shows OT (`Overtime` status on a day off
with only OT punches, plus an OT badge with window and counted hours on any day; `WorkedOnDayOff`
is now unplanned work only). Every date picker marks holidays; pickers with an employee also mark
leave (primary dot) and OT (success dot), lighter when Pending, via `GET /api/calendar/markers`
(kinds only, no reasons). Leave pickers refuse OT dates; the OT picker refuses dates already holding OT.

**Decided and built 2026-10-06** (answers to open questions 2–7, see *Resolved questions*; migration `LinkRapelToOvertimeAssignment`):
`ShiftEnd` capped at 18:00; a tap-out correction is allowed when the last owned punch left early
(before the assigned end, beyond grace); rapel requests close one month after the payout date; a rapel is tied to an
approved OT assignment and the amount is suggested from the employee's claimed times (difference
from what was already paid), with the Owner able to override.

## Scope

| In | Out |
|---|---|
| OT assignment (Owner/Manager → employee), OT punches, pay tiers | Primary salary run (gaji pokok, BPJS, PPh 21, THR, payslips) — still unplanned, see GSS03 |
| Correction requests for a missing OT tap-in/tap-out | Notifications |
| Gaji Premi: 2-month periods, close, rapel, per-employee history | Reopening a closed period |
| Payroll menu (Owner-only), Gaji Premi as its first page | GSS03 leave deduction (will later live under the same menu) |

**Separate module from Holiday.** A holiday is a company-wide calendar fact with no approval and no
money; overtime is per-employee, approved, and paid. The holiday calendar is read only to tell a
weekday from a day off (`AttendanceDayPolicy.IsWorkday`). Lives in `Erp.Core/Aggregates/Overtime/`.

## Prerequisite: the holiday engine (GSS02)

Merged to `main` in #36. GSS08 work happens on `refactor/overtime-and-gaji-premi`, branched off
the updated `main`.

Checked against GSS08 (2026-10-01):

| Check | Result |
|---|---|
| `AttendanceDayPolicy.IsWorkday` is holiday-aware (`AttendanceDayPolicy.cs:29-31`: Mon–Fri and not in `Holidays`) | ✅ Exactly the weekday vs day-off test GSS08 needs |
| Request-scoped `AttendanceDayPolicy` carries holidays (`Erp.Infrastructure/DependencyInjection.cs:59-72`) | ✅ Any handler that injects the policy sees the calendar |
| Holiday kind (National / Collective) | ✅ Display only; both are "day off" for OT. No GSS08 impact |
| **`RecomputeAttendanceDaysJob` builds its policy with `policyEntity.ToAttendanceDayPolicy()` — no holidays** (`RecomputeAttendanceDaysJob.cs:35`) | ⚠️ Harmless today (`AttendanceDay` status never calls `IsWorkday`). **Not harmless once punch ownership depends on weekday vs day off**: the 02:00 job would treat a holiday as a weekday and split its punches wrongly. Either load holidays in the job, or (preferred) read the snapshot below |
| `HolidayCalendarChangedHandler` only re-anchors leave | ⚠️ A holiday declared or removed **after** an OT assignment would flip its weekday/day-off class and with it punch ownership, contradicting decision 12 |

**Resolution (follows from decision 12): each OT assignment stores `IsDayOff`, snapshotted at
creation.** Window validation, punch ownership and the lunch deduction read the snapshot, never the
live calendar. The nightly job then needs no holidays, and `HolidayCalendarChangedHandler` needs no
OT branch.

## Terms

| Term | Meaning |
|---|---|
| **OT assignment** | Owner/Manager tells one employee to work overtime on one date, from–to. Replaces the owner's original "pengajuan" — employees do not file overtime themselves |
| **OT tap-in / OT tap-out** | The punches that belong to an OT assignment (see *Punch ownership*) |
| **Counted hours** | Assigned window ∩ OT punches, after grace, minus breaks, rounded down to whole hours |
| **Correction request** | Employee asks for a missing OT tap-in or OT tap-out to be written |
| **Gaji Premi** | The overtime pay pocket, separate from salary |
| **Period** | Two calendar months starting in an odd month (Jan–Feb, Mar–Apr, … Nov–Dec) |
| **Rapel** | Late claim for a closed period, decided by the Owner, paid as a line on the next payout |

## Decisions

### Pay

| # | Decision |
|---|---|
| 1 | Tiers per day: **0–1h Rp0 · 2–3h 25rb · 4–5h 50rb · 6h+ 70rb** |
| 2 | Always round down to completed hours. 3h 59m = 3h. No rounding-up grace |
| 3 | Weekday and day off pay the same tiers |
| 4 | Tiers and amounts live in **`appsettings.json` / env only** — no UI, engineer-only, redeploy to change. Frozen into each OT assignment at period close |
| 5 | Pay is per-day tier, not hours × rate. Hours are shown for information |

```json
"Overtime": {
  "Tiers": [
    { "MinHours": 2, "Amount": 25000 },
    { "MinHours": 4, "Amount": 50000 },
    { "MinHours": 6, "Amount": 70000 }
  ]
}
```

### The window

| # | Decision |
|---|---|
| 6 | An OT assignment = **date + from–to**, picked by the assigner |
| 7 | **Weekday:** start fixed at **18:30** (18:00–18:30 is a break after the shift). Only the end is picked |
| 8 | **Day off** (weekend or holiday): start is free, 12:00–13:00 lunch (1h) is deducted **only when the counted span, after grace, contains all of 12:00–13:00**; partial overlap deducts nothing (reuse `LeaveRequest.LunchStart`/`LunchEnd`) |
| 9 | **05:00 is the day boundary.** Every window sits inside 05:00 day D → 05:00 day D+1: day-off start ≥ 05:00, end ≤ 05:00 next day |
| 10 | Past midnight counts on the **start date** (18:30 Tue → 01:00 Wed is Tuesday's OT, one tier) |
| 11 | One OT assignment per employee per date |
| 12 | Weekday/day-off is checked only at creation and **stored on the OT assignment (`IsDayOff`)**. A holiday declared or removed later leaves the window, the punch ownership and the lunch deduction as entered (see *Prerequisite*) |

18:30 and 05:00 are constants next to `LunchStart`/`LunchEnd`. **`AttendancePolicy.ShiftEnd` is
capped at 18:00** (earlier is allowed): the policy update is refused with a later value, so the 18:30
start can never overlap regular hours.

### Counted hours

**Counted hours = assigned window ∩ OT punches**, then grace, then breaks, then round down.

| # | Decision |
|---|---|
| 13 | Late OT tap-in within `ClockInGraceMinutes` → counted from the assigned start. Beyond grace → marked late, counted from the actual punch |
| 14 | Early OT tap-out within `ClockOutGraceMinutes` → counted to the assigned end. Beyond grace → marked **Left early**, counted to the actual punch, **still paid for hours worked** |
| 15 | Grace snaps to the assigned edge; it never adds an hour |
| 16 | Early OT tap-in (18:20) counts from 18:30. Staying past the assigned end pays nothing extra |
| 17 | Weekday OT needs its **own** OT tap-in — working straight through with no 18:00 Out / 18:30 In → `Incomplete` (strict) |
| 18 | Missing OT tap-in or OT tap-out → `Incomplete`, **pays Rp0** until a correction request is approved. The system never invents a punch; the UI shows "assigned until 01:00" as a hint only |

Examples (grace 5m):

| Assigned | Punches | Counted | Pay |
|---|---|---|---|
| 18:30–21:30 | In 18:32, Out 21:35 | 3h | 25rb |
| 18:30–21:30 | In 18:36, Out 21:30 | 2h 54m → 2h | 25rb (late) |
| 18:30–01:00 | In 18:31, Out 00:57 | 6.5h → 6h | 70rb |
| 18:30–01:00 | In 18:31, Out 22:10 | 3h | 25rb (left early) |
| 18:30–01:00 | In 18:31, Out 20:15 | 1h | Rp0 (left early, under 2h) |
| 18:30–01:00 | In 18:31, Out 01:40 | capped 6.5h → 6h | 70rb |
| 18:30–21:30 | In 18:36, Out 20:35 | 1h 59m → 1h (no partial grace) | Rp0 (late, under 2h) |
| Sat 09:00–16:00 | In 09:10, Out 16:00 | 6h 50m − 1h lunch = 5h 50m → 5h | 50rb (late) |
| Sat 09:00–16:00, **grace 10m** | In 09:10, Out 16:00 | snaps to 09:00: 7h − 1h lunch = 6h | 70rb |
| Sun 12:30–14:45 | In 12:30, Out 14:45 | 2h 15m, lunch not fully inside → no deduction → 2h | 25rb |
| Sun 12:00–16:00 | In 12:04, Out 16:00 | snaps to 12:00, full lunch → 4h − 1h = 3h | 25rb |

### Punch ownership

`AttendanceDay` takes the day's first punch as tap-in and the last as tap-out
(`AttendanceDay.cs:149-152`). With OT, punches have to be split, or (a) an approved-leave day with
evening OT reads as a late `Incomplete`, and (b) a 00:58 OT tap-out becomes the next day's tap-in and
hides real lateness.

> **Weekday:** the OT tap-in is the **first `In` punch after `ShiftEnd`**. From it until **05:00 next
> day**, every punch — any type, any count — belongs to the OT assignment. OT tap-out = the last of
> them. Everything before belongs to the regular day.
>
> **Day off:** every punch from 05:00 to 05:00 next day belongs to the OT assignment.
>
> Applies to `Pending` **and** `Approved` OT assignments. A `Pending` one that expires at close keeps
> its punches and pays Rp0. Rejecting or cancelling one recomputes D and D+1, and the punches go back
> to the regular day.

| Case | Regular day | OT assignment |
|---|---|---|
| 07:55 In, 18:00 Out, 18:31 In, 21:35 Out | 07:55 → 18:00 Complete | 18:31 → 21:35 |
| Regular Out at 18:02 | stays regular (Out, not In) | from next In |
| Approved leave, only 18:31 In / 21:35 Out | no punches → stays `OnLeave` | 18:31 → 21:35 |
| Curious taps 00:58/00:59/01:00/01:01 | untouched | all claimed, tap-out 01:01, pay capped |
| Forgot OT tap-out, arrives 06:40 | 06:40 is the new day's tap-in | `Incomplete` |

`PunchType` is used only to find the start; devices record In/Out reliably. The end is type-agnostic
because curious repeated taps produce valid-looking In/Outs.

**Accepted edge:** OT the night before *and* arriving before 05:00 → that punch is absorbed into the
OT, the new day shows no tap-in → correction.

**Where it goes:** `AttendanceDayRecomputeService.RecomputeAsync` (`:29`) filters OT-owned punches
out after loading. Both paths go through it: the per-punch `AttendanceLogRecorded` handler and the
nightly `RecomputeAttendanceDaysJob` (02:00 Asia/Jakarta, `Program.cs:141-145`). Creating, extending
or cancelling an OT assignment must trigger a recompute of its date **and the next date**.

### Who

| # | Decision |
|---|---|
| 19 | Owner → OT assignment for anyone except Owners. Manager → only their direct Staff (`OrgScope.IsDirectStaffOf`; hierarchy is 2 levels, so direct = everyone under them) |
| 20 | Managers can receive OT assignments (from an Owner) |
| 21 | Owner-created → `Approved` immediately. Manager-created → `Pending` until an Owner approves (reuse `LeaveRules.IsAutoApproved`/`IsRequester`) |
| 22 | Edit end / cancel: until period close. Owner any; Manager own Staff; a Manager's edit returns it to `Pending` |
| 23 | Date is never edited — cancel + new OT assignment |
| 24 | **Once an OT assignment has punches:** cannot be cancelled; end can be **extended** but not shortened |

### Leave interplay

| Already on the date | Adding | Allowed |
|---|---|---|
| OT assignment (`Pending` or `Approved`) | Leave request | ❌ |
| Approved leave | OT assignment | ✅ |

OT wins, for Staff and Managers alike. The leave stays approved and charged as normal.

### Correction requests

| # | Decision |
|---|---|
| 25 | For a missing OT tap-in or OT tap-out only. Fields: OT assignment, which punch, time, reason (reuse `LeaveRequest.ReasonMinLength`/`MaxLength`), **required** proof upload |
| 26 | Upload reuses the leave attachment rules: 10 MB, PDF/JPEG/PNG. Visible to the employee, the approver, and Owners |
| 27 | Approved by `LeaveRules.CanDecideFor` — own Manager or any Owner for Staff; Owner only for a Manager; never the requester. Safe for Managers because a correction can only move a punch inside a window the Owner already approved |
| 28 | Approval writes an `AttendanceLog.Manual(...)` punch (`AttendanceLog.cs:93`); the existing recompute does the rest. `RecordedByUserId` is the audit trail |
| 29 | Counts toward the **work date**, never the approval date. Oct 30 corrected on Nov 5 is October OT |
| 29a | A tap-out correction is also allowed when the OT assignment already owns a tap-out that **left early** — before the assigned end, beyond `ClockOutGraceMinutes` (within grace it already counts to the end) (e.g. tapped out at 20:15 by mistake, worked until 21:30). The new time must be later than the last owned punch and inside the window. It adds a manual Out; the stray punch stays in the log. The system cannot tell this from a wrong device clock — the approver judges from the reason and proof, and rejects device faults with a note. Those go to the Owner for a manual punch edit |

### Gaji Premi

| Period | Payout |
|---|---|
| Sep 1 – Oct 31 | Nov 15 |
| Nov 1 – Dec 31 | Jan 15 |
| Jan 1 – Feb 28/29 | Mar 15 |

| # | Decision |
|---|---|
| 30 | While open, everything is computed live from punches |
| 31 | **Owner presses Close period** (confirmation dialog, irreversible). At close: counted hours, tier and amount frozen onto each OT assignment; still-`Pending` correction requests and OT assignments **expire** → pay follows the punches / Rp0. No nightly job needed — expiry happens in the close action |
| 32 | After close, anything dated in that period is refused — only a rapel gets in |
| 33 | **Rapel:** a late claim for a **closed** period. Decided by the Owner only. Fields: an **approved OT assignment** dated inside that period, the **from–to times actually worked** (prefilled with the assigned window), note, **WA proof upload** (same attachment rules). The system computes counted hours with the same rules as the calculator (whole hours down, day-off lunch rule via the OT assignment's `IsDayOff`) and **suggests the amount = tier(claimed hours) − amount already frozen on that OT assignment**. The Owner approves it or overrides it; the employee never enters an amount. A claim that adds nothing (suggested amount ≤ 0) is refused on the form. Added as a line to the next payout, labelled with its work date: *"Rapel · OT 14 Oct"*. Frozen when its own payout period closes |
| 33b | **One rapel per OT assignment**, counting `Pending` and `Approved` (assumed, confirm) |
| 33c | **Request deadline: one month from the payout date** of the closed period (Sep–Oct, paid Nov 15 → open until Dec 15). After it the *Request Rapel* button stays visible but disabled ("Deadline passed, contact the Owner") and the server refuses the request. The Owner's direct add has no deadline |
| 33a | **The employee starts it:** a **Request Rapel** button on each closed-period row of *Gaji Premi Saya* (their table of past OT disbursements). It goes to the **Owner, never the Manager**. Pending until decided; a rejected one is kept in the history with the Owner's note. The Owner may also add a rapel directly, with the same fields (assumed, confirm) |
| 34 | Terminated employees are still paid for OT already worked |

### Visibility and UI

| Menu | Who | Contents |
|---|---|---|
| `/overtime` (Lembur) | everyone, scoped | Staff: own OT assignments, file correction requests, **Gaji Premi Saya** tab. Manager: create OT assignments for own Staff, decide correction requests — **no pay column, no Gaji Premi tab**. Owner: all + approve Managers' OT assignments |
| `/payroll/gaji-premi` | `roles: ['Owner']` | Period picker, Open/Closed, per-employee table, Add rapel, Close period |

**Gaji Premi Saya** is a table of the employee's own OT disbursements, one row per period: period,
payout date, counted hours per month, amount, rapel lines. Closed-period rows carry the **Request
Rapel** button (33a); the open period shows the running estimate and no button. It is self-only
(Owners read any employee's through the payroll page); Managers never see it.

Placement: decision Q17 put it as a **tab inside `/overtime`**. It can instead be its own sidebar
item (a one-line `NAV` entry in `sidebar.tsx`) — see open question 6. The open period shows a
running amount marked *"Estimate, not final"* so missing punches get corrected before close.

Period table (per employee, clicking a row opens the day-by-day list: date, assigned window, actual
punches, counted hours, tier, status):

| Employee | OT days (Sep) | Hours (Sep) | OT days (Oct) | Hours (Oct) | Rapel | Total |
|---|---|---|---|---|---|---|
| Budi | 4 | 14h | 3 | 9h | — | Rp 225.000 |

Hiding pay from Managers is a UI rule, not a secret: a Manager sees windows, punches and the tier
table, and can work the amount out. Accepted — unlike wage (GSS03), the figure reveals nothing hidden.

## Files

| File | Change |
|---|---|
| `Erp.Core/Aggregates/Overtime/*` | new: `OvertimeAssignment`, status enum, `OvertimeCorrectionRequest`, `GajiPremiPeriod`, `Rapel` |
| `Erp.UseCases/Overtime/*` | new: create/edit/cancel/approve OT assignment, correction requests, counted-hours calculator, period close, rapel |
| `Erp.UseCases/Attendance/Common/AttendanceDayRecomputeService.cs` | filter OT-owned punches |
| `Erp.UseCases/Leave/CreateLeaveRequest/*` | refuse leave on a date with an OT assignment |
| `Erp.Infrastructure/Persistence/*` | configurations + migration |
| `Erp.Web/Endpoints/Overtime/*`, `Erp.Web/Endpoints/Payroll/*` | new endpoints |
| `Erp.Web/appsettings.json` | `Overtime:Tiers` |
| `apps/web/src/app/overtime/*`, `apps/web/src/app/payroll/gaji-premi/*` | new pages |
| `apps/web/src/components/layout/sidebar.tsx` | two items; Payroll `roles: ['Owner']` |
| `apps/web/messages/{en,id}.json` | keys |

The counted-hours calculator is the money path — it gets a unit test per row of the examples table
above, plus the punch-ownership table.

## Resolved questions

| # | Question | Answer (2026-10-06) |
|---|---|---|
| 1 | Close order | Sep–Oct must close before Nov–Dec (built) |
| 2 | `ShiftEnd` moved past 18:30 | Policy refuses a `ShiftEnd` later than 18:00 |
| 3 | Correction for a wrong OT punch | Decision 29a: allowed for an early tap-out; a device clock fault goes to the Owner for a manual edit |
| 4 | Revised tiers | Confirmed: 2–3h 25rb, 4–5h 50rb, 6h+ 70rb |
| 5 | Rapel request deadline | One month from the payout date (33c) |
| 6 | Where Gaji Premi Saya lives | A tab inside `/overtime` |
| 7 | Rapel amount | Owner decides; suggested automatically from the claimed times (33) |
| 8 | OT on the attendance calendar | Done, see *Follow-up* above |
