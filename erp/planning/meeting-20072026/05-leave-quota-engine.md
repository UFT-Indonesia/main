# Branch: Leave Quota Engine

## Decided

### Probation anchor
- **Probation end is a date, not an event.** `Employee` gains `HireDate` (nullable `LocalDate`) and
  `ProbationEndsOnOverride` (nullable `LocalDate`). Effective end is
  `ProbationEndsOnOverride ?? HireDate?.PlusMonths(3)` — a 3-month default from entry, editable by
  the Owner. Both null means no probation at all.

  > **Supersedes the previous decision in this file.** It previously locked "manually set, Owner
  > only, no fixed duration, no formula from `HireDate`". That is reversed: the default is now
  > computed from `HireDate`, the Owner edits it, and a Manager can request an extension. The
  > override column is what keeps this honest — an Owner edit writes the override, so a later
  > correction to `HireDate` never silently moves a date someone deliberately set.

- **Legacy rows are grandfathered.** Existing employees get `HireDate = NULL`, meaning "hired before
  this feature": never on probation, full entitlement. No sentinel dates in the database. `HireDate`
  is required on every new `CreateEmployee`.

### Entitlement
- Anchor is the probation end month, not the hire month. Graduation month itself is dropped:
  ```
  end is null            → 12          // never on probation
  end.Year  <  year      → 12          // confirmed in an earlier year
  end.Year  >  year      → 0           // still on probation all year
  otherwise              → 12 - end.Month
  ```
  Ends 2026-06-01 or 2026-06-30 → 6. Ends 2026-12-15 → 0. Ends 2027-01-01 → 11 in 2027.
  Known wart: confirmation on the 1st of a month loses that month, capped at 1 day, once.
- **No carryover.** Unused days die on 31 December. Nothing is stored — used-days is always derived.
- **Probation blocks Annual only.** Sick, Permission and Unpaid stay filable during probation
  (uncapped by default). A probationer with flu can still record the absence.
- **Owner is exempt** from both probation and quota, mirroring `LeaveRules.IsAutoApproved`: an
  Owner's leave is a calendar label, and there is nobody to enforce a cap on their behalf.
- **Cross-year requests split by calendar year.** Each workday charges the year it falls in, and the
  request must fit both years' remaining quota. This retires the `ponytail:` note on
  `ApprovedLeaveForYearSpec` (`LeaveSpecs.cs:47`).

### Per-employee overrides
- New `EmployeeLeaveQuotas` table, PK `(EmployeeId, LeaveType)`, column `EntitledDays`.
- **Permanent until cleared** — no year dimension. A cap that silently lapses each January fails
  open to uncapped, which is the dangerous direction.
- `EntitledDays = 0` is legal and means "none of this type". Absence of a row means uncapped for
  non-Annual, or the computed formula for Annual.
- **Probation beats an override; an override skips proration.** On probation, Annual is 0 whatever
  the override says. Once confirmed, an override is the whole-year figure — no fractional days.
  ```csharp
  int? Entitled(LeaveType type, Employee e, int year, LocalDate today)
  {
      if (e.Role == EmployeeRole.Owner) return null;      // exempt
      var ovr = e.QuotaOverride(type);                    // null when no row
      if (type != LeaveType.Annual) return ovr;           // null = uncapped
      if (e.IsOnProbation(today)) return 0;
      return ovr ?? AnnualEntitlement(e.ProbationEndsOn, year);
  }
  ```

### Probation extension workflow
- A Manager may ask for more time for **their own direct Staff** (`OrgScope.IsDirectStaffOf`), and
  only while that employee is still on probation. An **Owner never files** — they have the direct
  edit, so a request from them would be a note to themselves. **Any Owner** may decide.
- The request carries an **explicit later date** (`proposedEndsOn`), rejected unless later than the
  current effective end. Approval writes that exact date to `ProbationEndsOnOverride` — no
  re-derivation at decision time, so a shifting base cannot change what was agreed.
- **One pending request per employee**, mirroring `PendingLeaveForEmployeeSpec`. No cap on total
  extensions or total probation length: every one needs an Owner's approval, and the human is the
  control.
- **A stale request cannot be approved.** If probation ends before the Owner decides, approve is
  refused with `probation.already_confirmed` and the Owner is pointed at the direct edit. This
  prevents retroactively un-confirming someone whose leave was already approved in the gap.

### Enforcement
- Checked **twice**: in `CreateLeaveRequestHandler` (fast feedback) and in the approve path
  (authoritative — an override lowered mid-flight must not be approvable past).
- **Used days = sum of approved workdays** in that year, per type. Pending days do not reserve.
  Cancellation frees days automatically because nothing is stored.
- **Overflow is rejected outright**, naming the number: `leave.quota_exceeded` —
  *"Only 3 Annual days remain for 2026; this request is 5 workdays."* No auto-split into Unpaid;
  that can be its own branch later without undoing anything here.
- `remaining` may be negative for employees who exceeded a cap set after the fact. Reported raw
  rather than clamped — the Owner should see the overage.

## Current state
- No `LeaveQuota`/`LeaveBalance` concept exists anywhere in the codebase.
- No `HireDate` or probation field exists on `Employee`. `EffectiveSalaryFrom` is the nearest date
  but moves on every `ChangeSalary`, so it cannot double as an anchor.
- `ApprovedWorkdaysThisYear` already ships: computed in `ListLeaveRequestsHandler.cs:58-66`,
  surfaced at `leave/page.tsx:184` and `leave-dialogs.tsx:270`, typed at `types.ts:313`. It is an
  **all-types-combined** informational tally with nothing enforced against it.
- `LeaveRules.CanReadBalance` (`LeaveRules.cs:60`) already defines who may read whose balance —
  Owner reads all, Manager reads any non-Owner, Staff read their own. Reuse as-is.
- `EmployeeAuditLog` + the domain-event pipeline (branch 02) are live: new `Employee` events get an
  audit trail for free via `EmployeeAuditLogWriter`.
- `DecideLeaveRequestEndpointBase` (`DecideLeaveRequestEndpoints.cs`) is the approve/deny/cancel
  pattern to copy for probation decisions.
- Only two `Employee.Create` call sites: `CreateEmployeeHandler.cs:44` and `IdentitySeeder.cs:94`.

## API surface

| Verb | Route | Who | Purpose |
|---|---|---|---|
| `PUT` | `/api/employees/{id:guid}` | Owner, Manager | existing; gains `hireDate` (Owner-only field, guarded like wage) |
| `PUT` | `/api/employees/{id:guid}/probation` | Owner | set/clear `ProbationEndsOnOverride`; `{ endsOn }`, null clears |
| `PUT` | `/api/employees/{id:guid}/quota` | Owner | upsert/clear one override; `{ type, days }`, null days clears |
| `POST` | `/api/probation` | Manager | file extension; `{ employeeId, proposedEndsOn, reason }` |
| `GET` | `/api/probation` | Owner, Manager | list, `?status=Pending` |
| `POST` | `/api/probation/{id:guid}/approve` | Owner | writes the override |
| `POST` | `/api/probation/{id:guid}/deny` | Owner | `{ note }` |
| `POST` | `/api/probation/{id:guid}/cancel` | filing Manager | withdraw |
| `GET` | `/api/leave/balance` | per `CanReadBalance` | `?employeeId=&year=`, all four types |

`/api/probation` mirrors `/api/leave`: domain noun, "request" implied, decisions as
`POST /{id}/action`.

### Leave response shape
`approvedWorkdaysThisYear` **keeps its meaning** (total days away this year, all types) and its
existing column. A new nullable `quota` block carries what is actually enforced, for that row's own
type:
```json
{
  "approvedWorkdaysThisYear": 9,
  "quota": { "type": "Annual", "entitledDays": 12, "usedDays": 4, "remainingDays": 8 }
}
```
`entitledDays`/`remainingDays` null = uncapped. `quota` null = caller may not read this balance.
One consequence of per-year splitting: the tally becomes "workdays falling in this year" rather than
"`WorkdayCount` of requests starting this year" — identical except across New Year.

### Balance response
```json
{ "employeeId": "…", "year": 2026, "onProbation": false, "probationEndsOn": "2026-06-01",
  "quotas": [ { "type": "Annual", "entitled": 12, "used": 4, "remaining": 8 },
              { "type": "Sick", "entitled": 10, "used": 5, "remaining": 5 },
              { "type": "Permission", "entitled": null, "used": 0, "remaining": null },
              { "type": "Unpaid", "entitled": null, "used": 0, "remaining": null } ] }
```

## Plan
1. **Migration A** — `Employee.HireDate`, `Employee.ProbationEndsOnOverride` (both nullable, no
   backfill), plus `EmployeeLeaveQuotas (EmployeeId, LeaveType, EntitledDays)`.
2. **Migration B** — `ProbationExtensionRequests`: `EmployeeId`, `ProposedEndsOn`, `Reason`,
   `Status`, `RequestedByUserId/AtUtc`, `DecidedByUserId/Name/AtUtc`, `DecisionNote`.
3. **`Employee`** — the two fields, computed `ProbationEndsOn`, `IsOnProbation(today)`,
   `SetHireDate`, `OverrideProbationEnd`, quota override accessor. Each mutator raises a domain
   event (`EmployeeHireDateChanged`, `EmployeeProbationEndChanged`, `EmployeeLeaveQuotaChanged`) so
   the branch-02 audit writer picks them up; add the matching entries to
   `audit-log-event-types.ts`. `HireDate` reads follow `EmployeeVisibility.CanReadDetails`.
4. **`ProbationExtensionRequest` aggregate** — lifecycle copied from `LeaveRequest`
   (`Pending → Approved | Denied | Cancelled`), same decision-stamping helpers.
5. **`LeaveQuota`** — the pure entitlement function above, plus a per-type used-days rollup. Change
   `ApprovedLeaveForYearSpec` from "starts in year" to "overlaps year" and sum via the existing
   `LeaveRequest.Workdays(start, end)` enumerable, filtered by `d.Year == year`.
6. **Endpoints** — the table above. Probation decisions reuse the
   `DecideLeaveRequestEndpointBase` pattern: one abstract base, three thin subclasses.
7. **Enforcement** — one shared check called from `CreateLeaveRequestHandler` and the approve path.
8. **Frontend**
   - `app/probation/page.tsx` + `components/probation/probation-dialogs.tsx`, modelled on
     `app/leave/page.tsx`. Sidebar entry role-gated `['Owner', 'Manager']`.
   - Employee detail: hire date, probation end, "Request extension" (Manager, own Staff, on
     probation), Owner's date edit, quota override form.
   - `employee-form.tsx`: `hireDate` field, required.
   - Leave create dialog: fetch `/api/leave/balance` on employee pick, show remaining beside the
     type select.
   - `en.json`/`id.json` for all of the above.
9. **Tests** — unit tests for the entitlement function (probation gate, the four formula branches,
   override precedence, Owner exemption) and for the cross-year workday split. These are pure
   functions; no HTTP-level infra needed.

## Flagged, not blocking
Indonesian Manpower Law (UU 13/2003 art. 60) caps *masa percobaan* at 3 months for a PKWTT and does
not permit extension. Treating this field purely as a leave-entitlement anchor is an internal
bookkeeping choice and fine; treating it as employment-law probation status and extending it carries
real exposure. Owner's call — the workflow does not enforce a ceiling.

## Deferred
- Auto-splitting an over-quota request into Annual + Unpaid (standard Indonesian practice, its own
  branch — it breaks one-pending-per-employee and needs linked rows).
- Any cap on extension count or total probation length.
- A dashboard reminder for employees approaching their probation end.

## Built
Shipped as one branch. Deviations from the plan above, all deliberate:

- **One migration, not two.** `20260825061559_AddProbationAndLeaveQuotas` carries both the
  `Employee` columns and both new tables. Splitting them bought nothing — they deploy together.
- **`EmployeeLeaveQuotas` is an owned collection on `Employee`**, not a separate aggregate. EF
  loads it with every employee, which keeps `LeaveQuota.Entitled` a pure function of one
  already-loaded object instead of a second query at every call site.
- **Probation blocking Annual gets its own error code**, `leave.probation_annual`, rather than
  surfacing as `leave.quota_exceeded` with 0 remaining. "You have no annual leave until 1 Sep" is
  a different message from "you have 2 days left".
- **`ProbationExtensionRequest` carries `CurrentEndsOn`**, a snapshot of the end date at filing
  time, so the deciding Owner sees the delta being asked for without recomputing it.
- **Denying a stale request is allowed**; only approving is refused with
  `probation.already_confirmed`. Denying just closes out a request nobody can act on any more.
- **One quota shape everywhere**: `{ type, entitledDays, usedDays, remainingDays }` on both the
  leave row and the balance response, rather than the two spellings sketched above.
- **The `quota` block is gated on `CanReadDetails` as well as `CanReadBalance`**, because it names
  the leave type — which is redacted from callers without standing to read it.
- **"Today" is Jakarta, not UTC.** A UTC date rolls over at 07:00 local, which would confirm
  probation and roll the leave year on the wrong day. `DisplayZone` (`Erp.UseCases/Common`) holds
  the zone; the audit-log filter's private copy of it was folded into the same constant.
- **`Employee.Create` takes `hireDate` as an optional argument.** Required at the API contract
  (`employee.hire_date_required`), optional in the domain so `IdentitySeeder` can seed an Owner —
  who is exempt from probation anyway — without inventing a date.

Tests: 38 new unit tests (`LeaveQuotaTests`, `LeaveQuotaEnforcementTests`,
`ProbationExtensionHandlersTests`); 369 pass. `ProbationAuthorizationTests` was added to the
integration suite but **has not been run** — Docker was unavailable, so it carries the same
unverified note as `LeaveAuthorizationTests`.
