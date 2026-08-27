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
2. **Reuse `AccountRules.CanManage` directly — no new `EmployeeRules` class.** The rule is already role-generic (`ClaimsPrincipal` × `EmployeeRole`), it just happens to live in `AccountContracts.cs`. Duplicating it into an Employees-vertical twin buys nothing and gives us two copies to keep in sync.
3. `UpdateEmployeeEndpoint` enforces, before dispatching the command (`SendForbiddenAsync` on any failure):
   - `CanManage(User, employee.Role)` — the target's **current** role.
   - `CanManage(User, req.Role)` — the **requested** role. Without this, a Manager could promote a Staff member to Owner/Manager in the same call, escaping their own scope.
   - `CanManage(User, newParent.Role)` when `ParentId` changes. Net effect: **only Owner can reparent.** A Manager can only pass `CanManage` for a Staff parent, and `MaxDepth = 2` already makes Staff-under-Staff impossible — so every Manager reparent attempt is denied. That's the intended policy: org-chart structure is Owner's to change.
   - Manager sending **any** wage field → forbidden (see 5).
4. **Salary read redaction.** Open `ListEmployeesEndpoint`/`GetEmployeeEndpoint` to `Owner,Manager`; Manager sees **all rows** (org visibility) with `MonthlyWageAmount`/`MonthlyWageCurrency`/`EffectiveSalaryFrom` nulled out. Redaction lives in `EmployeeResponseMapper.ToResponse(result, caller)` — one place, all four endpoints route through it.
5. **Salary write.** `MonthlyWageAmount`/`EffectiveSalaryFrom` become **nullable** on `UpdateEmployeeRouteRequest` + `UpdateEmployeeCommand`; `null` = "leave unchanged". Manager's form omits them entirely (it never received them), so the check is simply *"caller is not Owner AND a wage field is present → forbidden"* — no need to load and diff the stored wage.
6. Replace both `// TODO` comments with a doc comment referencing `AccountRules.CanManage`.
7. Tests: `AccountRules.CanManage` truth table (Owner/Manager/Staff caller × Owner/Manager/Staff target) as a unit test. Endpoint wiring stays uncovered — the repo has no HTTP-level test infra and standing the first one up is out of scope for this branch.

## Frontend plan
- `Sidebar.tsx`: no change needed for the Employees nav item itself (see branch 03 for the actual sidebar bug found), but the **Edit** and **Delete** actions inside `EmployeeTable`/`app/employees/[id]/page.tsx` need role-gating that doesn't exist today:
  - Edit button/action: visible to Owner and Manager, hidden for Staff.
  - Delete/Terminate button/action: visible to Owner only, hidden for Manager and Staff.
  - Read role from `useAuthStore` (`user.roles`), same source `Sidebar.tsx` already uses for nav filtering.
- `EmployeeTable`: wage column hidden entirely for non-Owner (the API sends `null` anyway).
- `EmployeeForm`: wage + effective-from fields absent for non-Owner (not disabled-and-visible), and the **Role** select locked to `Staff` for a Manager — matching the backend rule, so a Manager never submits into a guaranteed 403.
- **ParentId change confirmation**: changing "Reporting to" prompts a confirm/cancel before submit, for **any** editor including Owner — reparenting is a structural change regardless of who does it. Termination already has its own confirm dialog (`DeleteEmployeeDialog`), so no new work there.

## Status
Decisions locked — implementing.
