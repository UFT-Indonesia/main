# GSS03 Follow-up — Potongan Cuti: gaps, corrections after close, adjustments

Created 2026-10-07 from a grill session (Q1–Q17) on the known gaps left by
[GSS03](GSS03-payroll-deduction-plan.md) (PR on `feat/owner-payroll-engine`). GSS03 stays the base
design; this doc only adds to it or changes it, and says so where it does.

**Status: planned, not built.**

## Cast used in every example

| Who | Role | Notes |
|---|---|---|
| **Kevin** | Owner | Runs payroll, the only one who sees rupiah |
| **Rina** | Manager | Decides leave for her Staff |
| **Budi** | Staff, reports to Rina | Salary Rp 5.000.000, divisor 20 → **Rp 250.000** per cut day. Annual cap 12 |

## Summary of decisions

| # | Topic | Decision |
|---|---|---|
| Q1 | Edit re-queue | Unchanged: every edit of an Approved leave re-queues it at the edit time (GSS03 decision 6) |
| Q2 | Edit form warning | Show over-quota days **before → after** while editing |
| Q3 | Locked Edit/Cancel | Buttons visible but **disabled with the reason** when the leave touches a closed month |
| Q4 | Locked Approve | Approve **disabled with the reason** when approving would cut a day in a closed month; Deny stays |
| Q5 | Divisor history | History table; page shows the last change and a History list |
| Q6 | Late approval into a closed month | Approval **stamps** the closed-month days as frozen lines, as close would have |
| Q7 | Pending blocking close | (A) clickable pending rows on Potongan Cuti, (B) banner on the Leave page, (C) Close states the exact reason |
| Q8 | Mistake after close | **Owner-only correction** with a required reason; paid figures stay frozen |
| Q9 | Money of a correction | The difference lands on the **next open month** as a "Late correction", both directions |
| Q10 | Price of a correction | The **closed month's rate**: close records the divisor and each employee's exception |
| Q11 | Quota returned by a correction | Frees cut days in **open months only**; closed cut days stay cut (GSS03 decision 15) |
| Q12 | Manual adjustments | **Owner can add adjustment lines** on an open month |
| Q13 | What Staff/Manager see | The correction and its reason on the leave request; never rupiah |
| Q14 | Refund months | Rounded **toward zero** to Rp 1.000; shown as **"Refund"**; header splits cuts and refunds |
| Q15 | Adjustment rules | Both directions, reason required; any open month; any non-Owner employee active or terminated within that month; delete and re-add, no edit |
| Q16 | Correction dialog | Warning banner, required reason, Effect panel (days + rupiah); target = first open month at the moment of correcting |
| Q17 | Showing corrections | Closed month keeps the paid line with a marker; receiving month lists the late correction with a link back; adjustments in their own section |

Decided without a question (follow from the code):

| Topic | Decision |
|---|---|
| Owner's Unpaid days | `LeaveDeductionCalculator.Allocate` checks "uncapped" before "Unpaid", so an Owner's days are never labelled cut (GSS03 decision 5) |
| Owner-filed leave | `CreateLeaveRequestHandler` auto-approves Owner-filed leave; it now takes the shared payroll lock and stamps closed-month days (Q6) like the Approve handler |
| Exception audit | Changing an employee's deduction exception writes an Owner-only employee audit-log entry (closes GSS03 status note "no audit-log entry yet") |
| Migration | New migration; `AddLeaveSalaryDeduction` is already pushed and is not amended |

---

## Part 1 — Gaps from the GSS03 PR

### Q2. Over-quota warning on the edit form

**Today:** the create form warns *"2 of these days are over your Annual quota and will be cut"*; the edit
form (`edit-leave-dialog.tsx`, Owner/Manager only) says nothing.

**Example:** Budi's approved 9–12 Mar leave is fully free. Rina extends it to 9–16 Mar. While she picks
the new end date the form shows:

```
Over quota: 0 → 2 days (salary cut)
```

Nothing is shown when both numbers are 0. Days only, never rupiah — except in the Owner's
after-close correction dialog (Q16).

- **Before** = the request's current `overQuotaDays` (already on the list response).
- **After** = `GET /api/leave/over-quota` with a new optional `excludeRequestId`, so the request's own
  approved days are not counted twice. `LeaveDeductionEngine.CandidateDaysAsync` already accepts it.

### Q3. Edit and Cancel on leave in a closed month

**Today:** `LeaveRequestResult.PermissionsFor` ignores payroll, so the buttons work and the save fails
with `leave.payroll_closed`.

**Example:** Budi's 16–18 Mar leave, March closed.

| Who | Edit / Cancel | Text on the request |
|---|---|---|
| Budi | disabled | *"Payroll for March 2026 is closed. Only the Owner can correct it."* |
| Rina | disabled | same |
| Kevin | enabled, opens the **correction** dialog (Q16) | *"Payroll for March 2026 is closed. Changes are corrections after close."* |

Rules (unchanged from GSS03 except the Owner path, which is new in Q8):

- Edit is locked for any request (Pending or Approved) with a workday in a closed month.
- Cancel is locked only for Approved; withdrawing a Pending one costs nothing and stays allowed.

### Q4. Approve that would cut inside a closed month

**Example:** Budi files Sick for 25 Mar on 3 Apr (March closed). Free at filing, so it was accepted
(GSS03 decision 8). Before Rina decides, another Sick approval uses the rest of the 30-day cap.
Approving now would cut 25 Mar — in a closed month.

Rina sees Approve disabled with:

> Approving would cut 1 day in March 2026, whose payroll is closed. Deny it, or ask Budi to withdraw it.

Deny stays enabled. Kevin sees the same (he can still correct afterwards via Q8 if it was approved
another way, but approval itself stays refused).

### Response fields for Q3/Q4

On `LeaveRequestResult` (and `LeaveRequestResponse`):

| Field | Meaning |
|---|---|
| `payrollClosedMonth` | Earliest closed month with a workday of this request, else null |
| `approveBlockedMonth` | Pending only: the closed month in which approving now would cut a day, else null |
| `canEdit` / `canCancel` | Folded: false for non-Owners when `payrollClosedMonth` is set (Cancel only if Approved) |
| `isCorrection` | True when the caller is the Owner and Edit/Cancel would be a correction after close |

The front end reads the reason text from these; it never re-derives payroll rules.

### Q5. Divisor history

**Example:** Kevin changes the divisor 20 → 22 on 7 Oct. Under the field:

> Last changed from 20 to 22 by Kevin on 7 Oct 2026 · History

History opens a list: old → new, who, when. Explains why an open month's estimate moved.

- New table `PayrollSettingsChange` (old divisor, new divisor, user id, name, time), written by
  `SetPayrollDivisorHandler` in the same transaction. The server log line stays.
- `LeaveDeductionMonthResult.LastDivisorChange`; `GET /api/payroll/leave-deductions/divisor-history`.

### Q6. Stamping a late approval into a closed month

**Today:** a free request approved after its month closed writes no frozen line; a later quota change
could silently relabel that day as cut, and that cut is never priced.

**Now:** every approval path stamps the request's closed-month days as `LeaveDeductionLine`s (free,
with salary and rate), exactly as close would have. GSS03 decision 15 then holds for every closed day.

Approval paths (all must stamp and take the shared payroll lock):

| Path | File |
|---|---|
| Approve | `DecideLeaveRequestHandlers.cs` — `ApproveLeaveRequestHandler` |
| Owner-filed auto-approve | `CreateLeaveRequestHandler.cs:161` — **currently takes no payroll lock** |
| Owner edits a Pending request (auto-approves) | `EditLeaveRequestHandler.cs:141` — refused in closed months already; no stamping needed |

Owners' days are skipped, as at close. The closed month's day list gains the free day; its total is
unchanged. Remove GSS03 status note "A free request approved late … writes no frozen line".

### Q7. Pending leave blocking a close

**Example:** 28 Sep Budi files Sick 29–30 Sep; Rina hasn't decided. 3 Oct Kevin wants to close September.

**A. Clickable pending rows (Potongan Cuti, Kevin).** Clicking *Budi · Sick · 29–30 Sep* opens the
existing Approve/Deny dialog with its over-quota days. Needs `GET /api/leave/{id}` (does not exist yet;
the dialog takes a full `LeaveRequest`). After deciding, the list refreshes and Close enables.

**B. Banner on `/leave` (Kevin and Rina).**

| Who | Banner |
|---|---|
| Rina | *"1 leave request in September is waiting for your decision. September payroll can't be closed until it's decided."* — counts only requests she may decide (`LeaveRules.CanDecideFor`); no rupiah, no payroll link |
| Kevin | Company-wide count, with a link to Potongan Cuti |
| Budi | Nothing |

Clicking filters the list to those requests. Months counted: ended, on or after the launch month, not
closed. New endpoint `GET /api/leave/close-blockers`.

**C. Close states its reason.** The disabled Close button shows one line instead of a generic tooltip.
The server returns the blockers, the page only renders them:

| Code | Text |
|---|---|
| `pending` | *Waiting on 1 pending leave request* |
| `earlier_open` | *Close August first* |
| `not_ended` | *September hasn't ended yet* |
| `before_launch` | *Before launch — nothing to close* |

`LeaveDeductionMonthResult.CloseBlockers` replaces the client guessing from `canClose`.

### Exception audit log

`Employee.SetLeaveDeductionException` raises `EmployeeLeaveDeductionExceptionChanged` (old, new);
`EmployeeDomainEventPublisher` publishes it; a handler writes `EmployeeAuditLog` like
`EmployeeLeaveQuotaChangedHandler`. The audit-log page (Owner-only) gets a label and old → new
rendering: *"Leave deduction: company divisor → flat Rp 0 per day"*. Grep every place the audit-log
event types are labelled (web page, export) — see memory "thorough scans".

---

## Part 2 — Corrections after close (new)

GSS03 decision 3 made closed months fully locked. **This changes it:** the Owner may correct.

### Q8. Who may correct, and what stays frozen

**Example:** Budi's Annual 16–18 Mar, already over quota, so 18 Mar is cut (Rp 250.000). March closed
1 Apr and paid. 10 Apr Rina finds Budi worked on 18 Mar.

- Only Kevin can edit or cancel leave with a day in a closed month; a **reason is required**.
- The closed month's paid figures never change (March still shows Rp 250.000).
- The leave record is corrected (16–17 Mar); Budi's quota is recomputed for open months (Q11).

### Q9. Where the money difference goes

The difference is a **late correction** on the **first open month at the moment of correcting**
(here April), in either direction:

| Correction | Late correction in April |
|---|---|
| Remove cut 18 Mar | **−Rp 250.000** (refund) |
| Add 19 Mar (would be cut) | **+Rp 250.000** (extra cut) |

The target month is fixed when the correction is saved and freezes when that month closes. There is
always an open month (the current one can't be closed until it ends).

### Q10. Price of a correction

- **Refund** of a frozen cut day: its own frozen `CutDays × DailyRate` — exactly what was deducted.
- **Added** cut day in a closed month: salary in effect on that date (salary history) ÷ **the month's
  divisor at close**, or **the employee's exception at close**.

Example: March closed with divisor 20; Kevin changes it to 25 on 5 Apr; on 10 Apr he adds 19 Mar →
priced at **Rp 250.000** (March's rate), not Rp 200.000.

Close therefore records:

- `LeaveDeductionMonth.Divisor`
- `LeaveDeductionMonthException` — each employee's exception (flat amount / divisor) as it stood at close

### Q11. Quota returned by a correction

**Example:** Budi cap 12. Jan (closed) 10 free days. Mar (closed) 3 days: 2 free, 20 Mar cut, paid.
May (open) 2 days, both cut. 10 Jun Kevin removes a free 15 Jan day → 1 quota day back.

- 20 Mar **stays cut** (closed labels never move — GSS03 decision 15).
- The day frees one **May** day: May's estimate drops Rp 500.000 → Rp 250.000.
- If no open-month cut day exists to benefit, Kevin can refund by hand with a manual adjustment (Q12).

### How a correction is computed

For each closed month the request touches, compare its days before and after the correction:

| Day | Before | After | Late correction |
|---|---|---|---|
| Removed (edit shortened, or cancelled) | frozen line | — | −(frozen cut × frozen rate) |
| Added in the closed month | — | label from the live queue (other closed days keep their labels) | +(cut × month's rate, Q10) |
| Kept, same charge | frozen line | unchanged | none |
| Kept, charge changed (e.g. full ↔ half day) | frozen line | treated as removed + added | both lines |

- Removed days' frozen lines are **kept** (history, Q17) and marked corrected; `Allocate` already
  ignores lines whose request/day no longer exists or is not approved.
- Added days get a new frozen line (so decision 15 holds for them too).
- Days in **open** months follow the live calculation as today; no late correction.
- A correction whose net is 0 rupiah (only free days changed) writes no late-correction line.

### Q13. What Staff and Manager see

On the leave request, for anyone who can read its details (Budi, Rina, Kevin):

> Corrected after March 2026 payroll was closed, by Kevin on 10 Apr: Budi worked on 18 March, confirmed by Rina.

Plus the updated over-quota days (1 → 0). No rupiah; manual adjustments are never shown to them.
Stored on `LeaveRequest`: correction reason, corrected by, corrected at (latest correction).

### Q16. The Owner's correction dialog

Same Edit / Cancel dialogs, in correction mode when `isCorrection` is true:

```
Edit leave — Budi, Annual
┌──────────────────────────────────────────────────────────┐
│ ⚠ March 2026 payroll is closed. This is a correction     │
│   after close: the paid March figures won't change.      │
└──────────────────────────────────────────────────────────┘
Start  [16 Mar 2026]    End  [17 Mar 2026]

Reason (required, shown to Budi and his manager)
[ Budi worked on 18 March, confirmed by Rina            ]

Effect
  Over-quota days on this leave:   1 → 0
  Late correction in April 2026:   Refund Rp 250.000
                                   (18 Mar, at March's rate)

                                 [Cancel]  [Save correction]
```

- The Effect panel comes from a preview endpoint (same computation as the save, no write):
  `POST /api/leave/{id}/correction-preview`.
- Cancel dialog: same banner and Effect panel; its note becomes the required reason.

---

## Part 3 — Manual adjustments (new)

### Q12/Q15. Adding an adjustment

**Example:** Budi's 20 Mar cut stays cut after the January correction (Q11) and he has no leave left
this year to benefit. Kevin refunds it by hand: June page → **Add adjustment**:

| Field | Value |
|---|---|
| Employee | Budi |
| Amount | −Rp 250.000 (negative = refund, positive = extra deduction) |
| Reason (required) | Refund 20 Mar cut — January corrected |

Rules:

1. Both directions. The dialog hints: *"Missed days should be filed as leave so the quota stays right."*
2. Any **open** month.
3. Any **non-Owner** employee who is active, or was terminated within that month. A row appears for an
   employee with only an adjustment.
4. **Delete and re-add only**, while the month is open. Each line keeps one author, time and reason.
5. Owner-only (`LeaveDeductionMonths.CanSee`). Never shown to Staff or Managers.

New table `LeaveDeductionAdjustment` (month, employee, amount, reason, user id, name, time).
`POST /api/payroll/leave-deductions/adjustments`, `DELETE /api/payroll/leave-deductions/adjustments/{id}`.
Both refuse a closed month (`payroll.already_closed`) and take the shared payroll lock.

---

## Part 4 — Month totals and display

### Q14. Rounding and refunds

An employee's month total = exact cut amounts of that month's days **+ late corrections targeting the
month + adjustments**, summed, then rounded **toward zero** to Rp 1.000, once.

> Changes GSS03 decision 9 ("rounded down") for negative totals only: positive totals are unchanged
> (Rp 341.666 → Rp 341.000); a refund of Rp 122.222 → **Rp 122.000** (not 123.000).
> `LeaveDeductionCalculator.MonthTotal` becomes `Math.Truncate(sum / 1000) * 1000`.

**Example — Budi, June:** cut day +Rp 227.777,75; late correction −Rp 250.000; adjustment −Rp 100.000 →
−Rp 122.222,25 → **Refund Rp 122.000**.

Display:

- A negative row shows **"Refund Rp 122.000"** in a distinct colour, not "−Rp 122.000".
- The page header splits instead of netting: **"Cuts Rp 4.250.000 · Refunds Rp 122.000"** (sum of
  positive rows · sum of negative rows).

### Q17. How corrections and adjustments appear

**March (closed)** — Budi's row total stays Rp 250.000. Day list:

```
18 Mar  Annual  16–18 Mar  Cut   Rp 5.000.000  Rp 250.000  Rp 250.000
        ↳ Corrected after close by Kevin on 10 Apr — refunded in April 2026
```

**April (open)** — Budi's day list:

```
Late correction from March 2026
18 Mar  Annual  Refund  −Rp 250.000   by Kevin on 10 Apr
        "Budi worked on 18 March, confirmed by Rina"   [view March]

Adjustments
        −Rp 100.000   "Refund 20 Mar cut — January corrected"   by Kevin on 12 Jun   [Delete]
```

Close freezes the month's late corrections and adjustments along with its days.

---

## Role walk-through

| Role | What changes for them |
|---|---|
| **Staff (Budi)** | Create form warns days over quota (unchanged). On a request in a closed month: Cancel disabled with *"Only the Owner can correct it"*. After a correction: sees the correction note and reason, and updated over-quota days. Never sees rupiah, adjustments or the payroll page |
| **Manager (Rina)** | Edit form shows over-quota days before → after. Edit/Cancel/Approve disabled with the reason when payroll blocks them. Leave page banner when her undecided requests block a month's close. Sees correction notes. Never sees rupiah |
| **Owner (Kevin)** | Potongan Cuti: Close says why it's disabled; pending rows open Approve/Deny; divisor shows its last change and history; Add adjustment; refunds labelled; header splits cuts/refunds; corrections linked between months. Leave page: can correct closed-month leave with a reason and sees the rupiah effect before saving. Audit log shows exception changes |

## Files

| Area | File | Change |
|---|---|---|
| Core | `Aggregates/Payroll/LeaveDeductionCalculator.cs` | uncapped before Unpaid; `MonthTotal` truncates toward zero |
| Core | `Aggregates/Payroll/LeaveDeductionMonth.cs` | `Divisor` on month; `LeaveDeductionMonthException`; line correction marker; `LeaveDeductionCorrection`; `LeaveDeductionAdjustment` |
| Core | `Aggregates/Payroll/PayrollSettings.cs` | `PayrollSettingsChange` |
| Core | `Aggregates/Leave/LeaveRequest.cs` | Owner correction (edit/cancel after close) with reason, corrected by/at |
| Core | `Aggregates/Employees/Employee.cs` + `Events/` | `EmployeeLeaveDeductionExceptionChanged` |
| Infrastructure | `Persistence/Configurations/PayrollConfigurations.cs`, new migration | tables and columns above |
| Infrastructure | `DomainEventHandlers/` | exception audit handler |
| UseCases | `Payroll/LeaveDeductions.cs` | close records divisor + exceptions; totals include corrections + adjustments; `CloseBlockers`; divisor history; adjustments add/delete |
| UseCases | `Payroll/Common/LeaveDeductionEngine.cs` | correction diff + pricing at the month's rate; stamping helper |
| UseCases | `Leave/DecideLeaveRequest`, `CreateLeaveRequest`, `EditLeaveRequest` | stamping (Q6); lock on create; Owner correction path; correction preview |
| UseCases | `Leave/Common/LeaveRequestResult.cs`, `ListLeaveRequests` | `payrollClosedMonth`, `approveBlockedMonth`, `isCorrection`, folded `canEdit`/`canCancel`, correction note |
| UseCases | `Leave/GetLeaveOverQuota` | `excludeRequestId` |
| UseCases | new `Leave/GetLeaveRequest`, `Leave/GetCloseBlockers` | `GET /api/leave/{id}`, `GET /api/leave/close-blockers` |
| UseCases | `Employees/SetLeaveDeductionException` | raises the audit event |
| Web | `Endpoints/Payroll/LeaveDeductionEndpoints.cs`, `Endpoints/Leave/*` | new routes above |
| FE | `components/leave/edit-leave-dialog.tsx` | before → after warning; correction mode (banner, reason, Effect) |
| FE | `components/leave/leave-dialogs.tsx`, `app/leave/page.tsx` | disabled reasons (Q3/Q4); cancel correction mode; close-blocker banner; correction note |
| FE | `app/payroll/potongan-cuti/page.tsx` | close reasons; clickable pending rows; divisor history; adjustments; refund label; split header; correction lines + markers |
| FE | `app/employees/audit-log/page.tsx` (+ export labels) | exception event label |
| FE | `lib/api/*`, `hooks/*`, `messages/{en,id}.json` | types, hooks, keys |
| Docs | `GSS03-payroll-deduction-plan.md` | point its status notes to this doc; drop the two caveats this closes |

## Tests

Calculator / engine (one per row unless noted):

- Owner's Unpaid day is free.
- `MonthTotal`: positive truncates down; negative truncates toward zero (−122.222,25 → −122.000).
- Correction diff: removed cut day → refund at frozen rate; added cut day in closed month → month's
  divisor and exception at close (divisor changed since, Q10 example); charge change (full → half) →
  removed + added; only-free change → no line.
- Quota returned by a correction frees an open-month cut day, never a closed one (Q11 example).

Handlers:

- Late approval into a closed month stamps free lines; Owner-filed auto-approve stamps and takes the lock.
- Non-Owner edit/cancel in a closed month refused; Owner correction requires a reason; target month is
  the first open month at save time.
- `approveBlockedMonth` set when approving would cut in a closed month; Approve still refused server-side.
- `/over-quota` with `excludeRequestId` doesn't double-count the request.
- Close records divisor and exceptions; `CloseBlockers` for each of the four reasons.
- Adjustments: Owner-only, open months only, no Owners, terminated-within-month allowed, delete only while open.
- Divisor change writes `PayrollSettingsChange`.
- Exception change writes an audit-log entry.
- Close-blockers for a Manager count only requests they may decide.
