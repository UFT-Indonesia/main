# Grill Session Summary — 01 Client Enhancements (2026-08-26)

Working doc from grill-me sessions on the five enhancements raised in the client meeting of **2026-08-26**. Records what got resolved, what the codebase actually says (several premises were refuted), and where to resume.

Session 1 settled items 1–3. Session 2 (2026-08-27) settled items 4–5 and added item 6 — a timezone bug family found while grilling item 4.

**Nothing here has been implemented — design only. No code, migration, script, or commit made yet.**

| # | Topic | Status |
|---|---|---|
| 1 | Leave types + probation gating | ✅ Settled |
| 2 | `Alasan` (reason) becomes required | ✅ Settled |
| 3 | App name → "ERP UFT" | ✅ Settled |
| 4 | Blocked leave dates in the date pickers | ✅ Settled (session 2) |
| 5 | `Cuti` attendance rows | ✅ Settled (session 2) — **scope reversed** |
| 6 | Timezone correctness (not a client ask — a bug found while grilling 4) | ✅ Settled (session 2) |

Holidays and the configurable working week are parked separately in `GSS02-attendance-holiday-workaround.md`.

---

## 1. Leave types — probation decides Annual vs Unpaid

### Client ask vs. what we decided

Client asked for **only three types: Cuti Tahunan, Sakit, Ijin** — remove "Di luar tanggungan" entirely, frontend and backend.

**Deliberately not implemented as stated.** User overrode it: the type stays, because unpaid leave is a real thing for probationary employees. The client's underlying complaint is read as *"Di luar tanggungan is ambiguous and shouldn't be showing up for regular staff"* — solved by the label rename plus the probation gate below, which still gives every user exactly three options.

### The rule

Each employee sees **exactly three types**, decided by probation status:

| Employee state | Options |
|---|---|
| **On probation** | Izin (`Permission`), Sakit (`Sick`), **Unpaid** |
| **Permanent / graduated** | Izin (`Permission`), Sakit (`Sick`), **Cuti Tahunan** (`Annual`) |

`Annual` and `Unpaid` are mutually exclusive; `Permission` and `Sick` are always available.

The rule keys off **the employee the leave is for**, never the filer. A Manager filing for a probationary Staff member sees the probation set. A Manager who is themselves on probation, filing for themselves, sees the probation set. Role is irrelevant — `ProbationEndsOn` decides.

**Owner falls out for free.** `Employee.IsOnProbation` (`Employee.cs:83`) is `ProbationEndsOn is { } endsOn && today < endsOn`. An Owner is never on probation, so an Owner is rejected for `Unpaid` by the ordinary rule with no special case. Confirmed as intended: Owner is uncapped on *quantity* but still gated on *type*.

⚠️ **This inverts a documented decision.** `LeaveQuota.cs:52` currently reads *"Probation only withholds paid annual leave. Someone on probation with flu still needs to be able to record the absence, so Sick/Permission/Unpaid stay filable throughout."* That comment becomes false and must be rewritten.

### Label rename

`type.Unpaid`: `"Di luar tanggungan"` → **`"Unpaid"`** in both `id.json:274` and `en.json`. English word in the Indonesian locale, on purpose — the Indonesian phrase is what confused people.

### Which date decides probation — **filing date**

`IsOnProbation(today)` at the moment the request is filed. Chosen over "leave start date" and "whole range inside probation" so there is one probation notion across the module, matching how `LeaveQuota.Entitled` already decides annual entitlement.

### Enforcement site — **create handler only**

The type-eligibility check goes in `CreateLeaveRequestHandler`, **not** in `LeaveQuotaGuard`.

```
if (type == LeaveType.Unpaid && !employee.IsOnProbation(today)) → reject
```

**Why not the shared guard.** `LeaveQuotaGuard.CheckAsync` runs **twice** — at filing and again at approval, with a different `today` each time (its own doc comment: *"the authoritative check: a quota lowered while the request sat pending must not be approvable past"*). Putting the type check there creates a dead-end:

> Budi's probation ends 15 Sep. He files Unpaid on 10 Sep for 20–24 Sep — allowed, saved Pending. Manager approves on 16 Sep → guard re-runs, Budi is no longer on probation → **rejected**. Nobody can approve it, nobody can edit it (`LeaveRequest` is cancel-and-refile only), and the refile would be a different type.

With create-only enforcement that approval succeeds, which is the honest reading of "filing date decides": eligibility was real when the employee exercised it, and a manager's approval delay shouldn't retroactively void it.

**Accepted tradeoff:** a pending request can be approved into a state the create rule would now forbid.

**Known pre-existing hole, left alone:** the Annual side has the same bug in reverse (`LeaveQuotaGuard.cs:30` re-checks `probation_annual` at approval). Not fixed here. Fixing it properly would mean passing the request's own `RequestedAtUtc` into the guard instead of `today` — noted for later, nobody has hit it yet.

`LeaveQuotaGuard`'s Owner early-return (`LeaveQuotaGuard.cs:25`) is untouched and never gets a say, since the new check lives elsewhere.

### Frontend

`CreateLeaveDialog` (`leave-dialogs.tsx`):

- `LEAVE_TYPES` (`:22`) stays a plain const. The **dialog filters it locally** by `balance.data.onProbation` (already returned by `useLeaveBalance`, see `types.ts:379`).
- `EMPTY_FORM.type` (`:70`) becomes `''` — its type widens to `LeaveType | ''`.
- **No employee picked** → type select is **disabled** with a `-` placeholder. (Self-filers are unaffected: the existing effect at `:94` seeds `employeeId` from `self.employeeId`, so balance fetches immediately and a probationary Staff member sees their correct three on open.)
- **Employee changes** → reset `form.type` to `'Permission'` (Izin — legal in both sets, so the reset never lands on something invalid, and it avoids auto-selecting "Unpaid" which one careless submit would file).
- **`canSubmit` (`:83`) must also require `form.type`** — otherwise the empty placeholder is submittable once an employee is picked.
- While `balance.isLoading` → select disabled. Disabled, not hidden; hiding makes the dialog jump.

**Deleted:** the `QuotaHint` probation branch (`:190-191`, `onProbation && quota.type === 'Annual'`) and its `quota.onProbation` i18n key in both locales. Unreachable once Annual can't be selected on probation — dead UI describing a rule that no longer exists.

### Deliberately NOT changed

- `probation-quota-card.tsx:168` still maps the full `LEAVE_TYPES`. Owner-only admin surface; seeing all four is a feature — the Owner can pre-set a quota for the type an employee gets *after* graduating.
- `GetLeaveBalanceHandler.cs:50` still returns all four quotas via `Enum.GetValues<LeaveType>()`.
- `SetLeaveQuotaHandler` still accepts all four types.
- The `"Leave type must be Annual, Sick, Permission, or Unpaid."` error strings stay accurate — all four remain valid enum values.

### Follow-up

`LeaveQuotaTests.cs` has `Unpaid` cases that need a probation-gated counterpart.

---

## 2. `Alasan` becomes required

### The rule

Reason is **required**, validated on the **trimmed** value, **minimum 2 characters**, existing max 500 unchanged. Enforced in `LeaveRequest.Create`, so every caller routes through one check.

Current behaviour being replaced — `LeaveRequest.cs:120` actively normalises whitespace *to null*:
```csharp
var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
```

**Copy the existing precedent verbatim:** `ProbationExtensionRequest` (`ProbationExtensionRequest.cs:58, 101-110`) already does exactly this shape — `string Reason = default!`, trim, `IsNullOrEmpty` throw, then length check.

### Layer-by-layer

| Layer | Now | After |
|---|---|---|
| `LeaveRequest.Reason` (domain) | `string?` | **`string`** |
| `reason` column (`LeaveRequestConfiguration.cs:58`) | nullable | **NOT NULL** |
| `CreateLeaveRequestCommand.Reason` | `string?` | **`string`** |
| `CreateLeaveRequest` contract (`LeaveContracts.cs:12`) | `string?` | **`string` = `default!`** |
| `LeaveRequestResponse.Reason` (`LeaveContracts.cs:88`) | `string?` | **stays `string?`** |
| `types.ts:329` `reason` | `string \| null` | **stays** |
| `types.ts:430` `CreateLeaveRequestBody.reason` | `string \| null` optional | **`reason: string`** |

⚠️ **Do not flip the response side.** `LeaveRequestResult.From` (`LeaveRequestResult.cs:70`) does `Reason = canReadDetails ? request.Reason : null` — same treatment as `Type`, because Sick is health data. The response nullability is **redaction, not absence**, and survives the column becoming NOT NULL.

### Frontend

- Placeholder `"Alasan (opsional)"` (`id.json:297`) / `"Optional reason"` (`en.json:297`) → drop "(opsional)". Replacement text still to pick — the probation module's `"Alasan perlu tambahan waktu"` (`id.json:541`) is the house precedent for a descriptive placeholder.
- `<Input>` at `leave-dialogs.tsx:154-159` gets `minLength={2}`.
- `canSubmit` (`:83`) gains `form.reason.trim().length >= 2`.

### Migration + data cleanup

**Migration does schema only:** `AlterColumn` → `nullable: false`. No backfill, no `UPDATE`, no `DELETE` baked into `Up` — a destructive statement in migration history would silently execute in any future environment.

**A separate one-off SQL script** (run manually against sandbox, before applying the migration) deletes every leave request with a NULL reason, **all statuses**.

⚠️ **Deletion order matters — a plain `DELETE` will fail.** `AttendanceDay.LeaveRequestId` has `OnDelete(DeleteBehavior.Restrict)` (`AttendanceDayConfiguration.cs:72-77`). Approved leave has materialized attendance rows pointing at it (`LeaveAttendanceSync.MaterializeAsync`), so those rows FK-violate. The script must mirror what `LeaveAttendanceSync.ReleaseAsync` (`LeaveAttendanceSync.cs:54-72`) already does on cancellation:

1. `DELETE` attendance days that exist **only** because of the leave (`tap_in_utc IS NULL`)
2. `UPDATE` the rest — `leave_request_id = NULL` — keeping days that collected real punches
3. `DELETE` the leave requests

Deleting an Approved request removes its `OnLeave` days from the attendance table and CSV export. Accepted — sandbox only, and half-cleaned data is worse than clean.

**Target DB** (`apps/api/src/Erp.Web/.env`, the only database that exists):
```
Host=localhost;Port=5432;Database=ufterp;Username=uft;Password=uftdev
```
Note this differs from `docker-compose.yml`, which provisions `erp`/`erp`/`erp_dev`. The script targets the `.env` one.

**Claude will not run any destructive command — the script gets handed over for the user to run.**

---

## 3. App name → "ERP UFT"

Full "Davis" purge (option 3 of 3, chosen deliberately over UI-only).

### Changes

| File | Change |
|---|---|
| `lib/constants.ts:10` | `APP_NAME` → `'ERP UFT'` |
| `app/layout.tsx:12` | `metadata.title` → `'ERP UFT'` |
| `messages/id.json:3`, `en.json:3` | `common.appName` → **deleted** |
| `messages/id.json:29` | `"Sistem ERP internal UFT Davis."` → drop "Davis" |
| `messages/en.json:29` | `"Internal ERP system for UFT Davis."` → drop "Davis" |
| `Program.cs:81`, `:141` | `"UFT Davis ERP API"` → `"UFT ERP API"` |
| `appsettings.json:45` | `FromName: "ERP UFT Davis"` → `"ERP UFT"` (visible: outgoing email sender) |
| `appsettings.json:46` | `FromAddress` → `no-reply@uft.local` |
| `appsettings.json:30-31` | `Issuer`/`Audience` `"erp.uft-davis"` → `"erp.uft"` |
| root `package.json:2` | `"erp-uft-davis"` → `"erp-uft"` |

### Notes

- **`common.appName` is dead code.** Zero `t('...appName')` calls anywhere in `src/`; only the `APP_NAME` const is rendered (sidebar `:77`, login `:64`, change-password `:93`). Deleted rather than translated — three names that can disagree is the actual bug.
- ⚠️ **JWT change forces re-login.** `Issuer`/`Audience` are string-matched during validation. Every access token minted under `erp.uft-davis` fails the moment this ships — everyone signed in gets 401'd once. Accepted knowingly; sandbox only.
- **Repo is NOT renamed.** Remote is `github.com/UFT-Indonesia/main.git` — contains no "Davis". Only the local directory `~/Documents/uft-davis/main` does, and renaming it breaks every absolute path and the IDE's recent-projects for zero gain.

---

## 4. Blocked leave dates in the date pickers — SETTLED

Client showed the **Tambah log manual** dialog's "Tanggal & waktu" field (Chrome's native `datetime-local` popup) and asked for leave dates to be greyed out.

### What happened to GSS01's two refuted premises

**"There is no calendar in this app."** Correct at the time — all 15 date inputs were native, and native inputs honor only `min`/`max`, a contiguous range. Resolved by **adopting a real component**, not by validating around the limitation.

**"The backend is already good."** False for the punch path — `AttendanceLogService.RecordAsync` has no leave check, deliberately (GSS04's *"punch always wins"*). True for the leave path — `CreateLeaveRequestHandler.cs:61-64` already rejects overlaps via `ApprovedLeaveOverlappingSpec`.

### Q4a — scope: **both**, punch date first, then the leave form

`AddManualLogDialog`, `ViewLogDetailsDialog` (it *edits* punch time — a rule that blocks creating a punch on a leave day must block moving one there too), and the leave form's start/end.

### Q4b — **grey the picker, backend stays permissive**

Two paths reach a leave day; only one is closed:

| Path | After this change |
|---|---|
| Physical device (`RecordDeviceLog`) | still records — untouched |
| Manual dialog (`RecordManualLog`) | blocked by the picker |

Story: *the machine records what happened, the form doesn't let you invent something on a day someone was away.* No backend rule change, no contradiction with GSS04's emergency-recall scenario.

**Accepted:** an admin needing to log attendance for someone recalled from leave has no UI path until the leave is cancelled — which is the correct action anyway (`LeaveAttendanceSync.ReleaseAsync` frees the days).

### The component: **react-aria-components**

`react-day-picker` was evaluated and **rejected**. It was never installed — zero hits in `package.json` and `pnpm-lock.yaml` — so there is nothing to uninstall.

```
pnpm add react-aria-components @internationalized/date
pnpm add -D tailwindcss-react-aria-components
```

- `tailwindcss-react-aria-components@2.2.0` declares `peerDependencies: { tailwindcss: "^4.0.0" }` — exact match. Gives `selected:` / `disabled:` / `unavailable:` variants over RAC's data attributes.
- Rejected alternatives: `react-tailwindcss-datepicker` (pulls dayjs alongside the installed date-fns, built for Tailwind 3), `react-time-picker` (own CSS, wrong family), `@mui/x-date-pickers` (brings MUI), `shadcn-datetime-picker` (project has no `components.json`, no Radix — `components/ui/` is hand-rolled).
- Blocked dates use **`isDateUnavailable`**, whose docs example is exactly our shape: `date.compare(interval[0]) >= 0 && date.compare(interval[1]) <= 0`.
- No popover library needed: `combobox.tsx:244` already has the house pattern (`absolute z-50 mt-1 rounded-md border shadow-md`, outside-`mousedown` close at `:58-64`), and `EmployeePicker` proves it renders correctly inside `dialog.tsx`.

### Component assignment — Q4j = **all 15 inputs**

(GSS01 said 13; the correct total is 15.)

| Component | Granularity | Inputs |
|---|---|---|
| `DatePicker` | **`minute`** | `add-manual-log-dialog.tsx:90`, `view-log-details-dialog.tsx:255` — the only two `datetime-local` fields |
| `DateRangePicker` | `day` | `leave-dialogs.tsx:137,145` (2 inputs → 1 control) |
| `DateRangePicker` | `day` | `attendance-day-filters.tsx:68,76`, `attendance-filters.tsx:65,73`, `audit-log-filters.tsx:43,51` — three from/to pairs. **Provisional**, pending the client's opinion |
| `DatePicker` | `day` | `employee-form.tsx:183,191`, `probation-dialogs.tsx:91`, `probation-quota-card.tsx:132`, `delete-employee-dialog.tsx:50` |

### Q4g′ — **no `time-picker.tsx`**

Supersedes two earlier decisions that existed only to work around a date-only library: splitting "Tanggal & waktu" into two fields, and hand-rolling a time picker. `granularity="minute"` renders one **segmented** input (`27` / `08` / `2026` / `09` / `40`) — tab between segments, type or arrow-step. The popover holds only the calendar.

**Expectation set:** segmented typing, *not* a scroll-column or clock-face picker. RAC's standalone `TimeField` is the same segmented idea. A dropdown-style time UI would still be hand-rolled.

### Q4k — the wrapper speaks **strings**, not `CalendarDate`

`<DatePickerField value="2026-08-27" onChange={(s) => …} />`, converting internally with `parseDate()` / `value.toString()`. The datetime variant speaks the UTC ISO string, matching `punchedAtUtc`.

Consequences — **the zod rewrite is avoided**:
- `employee-form.tsx` schemas stay `z.string()` (`:29`, `:34`)
- its `superRefine` comparisons keep working (lexicographic `YYYY-MM-DD` *is* chronological)
- API payloads and `types.ts` untouched
- only edit there: `{...register('hireDate')}` → `<Controller>` — a pattern **already used in that file** (`:4`, `:208`)
- the other sites are plain `useState` strings → one-line swaps

`CalendarDate` stays an internal detail of the component.

### Q4h — timezone: **policy zone, `APP_TIME_ZONE` fallback**

`hideTimeZone` only hides the zone abbreviation in the UI; it does **not** set the zone. The zone comes from the value being a `ZonedDateTime`:

```tsx
now(tz)                                   // prefill
parseAbsolute(log.punchedAtUtc, tz)       // existing punch → Jakarta wall clock
value.toDate().toISOString()              // submit — correct absolute instant
```

**This kills the bug class structurally**: no naked `new Date(string)` survives in the write path, so browser-zone drift becomes inexpressible rather than merely fixed. `date-fns-tz` is therefore *not* needed — `@internationalized/date` covers it.

**Attendance surfaces read `policy.timeZoneId`; everything else reads `APP_TIME_ZONE`.** The policy is authoritative, not decorative:
1. it's Owner-editable free text — `attendance/settings/page.tsx:132`
2. the server buckets days by it — `AttendanceDay.CalendarDate` is derived in that zone
3. it's validated as a real IANA zone — `AttendancePolicy.cs:134-138` rejects anything `Tzdb.GetZoneOrNull` doesn't know

Failure if attendance used the constant instead: Owner switches to `Asia/Makassar` (+08); the picker writes 09:00 as Jakarta, the server files it as a 10:00 Makassar punch, and near midnight it lands on the wrong day.

### The endpoint: `GET /leave/blocked-dates?employeeId=&from=&to=` (Q4d)

Purpose-built and lean, rather than reusing `ListLeaveRequests`.

| Decision | Value |
|---|---|
| Response | **Ranges** — `[{ startDate, endDate }]`. Matches rows 1:1, stays small, is the shape `isDateUnavailable` wants |
| `from`/`to` | **Required.** The picker passes the visible month ± 1 |
| Auth | `[Authorize]`, any signed-in user |
| Statuses | **`Approved` only** — mirrors `ApprovedLeaveOverlappingSpec`, the rule the server actually enforces |
| Weekends inside a range | **Not expanded** — return the raw range (Q4i′) |

**Why not reuse `ListLeaveRequests`:** it caps at `MaxPageSize = 100` and orders `RequestedAtUtc` **descending** (`:118, :120`). At ~20 approved requests a year an employee crosses 100 in about five years, and the *oldest* leave silently falls off the page — those dates quietly stop being greyed. A `from`/`to` window has no such cliff. It also does three round-trips (Count + List + `ApprovedLeaveForYearSpec` rollup) plus an `Include(Employee)` join for data we discard.

**Query weight — not a concern.** One employee at a time, never all. `HasIndex(new { EmployeeId, Status })` (`LeaveRequestConfiguration.cs:99`) matches the filter exactly. Rows are not authority-filtered (`ListLeaveRequestsHandler.cs:130-133`), so there's no permission blocker; the endpoint returns strictly less than the list already does.

**Weekends are not greyed as a general rule** — only as part of a leave span. Leave legitimately spans weekends and people punch on Saturdays.

---

## 5. `Cuti` attendance rows — SETTLED, scope reversed

Client ask: *an attendance row found as "Cuti" should not show the view action, since it reveals nothing.*

### ⚠️ We are NOT doing that. We are removing the cause instead.

**Keep the action. Make the dialog answer the question a leave row raises.** "Reveals nothing" is a gap, not a law — and the data to close it is already loaded.

**The deciding fact:** `AttendanceDayListSpec.cs:22` already does `Query.Include(day => day.LeaveRequest)`. That is how `ListAttendanceDaysHandler.cs:59` populates the `leaveType` badge. Adding leave detail to the response costs **no extra query and no extra join** — just more property reads off a navigation already in memory.

### The panel, for a punchless leave day

| Label (id) | Label (en) | Source |
|---|---|---|
| **Jenis Cuti** | Leave Type | `leaveType` — already present |
| **Durasi** | Duration | `18 Sep 2026 – 21 Sep 2026 (2 hari kerja)` — `StartDate`, `EndDate`, `WorkdayCount` |
| **Disetujui Oleh** | Approved By | `DecidedByName` |
| **Tanggal Disetujui** | Approved On | `DecidedAtUtc` |

Labels are **Title Case** — a standing preference for view/display fields, even though the rest of `id.json` is sentence case (`"Hari kerja"`, `"Tanggal mulai"`).

`AttendanceDayListItem` gains five fields: `leaveStartDate`, `leaveEndDate`, `leaveWorkdayCount`, `leaveDecidedByName`, `leaveDecidedAtUtc`.

`ViewLogDetailsDialog` branches on `!day.tapInUtc` — leave summary instead of the empty logs table. A punched-during-leave day (`Complete` + `leaveType`) keeps its logs **and** gains the summary.

### `Alasan` — considered, then dropped

Requested for the panel, then removed once the privacy cost was traced. Recording the trace so it isn't re-proposed:

- `AttendanceRules.CanReadAll` = **Owner or Manager** (`AttendanceRules.cs:14-15`) → a Manager sees **every** employee's attendance rows, not just their reports.
- `LeaveRules.CanReadDetails` = `Owner || IsSelf || CanDecideFor` (`LeaveRules.cs:50-51`), and `CanDecideFor` for a **Manager** subject is Owner-only (`LeaveRules.cs:33`).
- So Manager A can see Manager B's attendance rows while being deliberately barred from B's leave reason. Shipping `reason` ungated would expose Sick-leave detail — health data — to colleagues the leave module hides it from.

Dropping the field removes the need for any per-row gate. **All four remaining fields are ungated**: `LeaveType` was ruled low-sensitivity in GSS04, and `DecidedByName` is unconditional in `LeaveRequestResult.cs:74`.

### The condition, where it still matters

`!item.tapInUtc` — not `status === 'OnLeave'` (equivalent today, but `!tapInUtc` survives a future `Holiday` status) and definitely not `!!item.leaveType` (would wrongly catch `Complete`-during-leave rows that *do* have logs).

Used to pick **which panel renders**, not to hide the button. One narrow guard remains: if `!tapInUtc && !leaveType` — a punchless row with no leave, which shouldn't exist (`AttendanceDay.Create` throws on no punches; `CreateForLeave` is the only punchless path; `ReleaseAsync` deletes orphans) — **disable** the button rather than open an empty dialog.

### Demo framing

This **reverses the client's literal request**. They asked for a button to disappear; they'll be shown a button that now does something. Lead with that — "we didn't do what you asked, we did better" only lands when said first.

---

## 6. Timezone correctness (bug fix, not a client ask)

Surfaced while grilling item 4; confirmed by the user as previously seen in a demo. **Same branch as the rest** — item 4's rollout rewrites these files anyway, so fixing it separately would mean hand-writing the same bug into new code.

### The bug class: writes use the browser's zone, reads use Jakarta

| Site | Defect |
|---|---|
| `add-manual-log-dialog.tsx:27-31` | prefills "now" in browser zone |
| `add-manual-log-dialog.tsx:61` | `new Date(form.punchedAt).toISOString()` — unsuffixed string parsed as browser local |
| `view-log-details-dialog.tsx:56-58` | `isoToLocalInput` shifts by `getTimezoneOffset()` |
| `view-log-details-dialog.tsx:130` | same parse, on edit |
| `employee-form.tsx:100` | `toISOString().slice(0,10)` is **UTC** → yesterday before 07:00 WIB (salary-effective date) |
| `employee-form.tsx:106` | same — hire date |
| `delete-employee-dialog.tsx:34` | same — **termination date**. Terminate someone at 06:30 and the default is the previous day |

Display, by contrast, is Jakarta throughout: `attendance-table.tsx:27` (hardcoded), `view-log-details-dialog.tsx:51` and `attendance-day-table.tsx:49` (policy), `i18n/request.ts:22`, `layout.tsx`, `app/page.tsx:14`, `audit-log/page.tsx:34`.

They agree **only when the browser sits at +07:00** — which is why it never reproduced locally. Repro: set the OS timezone to Asia/Tokyo (+09), enter `09:40` → saved `00:40Z`, displayed **07:40 WIB**.

Beyond display: `AttendanceDay.CalendarDate` is derived server-side in the policy zone, so a near-midnight punch lands on the **wrong day**, which then feeds `Complete`/`Incomplete` and the leave-day linkage.

### Fixes

- **Delete `lib/utils/date.ts`.** Its header claims to solve exactly this (*"a filter for 'today' in UTC+7 would silently drop 7 hours"*) but it has **zero imports anywhere in `src/`**, and it anchors to *browser* local rather than Jakarta — the same bug wearing a fix's clothes.
- **New `APP_TIME_ZONE = 'Asia/Jakarta'` in `lib/constants.ts`.** HR fields (hire, salary-effective, termination) use it directly; attendance surfaces prefer `policy.timeZoneId` and fall back to it. Gives `attendance-table.tsx:27` something to import instead of its literal — **folded in**, not left inconsistent.
- **`app/page.tsx:12-25`** (`todayJakartaRange`) is the one place already correct — it formats via `Intl` in Jakarta then rebuilds with an explicit `+07:00`. Simplify to `today(tz)` and drop the hardcoded offset.
- All defaults become `today(tz).toString()` / `now(tz)` from `@internationalized/date`.

### Not bugs — leave alone

- `countWorkdays` (`leave-dialogs.tsx:39`) — consistently UTC-anchored date arithmetic
- `attendance-day-table.tsx:41`, `leave-dialogs.tsx:46` — `timeZone: 'UTC'` formatters, deliberate for date-only values

---

## Suggested implementation order

1. **Item 3** — pure string changes, zero dependencies, ships alone. (One forced re-login after, from the JWT `Issuer`/`Audience` change.)
2. **Item 2** — cleanup script → migration → domain → contracts → frontend. Self-contained.
3. **Item 1** — backend guard + frontend picker rework. Touches the same dialog as item 2, so land item 2 first to avoid conflicting edits in `leave-dialogs.tsx`.
4. **Item 5** — 5 response fields + one dialog branch. Independent of the picker work; can go in parallel.
5. **Items 4 + 6 together** — the RAC rollout across 15 inputs *is* the timezone fix. Largest piece, lands last. `/leave/blocked-dates` can be built any time before it.

## Open questions

| Question | Blocks |
|---|---|
| Replacement text for the `Alasan` placeholder now "(opsional)" is gone. `id.json:541`'s `"Alasan perlu tambahan waktu"` is the house precedent | Cosmetic, item 2 |
| Filter pairs as `DateRangePicker` — provisional, pending the client's opinion | Cosmetic, item 4 |
| Should **Pending** leave be visibly marked in the pickers? Currently it blocks nothing | Client call |

Parked in `GSS02-attendance-holiday-workaround.md`: configurable working days, public holidays / cuti bersama, the weekend-inside-leave asymmetry, and the pending-leave marker question.
