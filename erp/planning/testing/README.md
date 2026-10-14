# UI Test Guide — Attendance, Leave, Overtime, Payroll

Step-by-step manual tests, one valid and one invalid path per rule. Written from the code and the
planning docs (`planning/meeting-*`) on 2026-10-13; **not yet run against the live app**.

## Files, in the order to run them

| File | Covers | Phase |
|---|---|---|
| [00-test-bed-setup.md](00-test-bed-setup.md) | Database, cast of employees and accounts, policy, fixed dates. **Do this first.** | Setup |
| [01-calendar-and-attendance.md](01-calendar-and-attendance.md) | Attendance policy, holidays, punches, day statuses, calendar | A |
| [02-leave.md](02-leave.md) | Leave requests, quota, probation, decisions, edits | A (+ some B/C) |
| [03-overtime.md](03-overtime.md) | OT assignments, punches, counted hours, correction requests | A |
| [04-gaji-premi.md](04-gaji-premi.md) | Overtime pay periods, close, rapel | B, C |
| [05-potongan-cuti.md](05-potongan-cuti.md) | Leave salary cuts, divisor, close, corrections, adjustments | A, B, C |
| [06-cross-module.md](06-cross-module.md) | Leave vs OT, holidays vs payroll, termination, role sweep | A, C |

## Phases (why the order matters)

- **Phase A, open data:** everything that can be edited freely. Run modules 01, 02, 03 first, plus the "open" cases of 04 and 05.
- **Phase B, closing:** Gaji Premi close and Potongan Cuti close are **irreversible**. Run the close cases only after all Phase A data exists.
- **Phase C, after close:** locks, corrections, rapel, adjustments. These only work on closed data.

To redo a run, **drop and recreate the database**. There is no undo for a close.

## Case format

Every case has the same fields.

- **ID** — module prefix and number, e.g. `LV-07`. `V` = valid path, `I` = invalid path.
- **Rule** — the rule being tested, in one line.
- **Login** — which account to sign in as.
- **Steps** — numbered UI actions. Labels are written exactly as they appear in the English UI.
- **Expect** — the visible result.
- **Blocked by** — for invalid cases: `UI` or `Server`.
  - `UI` means the control is hidden, disabled, or the submit button stays greyed out. The test passes if you cannot send the request at all.
  - `Server` means the UI lets the request through and the server refuses. Expect a red toast whose title is the dialog's "error" title and whose description is the server message quoted in the case. The error code is given in parentheses for developers.
- **Result** — `[ ]` pass / `[ ]` fail / notes.

The toast layout (title = dialog error title, description = server message) is assumed from the
code; confirm it on the first server error you hit and correct this line if it differs.

## Things the guide treats as suspected defects (verify while testing)

1. **Multi-day approved leave shows `Absent` on days 2+.** The code materializes one `On leave` row, on the first workday only (`LeaveAttendanceSync.MaterializeAsync`). The attendance calendar then marks any employed employee with no row on a workday as `Absent`. Cases `LV-02` and `ATT-08` check this.
2. **Any punch is editable, not only manual ones.** `UpdateAttendanceLogHandler` does not check the punch source despite the method name `UpdateManualEntry`. Case `ATT-14`.
3. **Holidays can be declared inside a closed Gaji Premi period.** Only closed Potongan Cuti months block them. Harmless today because OT stores `IsDayOff` at creation. Case `XM-03`.

## Rule reference

Rule IDs in the cases (`C`, `A`, `L`, `O`, `G`, `P`) point to the curated rulebook from the 2026-10-13
review. Each case restates its rule in one line, so the guide stands on its own.
