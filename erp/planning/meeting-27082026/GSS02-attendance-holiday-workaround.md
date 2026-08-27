# GSS02 — Attendance Holiday & Working-Week Workaround

Scoping note for a **future** feature. Split out of `GSS01 Enhancements-08-26.md` (item 4, session 2, 2026-08-27) when "should the picker grey out weekends?" surfaced a gap the codebase has never modelled.

**Not started. No decisions made beyond the one below.**

**Decision that created this:** weekends are **NOT** greyed in the new pickers. Leave ranges legitimately span weekends (they're just not *charged*), and people do punch on Saturdays. Adding `{ dayOfWeek: [0,6] }` would break filing a Fri–Mon leave.

## The gap

**There is no holiday concept anywhere.** A full grep across `.cs`/`.ts`/`.tsx`/`.json` for `holiday` / `libur` returns zero hits outside the word "workday".

What exists is a **hardcoded Mon–Fri rule**, duplicated in two places that must change together:

| Location | Code |
|---|---|
| `Erp.Core/Aggregates/Leave/LeaveRequest.cs:204-213` | `Workdays()` — skips `IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday`, nothing else |
| `apps/web/src/components/leave/leave-dialogs.tsx:31-43` | `countWorkdays()` — already carries a `ponytail:` comment: *"mirrors the backend's hardcoded Mon–Fri workday rule; update both together if weekends ever become configurable"* |

Everything downstream inherits it:
- `LeaveRequest.CountWorkdays` → `WorkdayCount`, the number the quota engine charges (`LeaveQuota.WorkdaysInYear`, `UsedDays`)
- `LeaveRequest.Create` throws `leave.no_workdays` for an all-weekend range (`:114-118`)
- `LeaveAttendanceSync.MaterializeAsync` materializes `OnLeave` rows for workdays only — *"Weekends are skipped — nobody has a row for those"*
- the `workdayPreview` string (`id.json:298`)

`AttendancePolicy` (`ShiftStart`, `ShiftEnd`, `ClockInGraceMinutes`, `ClockOutGraceMinutes`, `TimeZoneId`) is the natural home for a working-days/holiday config and currently has **no** field for either.

## Why it matters (Indonesian context)

The company operates on `Asia/Jakarta`. Indonesia has a substantial calendar of **hari libur nasional** plus government-declared **cuti bersama**, which move year to year. Today the system charges annual-leave days for public holidays inside a leave range, and shows those days as `Incomplete` for everyone else — an unexplained absence on a day the office was shut.

## Open questions (none answered)

1. **Where does the working week live?** A field on `AttendancePolicy` (day-of-week set), or hardcoded with only holidays configurable on top?
2. **How do holidays get in?** Manual Owner entry, seeded per year, or an external Indonesian holiday source? Manual is the lazy answer and probably right.
3. **Do holidays refund leave?** Almost certainly not charged — but `WorkdayCount` is **stored at creation** (`LeaveRequest.cs:114`), so a holiday declared *after* approval leaves a stale count.
4. **Retroactive holidays.** Cuti bersama is sometimes announced late. Does declaring one recompute approved leave and materialized `AttendanceDay` rows, or apply only forward?
5. **Attendance status on a holiday.** A day nobody works shouldn't read `Incomplete`. New `AttendanceDayStatus`, or suppressed otherwise? `STATUS_VARIANT` (`attendance-day-table.tsx:31-36`) and the CSV export both enumerate statuses.
6. **Picker behaviour.** Holidays come nearly free once they exist — `isDateUnavailable` already takes arbitrary logic. Whether they should be *disabled* vs merely *marked* is a separate call.

## Weekend-inside-leave asymmetry

Q4i′ decided `/leave/blocked-dates` returns the **raw** leave range, so a Fri→Mon leave greys Sat and Sun too. Deliberate — the person is away — but it makes the picker the *only* surface where those weekend days exist.

| Surface | Grain | Weekend inside a leave range |
|---|---|---|
| `/leave` page | one row per **request** | never a row; a Fri–Mon leave is one row reading "2 hari kerja" |
| Attendance day table | one row per **day** | **no row** — `LeaveAttendanceSync.cs:38` iterates `LeaveRequest.Workdays(...)`, which skips Sat/Sun |
| New date picker | per day | **greyed** |

**The sharper bug this exposes.** If an employee punches on Sat 19 during a Fri 18 → Mon 21 leave:

1. The punch handler creates an `AttendanceDay` row for Sat 19.
2. `MaterializeAsync` had **skipped** that date, so `LeaveRequestId` is never set on it.
3. The row renders as an ordinary attendance day — **no `Cuti` badge**, no link to the leave covering it — while the picker had greyed that exact day.

Consequences to weigh:
- The row is invisible to item 5's leave-summary panel, because it carries no `leaveType`.
- `LeaveAttendanceSync.ReleaseAsync` won't unlink it on cancellation — there is no link to release.
- Whether the fix is "materialize weekends too" depends on question 1 above. Materializing weekends today would create `OnLeave` rows for days nobody has rows for, which GSS04 explicitly rejected as inconsistent.

## Pending leave is not blocked

`/leave/blocked-dates` returns **`Approved` only**, matching `ApprovedLeaveOverlappingSpec`, the rule the server enforces. A day covered by a **Pending** request stays fully selectable in both the attendance dialogs and the leave form.

Correct default — a pending request isn't leave yet, and the person is at work until someone approves it. But a plausible client surprise: *"I can see he asked for leave that day, why did it let me log him in?"* Raise before it's discovered in a demo; if they want pending days marked, the honest treatment is a **visual marker** (RAC styles that separately from `isDateUnavailable`) rather than actually blocking selection.
