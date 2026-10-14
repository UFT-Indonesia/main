# 05 — Potongan Cuti (Leave Salary Cuts)

Phases A, B and C. Page: **Potongan Cuti** (`/payroll/potongan-cuti`, Owner only). Also the **Leave**
page and the **Employees → [person] → Probation & leave quota** card.

**Prerequisites:** `00-test-bed-setup.md` S1 (launch month set to July 2026), leave cases `LV-31` and
`LV-34` approved, and the holidays from `01`. All the money below assumes the default divisor of 20.

## What is cut, in one paragraph

A leave day is **free** while it fits the yearly cap and **cut** once it goes past it. Every **Unpaid**
day is cut in full. The request that pushes you over the cap pays: requests take the cap in the order
they were decided, and inside one request the **last** days are the cut ones. A cut day costs
`salary on that date ÷ divisor`. An employee's month total is the exact sum, truncated **toward zero**
to the nearest Rp 1.000. Owners are never cut.

## Expected numbers

| Who | Month | Leave | Free / cut | Rate | Month total |
|---|---|---|---|---|---|
| Budi | July | Annual 6–8 Jul, cap 1 | 6 Jul free; 7 and 8 Jul cut | 5.000.000 ÷ 20 = **Rp 250.000** | **Rp 500.000** |
| Sari | August | Unpaid 12–13 Aug | both cut | 4.000.000 ÷ 20 = Rp 200.000 | Rp 400.000 (Rp 427.000 after `PC-07`) |
| Sari | August | Sick 10–11 Aug, 14 Aug | free (cap 30) | — | no cut |
| Dewi | August | Annual 3–4 Aug, 14 and 19 Aug, half-day 6 Aug, Izin 7 Aug | all free (cap 12) | — | no cut |

Sari's hire date is 1 Sep 2026, so her August leave pre-dates it. The system doesn't check leave
against hire date; the cast is deliberately kept small.

---

## Part 1 — Reading the numbers (Phase A)

### PC-01 (V) July page
- **Rule (P1–P6):** cut days beyond the cap, priced at salary ÷ divisor.
- **Login:** Owner.
- **Steps:** 1. **Potongan Cuti** → Month `July 2026`. 2. Find Budi. 3. Click his row to open the day list.
- **Expect:** "Open" badge and "Estimate, not final". Budi: Cut Days **2**, Total **Rp 500.000**. Day list: 6 Jul **Free**, 7 Jul **Cut** Salary Rp 5.000.000 Daily Rate Rp 250.000 Amount Rp 250.000, 8 Jul **Cut** (same). Owner and Dewi do not appear as cut rows.
- **Result:** [ ]

### PC-02 (V) Changing the divisor recalculates open months
- **Rule (P6, P20):** the divisor is Owner-only; open months follow a change.
- **Login:** Owner.
- **Steps:** 1. On the July page set Divisor `25`, **Save**. 2. Read the confirmation ("The price of a cut day for every employee in every open month will be recalculated, from salary ÷ 20 to salary ÷ 25…") and confirm. 3. Look at Budi. 4. Open **History**. 5. Set it back to `20`.
- **Expect:** Toast "Divisor updated". Budi's rate becomes Rp 200.000 and his total **Rp 400.000**. A line "Last changed from 20 to 25 by System Owner on …" appears, and **History** lists the change. After step 5 the totals return to Rp 500.000.
- **Result:** [ ]

### PC-03 (I) Divisor out of range
- **Rule (P6):** divisor is between 1 and 31.
- **Login:** Owner.
- **Steps:** Type `0`, then `32`, then `20` (the current value).
- **Expect:** **Save** is disabled in all three cases. (Server: "Divisor must be between 1 and 31." — `payroll.divisor`.)
- **Blocked by:** UI.
- **Result:** [ ]

### PC-04 (V) Per-employee exception
- **Rule (P7):** an Owner can set a flat amount per cut day (Rp 0 exempts) or a custom divisor, never both.
- **Login:** Owner.
- **Steps:** 1. **Employees → Budi Staff → Probation & leave quota → Leave Deduction Pricing**. 2. Pricing `Flat Amount per Cut Day`, Amount `0`, save. 3. Check July. 4. Pricing `Custom Divisor`, Divisor `10`, save. 5. Check July. 6. Reset to `Company Divisor`, save. 7. Open **Employee audit log**.
- **Expect:** Step 3: Budi's total **Rp 0**. Step 5: rate Rp 500.000, total **Rp 1.000.000**. Step 6: back to Rp 500.000. The audit log has entries like "Leave deduction: company divisor → flat Rp 0 per day".
- **Result:** [ ]

### PC-05 (I) Exception can't be nonsense
- **Login:** Owner.
- **Steps:** In the same card try (a) Flat Amount `-1000`; (b) Custom Divisor `40`.
- **Expect:** Message "Enter a flat amount of 0 or more, or a divisor from 1 to 31. Nothing was saved." and nothing changes. Choosing both at once is not possible; the Pricing selector allows one mode.
- **Blocked by:** UI.
- **Result:** [ ]

### PC-06 (V) Unpaid is always cut; Sick within cap is free
- **Rule (P1):** every Unpaid day is cut; Sick is free up to 30.
- **Login:** Owner.
- **Steps:** Month `August 2026` → open Sari's row.
- **Expect:** Cut Days **2**, Total **Rp 400.000**. Day list: 12 and 13 Aug **Cut** at Rp 200.000; the Sick days 10, 11 and 14 Aug **Free**. Dewi has only free rows. 17 and 18 Aug appear nowhere (holidays are never leave days).
- **Result:** [ ]

### PC-07 (V) Pricing by date, and rounding toward zero
- **Rule (P6, P8):** each day is priced with the salary in effect on that date; the month total truncates toward zero to Rp 1.000.
- **Login:** Owner.
- **Steps:** 1. **Employees → Sari Probation** → Monthly wage `4555555`, Effective from **2026-08-13** → save. 2. Open the August page → Sari's day list.
- **Expect:** "Changes saved". 12 Aug is still priced at Rp 200.000 (the old salary). 13 Aug is Rp 4.555.555 ÷ 20 = **Rp 227.777,75**. Exact sum Rp 427.777,75, shown as **Rp 427.000**.
- **Result:** [ ]

### PC-08 (I) Salary can't be backdated
- **Login:** Owner.
- **Steps:** Edit Sari's wage again with Effective from **2026-08-01**.
- **Expect:** "Update failed": "Effective date cannot be earlier than current effective date." (`employee.salary_backdated`).
- **Blocked by:** Server.
- **Result:** [ ]

### PC-09 (V) Cuti Bersama and holidays cost nothing
- **Rule (P1, C2):** holidays are never charged, so they are never cut.
- **Login:** Owner.
- **Steps:** August page → Dewi.
- **Expect:** Her 14–19 Aug leave contributes only 14 and 19 Aug (both Free). No line for 17 or 18 Aug. Cut Days 0.
- **Result:** [ ]

### PC-10 (V) Owner leave never appears
- **Rule (P5):** Owners are never cut.
- **Steps:** September page.
- **Expect:** The Owner's 14–16 Sep leave (`LV-17`) has no row at all.
- **Result:** [ ]

### PC-11 (I) Months before launch
- **Rule (P11):** nothing can be closed before the launch month.
- **Login:** Owner.
- **Steps:** Pick **June 2026**.
- **Expect:** "This month is before launch: over-quota leave was refused then, so nothing is cut." The blocker line reads "Before launch — nothing to close"; **Close Month** is disabled.
- **Blocked by:** UI.
- **Result:** [ ]

### PC-12 (I) Close blockers, in plain words
- **Rule (P11):** close needs the month to have ended, the earlier months closed, and no Pending leave.
- **Setup:** As Dewi file Annual for **Fri 28 Aug** (leave it Pending).
- **Login:** Owner.
- **Steps:** Look at **August 2026**, then **September 2026**, then **October 2026** (current month).
- **Expect:** August: **Close Month** disabled; blockers "Close July first" and "Waiting on 1 pending leave request — decide it below". A "Leave Awaiting a Decision" list shows Dewi's request. September: "Close July first" ("Close August first" appears once July is closed). October: "October 2026 hasn't ended yet".
- **Blocked by:** UI. (Server codes: `payroll.close_in_order`, `payroll.pending_leave`, `payroll.not_ended`.)
- **Result:** [ ]

### PC-13 (V) Decide a pending request from the payroll page
- **Rule (P11):** the Owner clears blockers by deciding the leave.
- **Login:** Owner.
- **Steps:** On the August page click Dewi's pending row. In the Approve/Deny dialog press **Deny** with a note.
- **Expect:** Dialog opens (same one as the Leave page). After deciding, the pending list refreshes and the "Waiting on pending leave" blocker disappears. Keep it denied, so it doesn't add August figures.
- **Result:** [ ]

### PC-14 (V) The Leave page warns about close blockers
- **Rule (P11):** a banner tells deciders that undecided leave blocks a close.
- **Setup:** Have another Pending request in August (for example Dewi, Annual, **Mon 31 Aug**).
- **Login:** Tono, Rina, Owner.
- **Steps:** Open **Leave** as each.
- **Expect:** Tono (Dewi's Manager) sees "1 leave request in August 2026 is waiting for your decision. August 2026 payroll can't be closed until it is decided." Rina sees no banner (she can't decide for Dewi). The Owner sees the company-wide count, a **Show them** button, and **Open Potongan Cuti**. Staff see nothing. Then deny the request to clear it.
- **Result:** [ ]

---

## Part 2 — Closing (Phase B, irreversible)

Before closing July confirm: divisor back to 20, Budi's exception set to Company Divisor, **no
Pending leave** touching July.

### PC-15 (V) Close July
- **Rule (P10–P12):** the Owner closes a month after it ends; the close freezes labels, divisor and exceptions.
- **Login:** Owner.
- **Steps:** 1. **Potongan Cuti → July 2026**. 2. **Close Month**. 3. Read the dialog ("July 2026 will be frozen … This cannot be undone.") and press **Close Month**.
- **Expect:** Toast "Month closed — Cut days and amounts are now frozen." The page shows **Closed**, "Closed by System Owner", and "This month was closed with divisor 20." Budi: Rp 500.000. The day list shows the same free/cut labels, now permanent. **Close Month** is gone or disabled.
- **Result:** [ ]

### PC-16 (I) Closing out of order
- **Login:** Owner.
- **Steps:** Open **September 2026** (July closed, August open).
- **Expect:** Blocker "Close August first". **Close Month** disabled.
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 3 — After close (Phase C)

### PC-17 (I) Non-Owners can't change closed-month leave
- **Rule (P14):** leave with a day in a closed month is locked for everyone but the Owner.
- **Login:** Rina, then Budi.
- **Steps:** Open **Leave**, find Budi's 6–8 Jul request. Look at **Edit** and **Cancel**.
- **Expect:** Both are disabled, with the reason "Payroll for July 2026 is closed. Only the Owner can correct this leave."
- **Blocked by:** UI. (Server: `leave.payroll_closed`.)
- **Result:** [ ]

### PC-18 (I) Holiday in a closed month
- **Rule (C3):** declaring a holiday on a date in a closed month is refused.
- **Login:** Owner.
- **Steps:** **Attendance policy → Holidays** → July 2026 → click **14** → Name `Test` → **Add Holiday**.
- **Expect:** "Failed to save holiday": "That date is in a month whose payroll is already closed." (`holiday.payroll_closed`). (Removing a holiday in a closed month is refused the same way; there is none in July to try.)
- **Blocked by:** Server.
- **Result:** [ ]

### PC-19 (I) Filing leave that would be cut in a closed month
- **Rule (P14):** a late request is allowed only if it costs nothing.
- **Login:** Sari.
- **Steps:** Request leave, `Unpaid`, **Mon 20 Jul**, Reason `Late filing`. Submit.
- **Expect:** "Failed to submit request": "This leave has days in a month whose payroll is already closed and would cost salary. It can't be added." (`leave.payroll_closed`). Unpaid days are always cut, so it is always refused.
- **Blocked by:** Server.
- **Result:** [ ]

### PC-20 (V) Free leave into a closed month is allowed and stamped
- **Rule (P14):** a free late request is allowed; its days are stamped free in the closed month.
- **Login:** Dewi, then Tono, then Owner.
- **Steps:** 1. Dewi files Annual **Tue 21 Jul**, Reason `Forgot to file`. 2. Tono approves. 3. Owner opens **July 2026** → Dewi.
- **Expect:** Both steps succeed. July now shows Dewi with a free line "Added after close", total Rp 0, no change to anyone else's figures.
- **Result:** [ ]

### PC-21 (V) Owner corrects closed-month leave: money lands next month
- **Rule (P15–P16):** the Owner may edit or cancel with a reason; closed figures never change; the difference lands as a late correction on the first open month.
- **Login:** Owner.
- **Steps:** 1. **Leave** → Budi's 6–8 Jul request. The banner reads "Payroll for July 2026 is closed. Changes here are corrections after close." 2. Press **Cancel** to open the correction dialog. 3. Read the banner ("…the figures already paid for July 2026 won't change."). 4. Enter Reason `Budi worked on 7 and 8 July, confirmed by Rina`. 5. Read the Effect panel. 6. Confirm with the button **Cancel Leave (Correction)**. 7. Open **Potongan Cuti → July**, then **August**.
- **Expect:** Effect panel: "Over-quota days on this leave" going to 0, and "Late correction in August 2026: Refund Rp 500.000", with lines for 7 and 8 Jul at July's rate (Rp 250.000 each). Toast "Leave cancelled". **July still shows Budi at Rp 500.000** with a marker "Corrected after close by System Owner on … — settled in August 2026". **August** shows Budi **Refund Rp 500.000**, "2 late corrections" (one per corrected day) with a **View July** link. The leave details now show "Corrected After Close". Budi sees the correction and the reason, never rupiah.
- **Result:** [ ]

### PC-22 (I) A correction needs a reason
- **Login:** Owner.
- **Steps:** Open the correction dialog on another closed-month request (Dewi's 21 Jul) and leave Reason empty.
- **Expect:** The confirm button is disabled. (Server: `leave.correction_reason`: "A correction after the payroll month closed needs a reason.")
- **Blocked by:** UI.
- **Result:** [ ]

### PC-23 (V) Manual adjustment on an open month
- **Rule (P19):** the Owner can add a refund or an extra deduction to an open month, with a reason.
- **Login:** Owner.
- **Steps:** 1. **Potongan Cuti → August 2026** → **Add Adjustment**. 2. Employee `Budi Staff`, Type `Refund`, Amount `100000`, Reason `Refund 20 Mar cut — January corrected`. 3. **Add Adjustment**. 4. Add a second: Type `Extra deduction`, Amount `12345`, Reason `Late fee`.
- **Expect:** Toast "Adjustment added" each time. Budi's August row now carries "2 late corrections" (from `PC-21`) and "2 adjustments". Exact sum: −500.000 − 100.000 + 12.345 = −587.655, shown as **Refund Rp 587.000** (rounded toward zero, not 588.000). The page header splits: **Cuts Rp 427.000** and **Refunds Rp 587.000** (Sari's cut and Budi's refund).
- **Result:** [ ]

### PC-24 (I) Adjustment rules
- **Login:** Owner.
- **Steps:** In **Add Adjustment** try: (a) Reason empty. (b) Amount `0`. (c) Open the Employee list. (d) Open **July** (closed) and look for **Add Adjustment**.
- **Expect:** (a)(b) The button is disabled. (c) The Owner is not listed. (d) Not available on a closed month. (Server codes: `payroll.adjustment_owner`: "An Owner's salary is never cut, so it can't be adjusted here."; `payroll.already_closed`.)
- **Blocked by:** UI.
- **Result:** [ ]

### PC-25 (V) Remove an adjustment
- **Rule (P19):** no editing; delete and re-add, only while the month is open.
- **Login:** Owner.
- **Steps:** August page → Budi → the "Adjustments" section → **Remove adjustment** on the Rp 12.345 one.
- **Expect:** Toast "Adjustment removed". The sum becomes −600.000 and the display **Refund Rp 600.000**.
- **Result:** [ ]

### PC-26 (V) Close August
- **Login:** Owner.
- **Steps:** Make sure no Pending leave touches August. **Close Month** for August 2026.
- **Expect:** Toast "Month closed". August is frozen with Sari at Rp 427.000 cut and Budi at Refund Rp 600.000, adjustment and correction lines included. **Add Adjustment** and **Remove adjustment** are gone. August leave is now locked for non-Owners (`PC-17` pattern).
- **Result:** [ ]

### PC-27 (I) Visibility
- **Rule (G2, L29):** days for everyone, rupiah for the Owner only.
- **Login:** Rina, Budi.
- **Steps:** Look for **Potongan Cuti** in the sidebar and open `/payroll/potongan-cuti`. Then open Budi's cancelled July request on **Leave**.
- **Expect:** No sidebar item; the page shows "Only an Owner can see Potongan Cuti." The leave details show the correction reason and the over-quota day count, **no rupiah** anywhere.
- **Blocked by:** UI.
- **Result:** [ ]
