# Branch: RBS Permission Enforcement (Employee Update/Delete)

## Decided
- **Owner**: Update + Delete (terminate) rights on any employee.
- **Manager**: Update rights only — no Delete/Terminate rights at all.
- **Manager's Update scope: Staff only.** Manager cannot update other Managers or the Owner — this mirrors `AccountRules.CanManage` exactly (Owner → any; Manager → Staff-role only).
- Frontend: Edit/Update UI accessible to Owner & Manager only (Staff sees no edit controls anywhere in the Employees section). Delete/Terminate action visible to Owner only.

## Current state
- `UpdateEmployeeEndpoint` / `DeleteEmployeeEndpoint`: both `[Authorize(Roles = "Owner")]` today, with a stale `// TODO: Enforce RBS permission check` comment (the attribute already blocks Manager/Staff — the gap is that it's a blanket gate, not a scoped one).
- Precedent already shipped: `AccountRules.CanManage(caller, targetRole)` in `Erp.Web/Endpoints/Accounts/AccountContracts.cs` — Owner manages any account, Manager manages Staff-role accounts only.
- **Found issue**: `ListEmployeesEndpoint` and `GetEmployeeEndpoint` are *also* `[Authorize(Roles = "Owner")]` only. Even once Manager gets Update rights, Manager still can't list or open an employee record today — there's nothing to click "Edit" on. And this isn't an oversight: `ProvisionCandidatesEndpoint`'s own doc comment says it "exists so Managers can pick an employee without access to the (salary-bearing, Owner-only) employees list" — i.e. Manager is deliberately kept off the full list because it carries `MonthlyWage`. Opening `ListEmployeesEndpoint`/`GetEmployeeEndpoint` to Manager as-is would leak salary data that was explicitly kept from them elsewhere.

## Plan
1. Split the Update/Delete authorization:
   - `[Authorize(Roles = "Owner,Manager")]` on `UpdateEmployeeEndpoint`.
   - Keep `[Authorize(Roles = "Owner")]` on `DeleteEmployeeEndpoint` (per your answer — no Manager delete rights).
2. Add `EmployeeRules.CanManage(caller, targetRole)` (mirrors `AccountRules.CanManage`: `Owner.IsInRole` OR `Manager.IsInRole && targetRole == Staff`) for use inside `UpdateEmployeeEndpoint` — enforce before dispatching the command; `SendForbiddenAsync` on failure.
3. Give Manager a way to actually reach the Update form without exposing salary. Recommendation (not asked as an open question — low-stakes implementation detail): redact `MonthlyWage`/`EffectiveSalaryFrom` from the list/detail DTO when the caller is a Manager, rather than standing up a second parallel endpoint set. This is consistent with the existing `ProvisionCandidatesEndpoint` precedent (Manager already only ever sees Staff, minus salary, there) and avoids duplicating list/detail logic.
4. Replace both `// TODO` comments with a short doc comment referencing `EmployeeRules`/`AccountRules`.
5. Tests: Owner update/delete-anyone; Manager update-only (forbidden on delete regardless of target); Staff no access.

## Frontend plan
- `Sidebar.tsx`: no change needed for the Employees nav item itself (see branch 03 for the actual sidebar bug found), but the **Edit** and **Delete** actions inside `EmployeeTable`/`app/employees/[id]/page.tsx` need role-gating that doesn't exist today:
  - Edit button/action: visible to Owner and Manager, hidden for Staff.
  - Delete/Terminate button/action: visible to Owner only, hidden for Manager and Staff.
  - Read role from `useAuthStore` (`user.roles`), same source `Sidebar.tsx` already uses for nav filtering.
- If the salary-redaction approach from step 3 is chosen: `EmployeeForm`/`EmployeeTable` need a variant that hides wage/salary fields entirely when the logged-in user is a Manager (fields simply absent, not disabled-and-visible).

## Status
Decisions locked — ready to implement once branch order/priority is set.
