# 00 — Test-Bed Setup

Do this once, on a **disposable database**. Everything else assumes the names, dates and numbers below.

## S0. Prerequisites

1. PostgreSQL running, an empty database, connection string set in `apps/api/src/Erp.Web/appsettings.Development.json` (see the root `README.md`, "Getting Started").
2. Apply migrations: from `apps/api/src/Erp.Infrastructure` run `dotnet ef database update`. This must include `AddLeaveSalaryDeduction` and `LeaveDeductionCorrections`.
3. Start the API and the web app (`pnpm dev` in `apps/web`).
4. Set your **browser and OS time zone to Asia/Jakarta**. Times you type into forms are read in your browser's zone, and every rule here is evaluated in Jakarta time.
5. Sign in as the seeded Owner: `owner@uft-juan.local` / `Owner12345` (Development seed).

## S1. Payroll launch month (SQL, one line)

The payroll launch month defaults to the month the migration ran. A month can only be closed after it
has ended, and not before the launch month, so move the launch month back:

```sql
UPDATE "PayrollSettings" SET first_month = '2026-07-01' WHERE "Id" = 1;
```

After this, July 2026 is the first month that can be closed. Leave the company divisor at the default of **20**.

## S2. Attendance policy

Sign in as Owner → **Attendance policy** → set:

| Field | Value |
|---|---|
| Shift start | 08:00 |
| Shift end | 17:00 |
| Clock-in grace (minutes) | 15 |
| Clock-out grace (minutes) | 15 |
| Time zone | Asia/Jakarta |
| Max Izin Hours | 3 |

Press **Save changes**. Expect the toast "Policy updated".

Derived numbers used later: latest on-time tap-in is **08:15**, earliest on-time tap-out is **16:45**,
a net working day for hourly Izin is 8 hours (09 hours minus the 1-hour lunch).

## S3. The cast

Create each person at **Employees → Add employee**. Use the same values for every unlisted field
(NPWP empty, Status Active).

| Name | Role | Reporting to | Monthly wage | Effective from | Hire date | NIK (16 digits) |
|---|---|---|---|---|---|---|
| Rina Manager | Manager | System Owner | 8.000.000 | 2026-01-01 | 2024-01-01 | 3171000000000001 |
| Tono Manager | Manager | System Owner | 8.000.000 | 2026-01-01 | 2024-01-01 | 3171000000000002 |
| Budi Staff | Staff | Rina Manager | 5.000.000 | 2026-01-01 | 2025-01-01 | 3171000000000003 |
| Dewi Staff | Staff | Tono Manager | 4.000.000 | 2026-01-01 | 2025-01-01 | 3171000000000004 |
| Sari Probation | Staff | Rina Manager | 4.000.000 | 2026-01-01 | 2026-09-01 | 3171000000000005 |

Expect the toast "Employee added" each time.

Facts that follow: **Sari** is on probation until **2026-12-01** (hire date plus 3 months). **Budi** and
**Dewi** are confirmed (full Annual quota of 12). **Rina** and **Tono** are each other's peers, and each
only manages their own Staff.

## S4. Accounts

For Rina, Tono, Budi, Dewi and Sari: **Accounts → Create account** → pick the employee → Username
(`rina`, `tono`, `budi`, `dewi`, `sari`) → **Create**. A "Temporary password" dialog appears **once**;
copy it. Sign in once as each user, and set the new password to something like `Test12345` on the
forced change-password page. Keep a note of who is which; the cases say "Login: Budi" etc.

## S5. Budi's Annual quota override

Sign in as Owner → **Employees → Budi Staff** → "Probation & leave quota" card → "Leave quota overrides".
Set **Annual = 1** and save ("Changes saved"). This makes Budi's second Annual day a salary-cut day,
which the payroll tests need. Dewi keeps the default 12.

## S6. Fixed calendar for the tests

All test dates are in 2026. Weekdays for reference:

| Date | Day | Used for |
|---|---|---|
| Mon 2 Mar | Mon | Gaji Premi Mar–Apr OT |
| Mon 6 – Wed 8 Jul | Mon–Wed | Budi's Annual leave (Potongan Cuti, July) |
| Mon 13 – Fri 17 Jul | Mon–Fri | Weekday OT cases |
| Sat 18 Jul | Sat | Day-off OT |
| Mon 3 – Fri 7 Aug | Mon–Fri | Dewi's leave and Budi's attendance |
| Sat 8 – Sun 9 Aug | weekend | Weekend cases |
| Mon 10 – Thu 13 Aug | Mon–Thu | Sick and Unpaid cases |
| Fri 14 Aug | Fri | Holiday-spanning leave starts |
| Mon 17 Aug | Mon | **Holiday: HUT RI (Libur Nasional)** |
| Tue 18 Aug | Tue | **Holiday: Cuti Bersama Test (Cuti Bersama)** |
| Wed 19 Aug | Wed | Holiday-spanning leave ends |
| Mon 24 Aug | Mon | Decision-rule cases |

**Today is Tue 13 Oct 2026** in the examples. Anything that depends on "today" says so. The rapel
deadline for the Jul–Aug period is **Thu 15 Oct 2026**; after that day the "deadline passed" cases
replace the valid ones (see `04-gaji-premi.md`).

## S7. Setup-time validation cases

Run these while creating the cast. Sign in as Owner, **Employees → Add employee**.

### SET-01 (I) NIK too short
- **Rule:** NIK is exactly 16 digits.
- **Steps:** Fill the form with a valid name and wage, NIK `317100000000` (12 digits). Press save.
- **Expect:** Inline message "NIK must be 16 digits." No employee is created.
- **Blocked by:** UI.
- **Result:** [ ]

### SET-02 (I) NIK with letters
- **Steps:** NIK `31710000000000AB`. Save.
- **Expect:** "NIK must contain digits only."
- **Blocked by:** UI.
- **Result:** [ ]

### SET-03 (I) Non-positive wage
- **Rule:** Monthly wage must be positive.
- **Steps:** Wage `0`. Save.
- **Expect:** Error toast "Failed to add employee" with "Monthly wage must be positive." (`employee.wage`). If the form already marks the field invalid, that also passes.
- **Blocked by:** UI or Server.
- **Result:** [ ]

### SET-04 (I) Staff without a manager
- **Rule:** Every non-Owner needs a reporting line.
- **Steps:** Role Staff, leave "Reporting to" empty. Save.
- **Expect:** Error: "Non-owner employee must have a parent." (`employee.parent_required`).
- **Blocked by:** UI or Server.
- **Result:** [ ]

### SET-05 (I) Salary backdated
- **Rule:** Effective-from can't move earlier than the current one.
- **Steps:** Open **Budi Staff** → change "Effective from" to 2025-06-01 → save.
- **Expect:** "Update failed": "Effective date cannot be earlier than current effective date." (`employee.salary_backdated`).
- **Blocked by:** Server.
- **Result:** [ ]
- **Note:** Don't run this on Sari. Phase C needs her salary history intact.

## Dependency map

```
S0–S6  →  01 (holidays, punches)  →  02 (leave)  →  03 (OT)
                                          ↘            ↘
                              05 open cases      04 open cases
                                          ↓            ↓
                              05 close July     04 close Mar–Apr, then Jul–Aug
                                          ↓            ↓
                              05 post-close     04 rapel
                                          ↘            ↙
                                         06 cross-module
```
