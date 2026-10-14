# 06 — Cross-Module Rules, Termination and Role Sweep

Cases where two modules meet. Run the "Phase A" cases before any close; the "Phase C" ones after.

---

## Leave versus overtime

### XM-01 (I) Leave on a date with an OT assignment
- **Rule (L15):** leave is refused on any date holding a Pending or Approved OT assignment.
- **Phase:** A (July must still be open, otherwise `leave.payroll_closed` may answer first).
- **Login:** Rina.
- **Steps:** 1. Check the date picker for Budi in **Request leave** around **Mon 13 Jul**. 2. Request leave for Budi, Annual, **Mon 13 Jul**.
- **Expect:** The picker marks 13 Jul with an OT dot (success colour) and refuses to pick it. If forced: "Failed to submit request": "The requested dates include a day with an overtime assignment." (`leave.overtime_on_date`).
- **Blocked by:** UI (server as backup).
- **Result:** [ ]

### XM-02 (V) OT on a day of approved leave
- **Rule (L15, O-leave table):** OT wins; the leave stays approved and charged.
- **Phase:** A.
- **Login:** Owner.
- **Steps:** 1. Assign Dewi weekday OT on **Mon 3 Aug** (she has approved Annual leave that day), End `20:30`. 2. Enter Dewi's punches OT In `18:31`, Out `20:30` (no regular punches). 3. Open **Attendance → 3 Aug → Dewi**. 4. Open Dewi's leave on **Leave**.
- **Expect:** The OT assignment is created. Attendance still reads **On leave** for the regular day, plus an OT badge "OT 18:30–20:30 · 2h counted" (Dewi's punches belong to the OT). The leave stays **Approved** and charged.
- **Result:** [ ]

### XM-03 (V) Holidays vs a closed Gaji Premi period
- **Rule (C3):** only a closed Potongan Cuti month blocks a holiday. A closed Gaji Premi period does not.
- **Phase:** C, **after `GP-07` and before `PC-26`**, so that August is closed in Gaji Premi (Jul–Aug) but still open in Potongan Cuti.
- **Login:** Owner.
- **Steps:** 1. **Attendance policy → Holidays** → August 2026 → **Fri 28 Aug** → Name `Test`, Type `Libur Nasional` → **Add Holiday**. 2. Open **Overtime** and look at the Jul–Aug assignments. 3. Remove the holiday again.
- **Expect:** Saved and removed without error. No OT assignment changes its window, "Day Off" tag, counted hours or pay (they were snapshotted). This is a suspected gap rather than a feature; record it, don't fix it.
- **Result:** [ ]

### XM-04 (V) A new holiday recounts existing leave
- **Rule (C4):** declaring or removing a holiday recounts the workdays of live leave covering that date.
- **Phase:** A (use October, which is open in every sense).
- **Login:** Owner, then Dewi/Tono.
- **Steps:** 1. Dewi files Annual **Mon 5 Oct – Wed 7 Oct**; Tono approves. Dewi's quota drops by 3. 2. As Owner, declare **Tue 6 Oct** `Test Holiday` (Libur Nasional). 3. Reopen Dewi's request. 4. Remove the holiday. 5. Reopen the request.
- **Expect:** After step 2 the request shows **2 workdays** and Dewi's used days fall by one; the toast says "Leave counts on this date are being updated." After step 4 it is back to **3 workdays**.
- **Result:** [ ]

---

## Termination

Create a disposable employee first: **Employees → Add employee**, `Eko Temp`, Staff, reports to
Rina Manager, wage 3.000.000, effective from 2026-01-01, hire date 2025-06-01, NIK `3171000000000006`.

### XM-05 (V) Terminating cancels Pending leave and trims leave-only rows
- **Rule (L19, A10):** Pending leave is auto-cancelled; leave-only attendance rows after the termination date are deleted; the approved leave itself is kept.
- **Phase:** A.
- **Login:** Owner.
- **Steps:** 1. File for Eko: Annual **Mon 26 Oct – Fri 30 Oct** (Owner-filed → Approved, one `On leave` row on 26 Oct) and Annual **Mon 2 Nov** as Rina (Pending). 2. **Employees → Eko Temp → Terminate**, Termination date **Wed 28 Oct 2026**, confirm "Terminate". 3. Look at **Leave** and **Attendance**.
- **Expect:** Toast "Employee terminated". The Pending 2 Nov request is **Cancelled** with note "Cancelled automatically: employee terminated." The approved 26–30 Oct request is untouched. Attendance rows for Eko after 28 Oct disappear; those on or before it stay (the 26 Oct row stays).
- **Result:** [ ]

### XM-06 (I) A terminated employee cannot be given new activity
- **Login:** Rina, Owner.
- **Steps (separately):** (a) **Attendance → Add manual log** for Eko. (b) **Request leave** for Eko. (c) **Assign Overtime** for Eko.
- **Expect:** (a) Eko is not selectable, or "Failed to save log": "Cannot record attendance for a terminated employee." (b) "Cannot file leave for a terminated employee." (`leave.employee_terminated`). (c) "Cannot assign overtime to a terminated employee." (`overtime.employee_terminated`).
- **Blocked by:** UI or Server.
- **Result:** [ ]

### XM-07 (V) Corrections to old punches stay open
- **Rule (A2):** only new punches stop at termination; editing existing ones continues so payroll can close.
- **Login:** Rina.
- **Steps:** Before `XM-05`, record one punch for Eko on a past date. After terminating him, open that punch and **Edit** its time.
- **Expect:** The edit saves ("Changes saved"). New punches are still refused (`XM-06`).
- **Result:** [ ]

### XM-08 (I) Adjustment for someone who left before the month
- **Rule (P19):** an adjustment can target an employee who is active or who left within that month.
- **Phase:** C, once August is closed (September is then the first open month).
- **Setup:** Create `Gina Temp` (Staff, reports to Rina, wage 3.000.000, effective 2026-01-01, hire date 2025-06-01, NIK `3171000000000007`) and terminate her with termination date **Tue 30 Jun 2026**.
- **Login:** Owner.
- **Steps:** **Potongan Cuti → September 2026 → Add Adjustment**, Employee `Gina Temp`, Type `Refund`, Amount `10000`, any reason. Then repeat for `Eko Temp` on **October 2026** (he left 28 Oct, inside October).
- **Expect:** Gina: "Could not save the adjustment": "Gina Temp left before this month." (`payroll.adjustment_terminated`); she may not even be listed. Eko: the adjustment saves.
- **Blocked by:** Server (or UI if the list hides her).
- **Result:** [ ]

---

## Money paths stay separate

### XM-09 (V) Gaji Premi and Potongan Cuti are never netted
- **Rule:** overtime pay and leave cuts are separate cycles and never offset each other.
- **Phase:** C.
- **Login:** Owner, Budi.
- **Steps:** Compare Budi on **Gaji Premi** (Jul–Aug Rp 195.000 and Sep–Oct rapel) with Budi on **Potongan Cuti** (August Refund Rp 600.000 after `PC-25`).
- **Expect:** Each page shows its own figure. Neither page includes the other's amount in any total.
- **Result:** [ ]

### XM-10 (V) Owner exemptions
- **Rule (L24, P5, O2):** an Owner has no caps, is never cut, and is never assigned OT.
- **Login:** Owner.
- **Steps:** (a) Check the Owner's Annual quota hint in **Request leave** for a 20-day range. (b) Look at the Employee list in **Assign Overtime**. (c) Look for the Owner in Potongan Cuti.
- **Expect:** (a) "No limit on this type." and no over-quota warning. (b) The Owner is not listed. (c) No row.
- **Result:** [ ]

---

## Role sweep (one pass at the end)

Sign in as each account and tick what you can open. Items marked ✗ must be **absent from the sidebar**
and refuse a direct URL.

| Page | Owner | Manager | Staff |
|---|---|---|---|
| Employees | ✓ | ✓ (own scope) | verify |
| Employee audit log | ✓ | ✗ | ✗ |
| Accounts | ✓ | verify | ✗ |
| Attendance | ✓ all | ✓ all, writes own line | ✓ own only, no writes |
| Attendance policy | ✓ | ✓ | ✗ |
| Attendance devices | ✓ | read-only message | ✗ |
| Leave | ✓ all | ✓ all, decides own Staff | ✓ own, files and cancels own |
| Probation | ✓ decides | ✓ requests for own Staff | ✗ |
| Overtime | ✓ all, with pay | ✓ own Staff, no pay | ✓ own, with pay and Gaji Premi Saya |
| Gaji Premi | ✓ | ✗ | ✗ |
| Potongan Cuti | ✓ | ✗ | ✗ |

### XM-11 (I) Sweep result
- **Steps:** For each ✗ in the table, check the sidebar and open the URL directly.
- **Expect:** Hidden from the sidebar; a direct visit shows the page's refusal text ("Only an Owner can see …" or "You don't have permission to view this page.") or redirects.
- **Result:** [ ] Record each mismatch here, with the page and role:
