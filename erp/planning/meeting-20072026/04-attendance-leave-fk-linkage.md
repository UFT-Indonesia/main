# Branch: Attendance & Leave — FK Linkage (Table/Status/Export)

## Decided
Separate branch, tied to leave approvals (per your confirmation).

## Current state
- `AttendanceDay.Status` only distinguishes `Complete`/`Incomplete`, derived purely from punches.
- `EmployeeStatus.OnLeave` already exists as an enum value and is fully wired through the frontend (filters in `constants.ts`, badge styling in `employee-table.tsx`, translations in `en.json`/`id.json`) — but **nothing in the backend ever sets it**. `LeaveRequest.Approve` / `.Deny` / `.Cancel` raise no domain event at all today, so nothing downstream reacts to a decision.
- No relational link exists between an approved `LeaveRequest` and the `AttendanceDay` rows it covers.

## Plan
1. `LeaveRequest.Approve` and `.Cancel` raise new domain events (`LeaveRequestApproved`, `LeaveRequestCancelled`) — currently raise none.
2. New handler reacts to `LeaveRequestApproved`:
   - Sets `Employee.Status = OnLeave` for the request's date range, with the cancel/expiry path reverting to `Active`, and/or
   - Stamps covered `AttendanceDay` rows with a nullable `LeaveRequestId` foreign key (migration required), so the table, status badge, and CSV export can show "On Leave" backed by a real relation instead of a derived string.
3. Update `AttendanceDayTable`, `attendance-day-table.tsx` status badge, and `ExportAttendanceDaysHandler`/CSV output to surface the linked leave info once the FK exists.

## Open questions (already flagged, not in this pass's 4 — revisit once branch starts)
1. Should `Employee.Status` flip to `OnLeave` for the whole range (simple, but a manual attendance correction wouldn't un-flip it early), or should the FK live strictly at the `AttendanceDay` grain and `Employee.Status` drop `OnLeave` entirely?
2. Multi-day leave crossing a weekend — materialize `AttendanceDay` rows for weekend days under leave, or only workdays (matching `LeaveRequest.CountWorkdays`, which already skips Sat/Sun)?
3. Should the export gain a "Leave type / reason" column when a row is FK-linked?

## Frontend
Table/status badge/export changes listed above — real frontend work, not just backend.
