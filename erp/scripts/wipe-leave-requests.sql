-- Wipes every leave request, clean, so the sick-leave attachment work starts from an empty table.
--
-- Leave does not live only in "LeaveRequests": approving one materializes an attendance row and
-- flips the employee's status badge. Deleting the requests alone would leave both behind, so this
-- unwinds them in the same transaction, in dependency order.
--
-- NOT touched: "EmployeeLeaveQuotas" (per-employee entitlement overrides — configuration, not
-- leave history) and any attendance day that carries a real punch (genuine attendance; it only
-- loses its link to the leave that no longer exists).
--
-- Run it yourself:
--   psql "postgresql://uft:uftdev@localhost:5432/ufterp" -f scripts/wipe-leave-requests.sql

BEGIN;

-- What is about to change.
SELECT
    (SELECT count(*) FROM "LeaveRequests")                                             AS leave_requests_to_delete,
    (SELECT count(*) FROM "AttendanceDays"
      WHERE leave_request_id IS NOT NULL AND tap_in_utc IS NULL)                       AS leave_only_days_to_delete,
    (SELECT count(*) FROM "AttendanceDays"
      WHERE leave_request_id IS NOT NULL AND tap_in_utc IS NOT NULL)                   AS worked_days_to_unlink,
    (SELECT count(*) FROM "Employees" WHERE status = 'OnLeave')                        AS employees_to_reactivate;

-- 1. Days the leave created on its own go away entirely — no punches, no reason to exist.
DELETE FROM "AttendanceDays"
WHERE leave_request_id IS NOT NULL
  AND tap_in_utc IS NULL;

-- 2. Days that collected a real punch are real attendance and stay; they just lose the dead link.
UPDATE "AttendanceDays"
SET leave_request_id = NULL
WHERE leave_request_id IS NOT NULL;

-- 3. Nobody is on leave once there are no leave requests. Terminated employees stay terminated.
UPDATE "Employees"
SET status = 'Active'
WHERE status = 'OnLeave';

-- 4. The requests themselves.
DELETE FROM "LeaveRequests";

-- Expect: 0, 0, 0.
SELECT
    (SELECT count(*) FROM "LeaveRequests")                          AS remaining_leave_requests,
    (SELECT count(*) FROM "AttendanceDays"
      WHERE leave_request_id IS NOT NULL)                           AS remaining_linked_days,
    (SELECT count(*) FROM "Employees" WHERE status = 'OnLeave')     AS remaining_on_leave;

COMMIT;
