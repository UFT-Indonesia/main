# 03 — Overtime (OT Assignments, Punches, Corrections)

Phase A. Page: **Overtime** (`/overtime`). Tabs: **OT Assignments**, **Corrections**, and for Staff
**Gaji Premi Saya**. Run after `01` and `02`.

Budi Staff is the subject of nearly every case. The planned pay is worked out once here and re-used
in `04-gaji-premi.md`.

| # | Date | Window | Created by | Punches to enter (Budi) | Expected result |
|---|---|---|---|---|---|
| 1 | Mon 13 Jul | 18:30–21:30 | Rina → Owner approves | OT 18:32 / 21:35 | 3 h → Rp 25.000 |
| 2 | Tue 14 Jul | 18:30–21:30 | Owner | OT 18:50 / 21:30 | Late, 2 h → Rp 25.000 |
| 3 | Wed 15 Jul | 18:30–21:30 | Owner | OT 18:31 / 20:00 | Left Early, 1 h → Rp 0 |
| 4 | Thu 16 Jul | 18:30–20:30 | Owner | OT 18:31 only | Incomplete → correction → 2 h → Rp 25.000 |
| 5 | Fri 17 Jul | 18:30–20:30 | Rina (Pending) | OT 18:35 / 20:30 | Rejected → punches return to the regular day |
| 6 | Sat 18 Jul | 09:00–16:00 | Owner | 09:05 / 16:00 | 7 h − 1 h lunch = 6 h → Rp 70.000 |
| 7 | Tue 21 Jul | 18:30–20:30 | Owner | OT 18:31 only | Incomplete; correction left Pending |
| 8 | Wed 22 Jul | 18:30–20:30 | Owner | none | Edit and cancel playground |
| 9 | Tue 28 Jul | 18:30–20:30 | Rina (left Pending) | OT 18:31 / 20:30 | Pending → expires at close, Rp 0 |
| 10 | Mon 2 Mar | 18:30–20:30 | Owner | OT 18:31 / 20:30 | 2 h → Rp 25.000 (for the Mar–Apr close) |
| 11 | Mon 17 Aug | 09:00–12:00 | Owner | 09:00 / 12:00 | Holiday day-off OT, 3 h → Rp 25.000 |

For every **weekday** row also record the regular day (`Clock in 08:00`, `Clock out 17:00`) on the
**Attendance** page, so the regular day reads Complete and not Absent. Punches are entered on
**Attendance → Add manual log** (see `ATT-01`). OT punches are ordinary punches; the system sorts
which belong to the OT assignment.

---

## Part 1 — Assigning and approving

### OT-01 (V) Manager assigns weekday OT
- **Rule (O1–O4):** a Manager assigns to direct Staff; weekday OT always starts at 18:30.
- **Login:** Rina.
- **Steps:** 1. **Overtime → OT Assignments → Assign Overtime**. 2. Employee `Budi Staff`. 3. Date **Mon 13 Jul 2026**. 4. Read the hint "Weekday: overtime always starts at 18:30." and note Start is locked. 5. End `21:30`. 6. **Assign Overtime**.
- **Expect:** Toast "OT assignment created — Waiting for an Owner's approval." The row shows **Pending**, window 18:30–21:30, tag "Weekday". Rina's table has **no Pay column**.
- **Result:** [ ]

### OT-02 (V) Owner approves a Manager's OT assignment
- **Rule (O3):** only an Owner approves; Manager-created assignments wait.
- **Login:** Owner.
- **Steps:** Open **Overtime**, find Budi's 13 Jul row, press **Approve**, confirm ("Approve Budi Staff's overtime on …?").
- **Expect:** Toast "OT assignment approved". Status **Approved**. The Owner sees a Pay column.
- **Result:** [ ]

### OT-03 (V) Owner assigns day-off OT, approved at once
- **Rule (O3–O4):** an Owner's assignment is Approved immediately; on a day off the start is free.
- **Login:** Owner.
- **Steps:** **Assign Overtime** → Budi → Date **Sat 18 Jul** → hint "Day off: pick the start; 12:00–13:00 lunch is deducted only when the whole hour is worked." → Start `09:00`, End `16:00` → **Assign Overtime**.
- **Expect:** Toast "OT assignment created — Approved." Row tagged "Day Off", status **Approved**.
- **Result:** [ ]

### OT-04 (V) Create the rest of the table
- **Steps:** Create assignments 2, 3, 4, 7, 8, 10 and 11 from the table above as **Owner**, and 5 and 9 as **Rina** (these stay Pending). For 11 (Mon 17 Aug, a holiday) the form switches to day-off mode and Start becomes editable.
- **Expect:** Each saves. Rina's two appear as Pending, the Owner's as Approved.
- **Result:** [ ]

### OT-05 (I) Window rules
- **Rule (O4):** day-off start can't be before 05:00; a window past midnight must end by 05:00; start ≠ end.
- **Login:** Owner.
- **Steps:** (a) Day-off date **Sun 19 Jul**: Start `04:00`, End `10:00`. (b) Weekday **Mon 20 Jul**: End `06:30` (past midnight, after 05:00). (c) Day-off: Start `09:00`, End `09:00`.
- **Expect:** **Assign Overtime** stays disabled in each case.
- **Blocked by:** UI. (Server codes: `overtime.start_before_boundary`, `overtime.end_after_boundary`, `overtime.window`.)
- **Result:** [ ]

### OT-06 (V) Window past midnight counts on the start date
- **Rule (O4):** an overtime past midnight belongs to its start date.
- **Login:** Owner.
- **Steps:** Assign Budi a weekday OT on **Mon 20 Jul**, End `01:00` (next day). Cancel it afterwards.
- **Expect:** Saves. The row shows the end with a next-day marker; the date stays 20 Jul.
- **Result:** [ ]

### OT-07 (I) Second OT assignment on the same date
- **Rule (O6):** one per employee per date.
- **Login:** Owner.
- **Steps:** Assign Budi another OT for **Mon 13 Jul**.
- **Expect:** "Could not create the OT assignment": "Budi Staff already has overtime on that date." (`overtime.duplicate_date`).
- **Blocked by:** Server (the date picker marks the OT date and may also refuse it).
- **Result:** [ ]

### OT-08 (I) Who can be assigned
- **Rule (O2):** Owner → anyone except an Owner; Manager → own direct Staff only.
- **Login:** Rina, then Owner.
- **Steps:** (a) Rina opens the Employee list in **Assign Overtime**. (b) Owner opens it.
- **Expect:** (a) Only Budi and Sari. No Dewi, no Tono, no Owner. (b) Everyone except the Owner, Managers included.
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 2 — Punches, ownership and counted hours

Enter the punches in the table with **Attendance → Add manual log** as Rina, in Jakarta time.

### OT-09 (V) Full window: 3 counted hours
- **Rule (O7–O9, O12):** OT punches belong to the OT assignment; counted hours = window ∩ punches after grace, rounded down; tier 2–3 h pays Rp 25.000.
- **Steps:** For 13 Jul enter regular In 08:00 / Out 17:00, then OT In 18:32 / Out 21:35. Open **Attendance → July 2026 → 13 Jul → Budi**. Then open **Overtime** as Owner.
- **Expect:** The regular day shows 08:00 → 17:00 **Complete** (OT punches are not the tap-out). A badge reads "OT 18:30–21:30 · 3h counted". On **Overtime** the row shows OT Tap-In 18:32, Tap-Out 21:35, Counted Hours 3, Pay Rp 25.000, status Approved. Rina sees the hours but no Pay.
- **Result:** [ ]

### OT-10 (V) Late beyond grace
- **Rule (O9):** a tap-in beyond clock-in grace is Late and counts from the actual punch.
- **Steps:** 14 Jul regular 08:00/17:00; OT In **18:50**, Out **21:30**.
- **Expect:** Flag **Late**. Counted 2 h (18:50 → 21:30 is 2h40, rounded down), Pay Rp 25.000.
- **Result:** [ ]

### OT-11 (I) Left early under 2 hours pays nothing
- **Rule (O9, O12):** a tap-out beyond grace is "Left Early", counted to the punch; under 2 h the tier is Rp 0.
- **Steps:** 15 Jul regular 08:00/17:00; OT In **18:31**, Out **20:00**.
- **Expect:** Flag **Left Early**, Counted Hours 1, Pay **Rp 0**.
- **Result:** [ ]

### OT-12 (V) Day-off lunch is deducted only when fully inside
- **Rule (O10):** the 12:00–13:00 lunch comes off only on a day off, only if the counted span holds all of it.
- **Steps:** Sat 18 Jul: In **09:05**, Out **16:00** (no regular punches needed).
- **Expect:** 09:05 is within grace, so counting starts at 09:00. 09:00 → 16:00 = 7 h, minus 1 h lunch = **6 h**, Pay **Rp 70.000**. Attendance for 18 Jul shows status **Overtime**.
- **Result:** [ ]

### OT-13 (V) Lunch partly inside is not deducted
- **Rule (O10):** partial overlap deducts nothing.
- **Steps:** Create day-off OT for Budi on **Sun 19 Jul**, **12:30–14:45** (Owner); punches In 12:30, Out 14:45.
- **Expect:** 2h15 counted as **2 h** (lunch not fully inside → no deduction), Pay Rp 25.000. Then cancel this assignment? It has punches, so it **can't** be cancelled (see `OT-23`). Leave it; add it to the Jul expected totals in `04` (one more approved day, 2 h, Rp 25.000).
- **Result:** [ ]

### OT-14 (I) Missing tap-out is Incomplete and pays nothing
- **Rule (O11):** no OT tap-in or no OT tap-out is Incomplete and pays Rp 0 until a correction is approved.
- **Steps:** Thu 16 Jul: regular 08:00/17:00; OT In **18:31** only.
- **Expect:** Flag **Incomplete** (Attendance shows "Missing Punch" on the OT badge); Counted Hours 0, Pay Rp 0. The row for Budi offers **Request Correction** (when Budi signs in).
- **Result:** [ ]

### OT-15 (V) Pending OT still owns its punches
- **Rule (O7, O13):** Pending (and Expired) OT owns punches but pays Rp 0.
- **Steps:** On the Pending OT of **Tue 28 Jul** enter OT In 18:31 / Out 20:30.
- **Expect:** The OT row shows counted hours but Pay is Rp 0 until Approved. The regular day does not show 20:30 as its tap-out.
- **Result:** [ ]

### OT-16 (V) Rejecting hands punches back
- **Rule (O7):** rejected or cancelled OT owns nothing; the punches return to the regular day.
- **Login:** Owner.
- **Steps:** 1. For Rina's Pending OT on **Fri 17 Jul** (item 5) enter regular In 08:00 / Out 17:00 and OT In 18:35 / Out 20:30. Note the Attendance tap-out for 17 Jul (17:00). 2. On **Overtime** press **Reject** and confirm ("Their punches go back to the regular day.").
- **Expect:** Toast "OT assignment rejected". Attendance 17 Jul now shows tap-out **20:30**. The row's status is **Rejected**.
- **Result:** [ ]

---

## Part 3 — Correction requests (missing OT punch)

### OT-17 (V) Employee requests a missing tap-out
- **Rule (O17):** only the employee files; reason and proof are required.
- **Login:** Budi.
- **Steps:** 1. **Overtime → OT Assignments**, find Thu 16 Jul. 2. **Request Correction**. 3. Time `20:30` ("OT Tap-Out Time", hint "Must fall inside the assigned window, up to 20:30."). 4. Reason `Reader did not beep`. 5. Upload a small PDF as Proof. 6. **Submit Request**.
- **Expect:** Toast "Correction requested — Your manager or an Owner will decide it." It appears under **Corrections**.
- **Result:** [ ]

### OT-18 (V) Manager approves, hours count
- **Rule (O19–O20):** the employee's Manager or an Owner decides; approval writes a manual punch counted on the work date.
- **Login:** Rina.
- **Steps:** **Overtime → Corrections** → Budi's request → open the proof → **Approve** ("Write Budi Staff's OT Tap-Out at 20:30?").
- **Expect:** Toast "Correction approved". The 16 Jul OT row now has Tap-Out 20:30, Counted Hours 2, Pay Rp 25.000 (Owner view). A new punch with Source "Manual" appears in Attendance.
- **Result:** [ ]

### OT-19 (V) Correction on 21 Jul left Pending
- **Login:** Budi.
- **Steps:** Repeat `OT-17` for **Tue 21 Jul** (Tap-Out `20:30`). **Do not approve it.**
- **Expect:** Request shows Pending; it will expire at the Gaji Premi close (`04`).
- **Result:** [ ]

### OT-20 (I) Correction rules
- **Login:** Budi.
- **Steps (each on an assignment that allows it):**
  (a) Tap-out time outside the window, e.g. `22:00` on 21 Jul.
  (b) Submit without proof.
  (c) A second request for the same punch while one is pending (21 Jul again).
  (d) A tap-in correction for 13 Jul, which already has an OT tap-in.
  (e) A tap-out earlier than the last punch.
- **Expect:**
  (a) "The time must fall inside the assigned window." (`overtime_correction.outside_window`)
  (b) **Submit Request** is disabled (UI).
  (c) "A correction for this punch is already waiting for a decision." (`overtime_correction.duplicate`)
  (d) "This overtime is not missing its tap-in." (`overtime_correction.not_missing`)
  (e) "The tap-out must be after the last recorded punch." (`overtime_correction.before_tap_in`)
- **Blocked by:** Server, except (b) UI.
- **Result:** [ ]

### OT-21 (I) Who decides
- **Rule (O19):** never the requester; a Manager's own correction goes to an Owner only.
- **Login:** Rina, Tono, Budi.
- **Steps:** (a) Open **Corrections** as Budi. (b) Owner assigns Rina a weekday OT (Wed 29 Jul, 18:30–20:30), Rina requests a tap-in correction `18:30` with proof; open **Corrections** as Tono.
- **Expect:** (a) No Approve/Reject buttons. (b) Tono sees no Approve/Reject on Rina's request; only the Owner does.
- **Blocked by:** UI.
- **Result:** [ ]

### OT-22 (I) Staff cannot file for someone else
- **Login:** Rina.
- **Steps:** Open Budi's OT row.
- **Expect:** No **Request Correction** button for Rina or for the Owner. Only Budi has it.
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 4 — Editing, cancelling, visibility

### OT-23 (I) With punches: no cancel, no shorten
- **Rule (O16):** once there are punches, the end can only be extended and the OT cannot be cancelled.
- **Login:** Owner.
- **Steps:** On **13 Jul** (has punches): (a) **Edit End Time** → `21:00`. (b) **Cancel OT** → confirm.
- **Expect:** (a) "Could not update the OT assignment": "An overtime that already has punches can only be extended." (`overtime.shorten_with_punches`). (b) "Could not cancel": "This overtime already has punches and can no longer be cancelled." (`overtime.has_punches`).
- **Blocked by:** Server.
- **Result:** [ ]

### OT-24 (V) A Manager's edit goes back to the Owner
- **Rule (O14–O15):** edit end until period close; a Manager's edit returns it to Pending.
- **Login:** Rina, then Owner.
- **Steps:** 1. Rina edits the **22 Jul** OT (no punches): **Edit End Time** → `21:00` → **Save**. 2. Owner approves it again. 3. Owner edits the end to `21:30`.
- **Expect:** (1) Toast "OT assignment updated — Waiting for an Owner's approval." Status **Pending**. (2) Back to Approved. (3) An Owner's edit stays **Approved**.
- **Result:** [ ]

### OT-25 (V) Cancel without punches
- **Login:** Owner.
- **Steps:** On the **22 Jul** OT press **Cancel OT**, confirm.
- **Expect:** Toast "OT assignment cancelled". Status **Cancelled**; the date is free again (leave can be filed on it).
- **Result:** [ ]

### OT-26 (V) A holiday declared later doesn't flip an OT assignment
- **Rule (O5):** `IsDayOff` is stored at creation.
- **Login:** Owner.
- **Steps:** 1. Confirm the 17 Aug OT shows "Day Off" (it was created while 17 Aug was a holiday). 2. **Attendance policy → Holidays** → remove the 17 Aug holiday. 3. Re-open the OT row. 4. Declare the holiday again (`HUT RI`, Libur Nasional).
- **Expect:** The OT stays "Day Off" with its original window, and the lunch and punch-ownership rules do not change. Declaring or removing the holiday in an open month is allowed.
- **Result:** [ ]

### OT-27 (V) Who sees what
- **Rule (O12, G2):** pay is visible to the Owner and the employee; a Manager sees windows and hours only; Staff get "Gaji Premi Saya".
- **Login:** Budi, Rina, Owner.
- **Steps:** Compare the **Overtime** page for each.
- **Expect:**
  - Budi: only his own OT assignments, a Pay column, and the **Gaji Premi Saya** tab.
  - Rina: Budi's and Sari's OT assignments, **no Pay column**, no Gaji Premi Saya tab.
  - Owner: everyone's, Pay column, Approve/Reject on Managers' assignments. The Owner also has no Gaji Premi Saya tab; the Owner reads individuals on the payroll page.
- **Result:** [ ]
