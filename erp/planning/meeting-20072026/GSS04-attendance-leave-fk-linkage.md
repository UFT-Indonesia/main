# Grill Session Summary — 04 Attendance & Leave FK Linkage

Working doc from a grill-me session against `04-attendance-leave-fk-linkage.md`. Records what got resolved, what's still pending, and where to resume. Nothing here has been implemented — brainstorming only, no code/migration/commit made yet.

## Goal (plain-language, confirmed with user)

Approved leave and unexplained absence currently look identical to the system — both just show up as "no punches" on `AttendanceDay`. This branch makes approval visible downstream:
- Employee list "On Leave" badge (already built in frontend, never lit up — nothing backend sets `EmployeeStatus.OnLeave` today) actually works.
- Attendance table shows a day as covered by leave instead of looking like an unexplained gap.
- CSV export (payroll-facing) shows "On Leave" instead of blank/incomplete, so leave doesn't get misread as an absence.
- Cancelling the leave undoes all of the above.

## Reschedule module — reconfirmed, NOT related

User's assumption checked against code and refuted:
- No "reschedule" code exists anywhere in the repo (full grep, zero hits in `.cs`/`.ts`/`.tsx`).
- The only mention is in a *different, unimplemented* sibling planning doc, `03-leave-self-service-login.md:12-21`: a proposed `LeaveRequest.Reschedule(newStart, newEnd)` self-service method, explicitly restricted to `Status == Pending` only — an Approved request still requires cancel + resubmit.
- Since doc 04's FK-linkage only ever fires on **Approve**/**Cancel** of an already-decided request, and doc 03's Reschedule only ever touches **Pending** requests, the two features structurally cannot collide — a request is never in both states at once.
- Note: `36e52d5` ("refactor(Leave): staff role restrictions") already landed doc 03's *self-service filing* piece (`LeaveRules.CanFileFor` now supports filing for self). The `Reschedule` domain method itself is still unbuilt.
- **Correction (session 2, 2026-08-23):** self-cancel is **not** unbuilt — `CancelLeaveRequestEndpoint` (`Erp.Web/Endpoints/Leave/DecideLeaveRequestEndpoints.cs:95-103`) is `[Authorize]` only (any signed-in role), and `LeaveRules.CanCancel` (`Erp.UseCases/Leave/Common/LeaveRules.cs:41-42`) already permits `IsSelf(caller, subject) || CanDecideFor(caller, subject)`. A Staff employee with a login can already cancel their own request today, Pending or Approved. This was misreported last session.
- **Decision: drop reschedule from this branch's scope entirely.** Treat as unrelated.

## Session 2 (2026-08-23) — client scenario: emergency recall from approved leave

Client scenario: employee's leave for 23 Aug is approved, then an emergency requires them to work that day, so the manager/owner who assigned the emergency cancels the already-approved leave. Client asked whether this needs to be modeled as a new type of leave request.

- **No new leave type or workflow needed.** `LeaveRequest.Cancel` already works on an `Approved` request with no date restriction (`LeaveRequest.cs:146-156`), and `LeaveRules.CanCancel` already restricts non-self cancellation to the subject's decider (manager or Owner) — exactly the client's restriction. `LeaveType` (Annual/Sick/Permission/Unpaid) describes why leave was *taken*, not why it was *cancelled*, so it doesn't apply here.
- **New: `CancellationReason` enum, required on every `Cancel`.** Client wants emergency recalls distinguishable from ordinary withdrawals (e.g. for reporting). Minimal 2-value enum: `WithdrawnByEmployee`, `RecalledForWork`. No broader categories added — nothing else has been described by the client yet.
- **Auto-derived server-side, no new request field.** `DecideLeaveRequestService.DecideAsync` (`DecideLeaveRequestHandlers.cs:81-134`) already computes `IsSelf(caller, subject)` for authorization at the same point `Cancel` is invoked — reuse it: `IsSelf → WithdrawnByEmployee`, else `→ RecalledForWork`. No new API/UI field, no client-supplied value to trust, and it can't be set "wrong" (if a manager cancels their *own* request, that's a withdrawal regardless of role).
- **Punch-vs-leave precedence — resolved, closing session 1's open item.** The recall scenario is exactly the case that argues for "punch always wins": if the `LeaveRequestCancelled` revert hasn't finished when the recalled employee's punch lands, the punch handler must still be able to overwrite the day back to Complete/Incomplete regardless of event ordering. Decided: **punch always wins**, no special sequencing required between cancel-revert and punch-record.
- **Revert path on cancel — resolved.** On `LeaveRequestCancelled`, **delete** the materialized punch-less `AttendanceDay` row(s) for the cancelled range (rather than adding a 4th `AttendanceDayStatus.Cancelled` value). Reasoning: `AttendanceDay` is a punch-derived projection ("Materialized employee-day view over raw punches" per its own doc comment), not an audit log — `LeaveRequest` already retains full cancellation history (`Status=Cancelled`, `DecidedByUserId`, `DecidedByName`, `DecisionNote`, now `CancellationReason`). Deleting restores the same "no row = nothing happened yet" state every other day has with zero punches; if the employee does punch in later, the normal `AttendanceLogRecordedHandler` path creates a fresh row, consistent with "punch always wins." A `Cancelled` status was considered and rejected as a modeling smell — it would duplicate what `LeaveRequest` already tracks and only ever persists on days the employee never ends up punching in, which reads confusingly next to Complete/Incomplete/OnLeave on the attendance table.

## Decisions made this session

1. **No-punch leave days.** `AttendanceDay.Create` currently throws (`attendance_day.no_punches`) on an empty punch list, and rows are only ever created reactively from real punches (`AttendanceLogRecordedHandler`). A leave day has zero punches, so there's no existing row to stamp a FK onto.
   → **Decided: relax `AttendanceDay` to allow materializing punch-less rows.** New creation path (e.g. `AttendanceDay.CreateForLeave`) producing a row with no `TapIn`/`TapOut`, a new third `AttendanceDayStatus` value (`OnLeave`, alongside existing `Complete`/`Incomplete`), and the `LeaveRequestId` FK set. Bigger change than the original plan text implied — touches a core invariant, needs a migration, and an enum change that ripples into `attendance-day-table.tsx`'s `STATUS_VARIANT` map (currently only 2-way) and the CSV export's status column.

2. **Employee.Status vs AttendanceDay grain (plan's open question #1).**
   → **Decided: keep both.** `Employee.Status = OnLeave` for cheap list/badge filtering (no join needed on the employee list page), *and* the per-day `AttendanceDay.LeaveRequestId` FK for per-day accuracy/export. Accepted tradeoff: two sources of truth that must stay in sync (Employee.Status must revert to `Active` on cancel/expiry, same as the AttendanceDay rows).

3. **Weekend materialization (plan's open question #2).**
   → **Decided: workdays only, skip Sat/Sun.** Matches `LeaveRequest.CountWorkdays`, which already skips Sat/Sun for `WorkdayCount`. Weekends have no `AttendanceDay` row for anyone today (no punches, no rows) — materializing them only for leave would be inconsistent with how every other employee's weekend is represented.

## Resolved this session (2026-08-23) — no longer open

- **Punch-vs-leave precedence:** punch always wins (see session 2 above). Closed without a separate client round-trip — the recall scenario itself was the deciding case.
- **Revert path on `LeaveRequestCancelled`:** delete the materialized punch-less `AttendanceDay` row(s) (see session 2 above).

## Session 3 (2026-08-23 cont'd) — confirmed doc 04 unchanged, closed remaining open items

Re-checked `04-attendance-leave-fk-linkage.md` against git (`git log` shows last touch was `6fcbb1d` / #18, unchanged since) — nothing new there, GSS04 already covers all 3 of its plan steps and both its resolved open questions (#1, #2). This session closed the last original open question (#3) plus two wiring gaps found while designing the concrete implementation:

- **Doc 04 open question #3 — resolved: yes, add an export column, LeaveType only.** `LeaveType` (Annual/Sick/Permission/Unpaid) is low-sensitivity, safe to show to anyone who can already run the export. The free-text `Reason` stays out — `LeaveRules.CanReadDetails` (`LeaveRules.cs:50-51`) already restricts it to the employee, their decider, or Owner, and a bulk CSV export isn't scoped per-row to that check today. Adding `Reason` would require real per-row permission enforcement in the export path, not just a column; out of scope unless requested. `CancellationReason` in the export was not decided — still open, see below.
- **Owner auto-approve gap — resolved: yes, publish there too.** `CreateLeaveRequestHandler.cs:80-83` auto-approves an Owner's own leave inline (`LeaveRules.IsAutoApproved`) and has no `IMessageBus` today — it's a second call site for `Approve` beyond `DecideLeaveRequestHandlers`. Decided: add `IMessageBus` to `CreateLeaveRequestHandler` and publish `LeaveRequestApproved` there too when the auto-approve path fires, so an Owner's own leave gets the same FK-linkage materialization (Employee.Status, AttendanceDay rows) as anyone else's — no silent gap for one role.
- **Materialization vs. an already-punched day — resolved: skip.** Leave requests aren't restricted to future dates, so an approved range can include a day that already has a punch-derived `AttendanceDay` row (which would collide with the unique index on `(employee_id, calendar_date)` if materialization tried to insert). Decided: skip creating an `OnLeave` row for any day that already has an existing row — consistent with "punch always wins." Only genuinely punch-less days in the range get materialized.

### Flagged, not yet resolved (low priority, noted while checking Approve call sites)

- `CancelForTermination` (`LeaveRequest.cs:163-174`) only auto-cancels a **Pending** request on employee termination — an **Approved**, already-materialized leave is left untouched (still `Approved`, its `AttendanceDay` rows/FK stay put) even though `Employee.Status` gets forced to `Terminated` separately by the termination flow. Net effect: an employee terminated mid-approved-leave keeps `OnLeave`-tagged `AttendanceDay` rows for leave dates past their termination date, while `Employee.Status` itself correctly shows `Terminated` (single field, overwritten regardless of prior value). Cosmetic edge case, not blocking — flag for a decision later (leave as historical record vs. also revert on termination) rather than resolving now.

## Session 4 (2026-08-23 cont'd) — implementation landed, two deviations grilled

The branch was implemented (build clean, 329 unit tests passing, 4 new). Nothing committed. Two places where the implementation departed from what this doc said, plus one leftover, were put back through the grill:

- **Deviation 1 — the leave link survives punches.** This doc's earlier wording said punches "clear/ignore the FK". The implementation keeps `AttendanceDay.LeaveRequestId` set even once punches land; punches still win the *status* (Complete/Incomplete), the link just stays as an annotation. Reason: if the link were cleared, deleting or correcting a mistaken punch would permanently lose the fact that leave covered that day — there is nothing left to restore it from. `AttendanceDay.RevertToLeave()` handles that path, falling the row back to `OnLeave` when its last punch goes away.
  - **Consequence grilled:** a worked-during-approved-leave day exports as `Complete` + `LeaveType`, but the attendance *table* showed a plain `Complete` badge with no hint leave was involved — export surfaced the anomaly, the UI hid it.
  - **Decided: surface it in the table too, as a secondary badge beside the status badge** (not a new column — the table already has seven, and a leave column would be blank on nearly every row). A leave-covered row reads `[Complete] [Annual]`; an ordinary leave day reads `[On leave] [Sick]`; an ordinary working day is unchanged. Needs `leaveType` added to `AttendanceDayListItemResult` and the existing leave-type translations reused from the leave namespace. **Not yet implemented.**

- **Deviation 2 — a new hourly background job.** Nothing in the codebase un-flips `Employee.Status` when leave simply *ends*: approve and cancel both reconcile inline, but a leave that runs out on its own fires no event, and the repo had no recurring-job infrastructure at all (only fire-and-forget `IBackgroundJobClient`). Without something scheduled, the badge would stay lit forever.
  - Added `SyncEmployeeLeaveStatusJob`, registered hourly via Hangfire's `IRecurringJobManager` in `Program.cs`. It reuses the same `LeaveAttendanceSync.ReconcileEmployeeStatusAsync` the event handlers call, so there is no duplicated rule. Reconciliation re-derives from the leave table rather than toggling, which makes it idempotent — a stale, duplicate, or missed firing cannot corrupt the flag.
  - Alternatives weighed and rejected: scheduling exact one-off jobs at each leave's start/end (precise, but nothing self-heals a transition that never got scheduled — e.g. leave approved before this shipped); and dropping the stored flag to derive `OnLeave` at query time (always accurate and needs no job, but reverses this doc's "keep both" decision and adds an EXISTS subquery to both the employee list and count specs).
  - **Decided: keep the hourly job.** Accepted cost: at a natural start/end boundary the badge can lag by up to an hour. Decisions made through the app are still instant.

- **Leftover — `CancellationReason` was stored but displayed nowhere.** The export question turned out to be moot: cancelling deletes the attendance rows the leave created, so there is no row left to carry a cancellation reason. But that left the field saved to the database and visible on no screen and in no API response — the client asked for it precisely so recalls could be told apart from ordinary withdrawals, and nobody could see that difference.
  - **Decided: show it on the leave list/detail** — a cancelled request reads "Cancelled — recalled for work" rather than just "Cancelled". Exposed to anyone who can see the request at all, *not* gated behind `CanReadDetails` the way the free-text `DecisionNote` is: the reason is about as sensitive as the `Cancelled` status itself, which is already visible company-wide. Needs the field through `LeaveRequestResult` + `LeaveContracts` and labels in `en.json`/`id.json`. **Not yet implemented.**
  - **Decided: no `CancellationReason` column in the attendance export** — nothing to attach it to.

## Session 5 (2026-08-23 cont'd) — outstanding work landed

Both items agreed in session 4 are now implemented:
1. `leaveType` threaded through `AttendanceDayListItemResult` → `AttendanceDayListItemResponse` → `attendance-day-table.tsx`. Renders as a secondary outline badge beside the status badge (`[Complete] [Annual]`), only when a leave link exists — no new column.
2. `CancellationReason` threaded through `LeaveRequestResult` → `LeaveRequestResponse` (unconditionally, not gated behind `canReadDetails`) → the leave list row (muted text under the status badge) and `LeaveDetailsDialog` (its own row, only when Cancelled). Labels added in both `en.json`/`id.json` under `leave.cancellationReason.*` and `leave.details.cancellationReason`.

Frontend typechecks and lints clean; backend build clean, 329 unit tests still passing.

## Session 6 (2026-08-24) — terminated mid-approved-leave, closed

The last flagged edge case is now handled. An employee terminated partway through approved leave kept `OnLeave` attendance rows for dates after they left the company — visible in the table and flowing into the payroll export as though they were still employed on approved leave.

**Decided and implemented:** a new `EmployeeTerminatedAttendanceHandler` (fifth consumer of `EmployeeTerminated`, alongside the existing leave, account, refresh-token and audit handlers) deletes the leave-generated rows dated *after* the termination date, via `LeaveAttendanceSync.DropLeaveDaysAfterAsync` + `LeaveOnlyDaysAfterSpec`.

Judgment calls baked into it:
- **Only rows after the termination date.** Days on or before it are genuine history — the employee really was employed and on leave then. The same rule handles a future-dated termination ("last working day is 30 September") correctly.
- **Only rows the leave itself created** (`TapInUtc == null`). A row carrying real punches is real attendance and is never deleted, whatever its date.
- **Punched rows keep their leave link** — deliberately unlike the cancellation path, which clears it. A cancelled leave no longer applies, so the attribution would be wrong; a terminated employee's leave was never cancelled and stays approved history, so a day with both leave and a punch is still accurately attributed.
- **The `LeaveRequest` itself is untouched**, keeping its original dates and `Approved` status. This preserves the intent already stated on `EmployeeTerminatedLeaveHandler` ("Approved leave is left alone — it already happened, and the record should stay truthful"): only the derived attendance rows are trimmed, never the decision that was made.

Rejected: keeping the rows as-is (leaves misleading data in the payroll export), and additionally trimming the approved leave's end date to the termination date (rewrites a decision that was genuinely made).

## Status

Implemented and green, **not committed**. Backend build clean, 331 unit tests pass (6 covering the leave-attendance sync, including the termination sweep and its date/punch filtering). Frontend typechecks and lints clean. Every decision from sessions 1–6 is reflected in code; nothing outstanding.
