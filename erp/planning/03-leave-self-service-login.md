# Branch: Leave Self-Service + Login/Nav Fix

## Current state
Account provisioning already works end-to-end: `Erp.Web/Endpoints/Accounts/` links an `ApplicationUser` to an `Employee` via `ApplicationUser.EmployeeId`, any role (including Staff) can be given login credentials, `MustChangePassword` is enforced, and `/change-password` exists on the frontend and is routed to correctly after login. So the account/login mechanics themselves are not broken.

**What's actually left**:
1. `CreateLeaveRequestEndpoint` is `[Authorize(Roles = "Owner,Manager")]` — a Staff employee with a working login still cannot file their own leave request, only have one filed on their behalf. `LeaveRequest.cs`'s doc comment ("no self-service yet — employees have no login accounts") is now stale since accounts do exist.
2. **Likely cause of the demo problem**: `Sidebar.tsx`'s nav list shows the **Employees** item to every logged-in user with no role restriction (`{ href: '/employees', labelKey: 'employees', icon: Users }` — no `roles` array), but the backing endpoints (`ListEmployeesEndpoint`, `GetEmployeeEndpoint`) are `[Authorize(Roles = "Owner")]` only. A Manager or Staff account logging in sees "Employees" in the sidebar, clicks it, and gets a 403 from the API — a broken-looking menu item. This is a frontend/backend mismatch, not a backend gap.

## Decided
- **Cancel**: Staff can cancel their own leave request (matches the existing `Cancel` semantics already on `LeaveRequest` — allowed while Pending or Approved).
- **Reschedule (new capability)**: Staff can also *update* their own request, but **only the dates** — nothing else (not type, not reason) — so they can revise a leave window without a full cancel-and-resubmit round trip.
  - **Conflict to resolve in code**: `LeaveRequest.cs`'s own doc comment currently states the lifecycle is deliberately edit-free — *"Denied/Cancelled are terminal; wrong dates are fixed by cancel + resubmit, never edit."* This branch directly changes that rule for the self-service case. Plan: add a new domain method, e.g. `LeaveRequest.Reschedule(LocalDate newStart, LocalDate newEnd)`, restricted to `Status == Pending` only (an already-Approved request should still require cancel + resubmit, since a decision was already made against the old dates), re-running the same date-range/workday validation `Create` already does, and recomputing `WorkdayCount`. Update the doc comment once this ships so it no longer contradicts the code.
  - Reschedule is for the employee's own Pending request only — Owner/Manager-initiated changes to *other* employees' requests still follow cancel + resubmit (no change to their existing flow).

## Plan
**Self-service leave**:
1. New endpoint (or relaxed existing one) letting an authenticated user submit a `LeaveRequest` for *their own* linked employee — resolved from `ApplicationUser.EmployeeId` via the JWT claim, never from an arbitrary `EmployeeId` in the request body.
2. New self-cancel endpoint (or relaxed existing `CancelLeaveRequestEndpoint`) scoped the same way — caller can only cancel a request where `EmployeeId` matches their own linked employee.
3. New `LeaveRequest.Reschedule(newStart, newEnd)` domain method (Pending only) + a self-service "update dates" endpoint, same own-employee scoping.
4. Update the stale doc comment in `LeaveRequest.cs` to reflect the new (restricted) reschedule capability.

**Login/nav fix**:
1. Give `Sidebar.tsx`'s `Employees` nav item a `roles: ['Owner']` (or `['Owner', 'Manager']`, once branch 01 lands and Manager gets some employee access) restriction so it's never shown to a role that will 403 on it.
2. Audit the rest of `NAV` the same way — cross-check every nav item's `roles` array against its backing endpoint's actual `[Authorize]` attribute, not just against what "seems right." (`Attendance` has no role restriction today and its backing endpoints are genuinely `[Authorize]`-only i.e. open to any authenticated role, so that one's already consistent.)
3. Once self-service leave exists, add a nav entry for Staff to reach their own leave requests (today `/leave` is Owner/Manager-only in the sidebar and the page itself likely assumes an admin view — check `app/leave/page.tsx` for whether it needs a Staff-facing variant or can branch on role).

## Frontend
Covered above — this branch is arguably frontend-heavier than backend: the self-service endpoint is a small backend addition, but the nav/role audit and the Staff-facing leave view are real frontend work.

## Status
Decisions locked — ready to implement once branch order/priority is set.
