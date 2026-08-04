# Branch: Leave Quota Engine

## Decided
- The anchor is the **probation graduation month**, not the hire month. Example: employee graduates probation in June → that year's `LeaveQuota` is 6 (remaining months of the year after graduation, 1 month = 1 day). Formula:
  ```
  EntitledDays(graduationYear) = 12 - graduationMonth   // e.g. June (6) → 12 - 6 = 6
  EntitledDays(subsequentFullYears) = 12
  ```
  (Before probation is graduated, entitlement is 0 — no quota accrues during probation itself.)
- **Probation graduation is manually set, Owner only** (not Manager). There's no fixed duration and no per-employee configured length — it's a direct action: the Owner marks a specific employee as having graduated probation, on a date of their choosing. No formula computes it from `HireDate`.

  My take on this, since you asked: manual-by-Owner is the right call, and better than either a uniform fixed duration or a per-employee-configured duration. Probation "graduating" in most orgs is a judgment call tied to a performance review, not a calendar date — a fixed-duration formula would assume every review concludes exactly on schedule, which won't hold in practice (reviews slip, get extended, etc.). Manual confirmation also naturally handles edge cases (extended probation, early confirmation) without needing special-case logic. The only cost is it's one more thing Owner has to remember to do per employee — worth a dashboard reminder/flag later (e.g. "N employees still on probation past their expected date") once `HireDate` exists, but that's a nice-to-have, not a blocker.
- **No carryover.** If an employee has, say, 3 days left when the year ends, those 3 are lost — the new year starts fresh at that year's full entitled amount (12, or the graduation-year prorated amount if it's their first year).
- **Non-Annual types (Sick/Permission/Unpaid) are uncapped by default** (matches general Indonesian practice, per your note), **but configurable per employee by the Owner** — i.e. the quota model needs to support an optional override cap on any leave type per employee, defaulting to unlimited for everything except Annual.

## Current state
- No `LeaveQuota`/`LeaveBalance` concept exists anywhere in the codebase.
- No `HireDate` or probation-related field exists on `Employee` at all — `EffectiveSalaryFrom` is the closest date field but is semantically about salary and moves on every raise (`ChangeSalary`), so it cannot double as a hire/probation anchor.
- `CreateLeaveRequestHandler` already computes and surfaces `ApprovedWorkdaysThisYear` (an informational running tally of approved Mon–Fri days per employee per calendar year, shown in the `LeaveRequest` API response) — but this is **purely informational today, not enforced**. No cap, no probation gate, nothing blocks a request once the tally is high. This is a useful head start: the yearly-tally plumbing already exists, it just needs a limit wired to it.

## What needs to exist before this can be built
1. `Employee.ProbationGraduatedOn` (nullable `LocalDate`) — null while still on probation (entitlement 0), set once via an Owner-only action. (`HireDate` itself isn't strictly required for the quota formula since graduation is manual, not computed — but worth adding alongside for display/tenure purposes if you want it; not a hard blocker either way, flag if you want it included now or later.)
2. A per-employee, per-leave-type quota override: entitled-days cap for a given `LeaveType`, nullable = uncapped. Annual's cap is computed (not stored) via the formula unless the Owner overrides it; Sick/Permission/Unpaid default to null (uncapped) unless the Owner sets one.
3. Used-days tracking per employee per year per leave type — `ApprovedWorkdaysThisYear` already exists as a running tally in `LeaveRequestResult` (informational only today); extend/reuse this, scoped per `LeaveType`, and reset naturally each calendar year (no carryover, so this is just "sum of approved workdays where `Year(StartDate) == currentYear`" — no stored balance to roll over).
4. `CreateLeaveRequestHandler`: check remaining quota before allowing a request to succeed for any capped type (Annual always; others only if the Owner set an override).

## Plan
1. Add `ProbationGraduatedOn` to `Employee` (migration) + a new Owner-only endpoint/action to set it (e.g. `POST /employees/{id}/graduate-probation`).
2. Add the optional per-employee-per-type quota override (new small table or nullable columns — table is cleaner since it's sparse/optional), plus an Owner-only endpoint to set/clear an override.
3. Build the entitlement calculation as a pure function (easy to unit test): given `ProbationGraduatedOn` + calendar year + any override, return entitled days (0 if not yet graduated, prorated in the graduation year, 12 for full subsequent years, or the override value if set).
4. Wire enforcement into `CreateLeaveRequestHandler`: reject if entitled-days is 0 (still on probation) or if the request would push `ApprovedWorkdaysThisYear` past the entitled amount for that type.
5. Surface remaining quota in the frontend leave request form (so Owner/Manager — and later Staff, once branch 03 ships self-service — see the balance before submitting), and add the "graduate probation" action + optional per-employee override UI somewhere in the Employee detail page (Owner-only).

## Status
Decisions locked — ready to implement once branch order/priority is set. Only remaining implementation detail (not a product question): whether to add `HireDate` now alongside `ProbationGraduatedOn` for display/tenure purposes, or defer it — doesn't block anything above either way.
