# 02 — Leave Requests, Quota and Probation

Phase A (a few cases are noted Phase C). Pages: **Leave** (`/leave`), **Probation** (`/probation`),
**Employees → [person]** ("Probation & leave quota" card).

Run the holiday cases in `01` first (17 and 18 Aug must be holidays). Keep every request you create
in **August 2026** decided or withdrawn before you close August in `05-potongan-cuti.md`.

**Budi's Annual ordering matters.** Budi's Annual quota is 1 day, and the cap goes to whichever
approved request has the earliest decision time. Approve `LV-34` (Budi, 6–8 Jul) **before** any
other Annual leave for Budi, or the July payroll numbers in `05` will not match.

The Request leave dialog hides or disables most illegal inputs on its own. The cases say which
rejections come from the UI and which reach the server.

---

## Part 1 — Filing, deciding, cancelling

### LV-01 (V) Staff files their own Annual leave
- **Rule (L9, L21):** Staff can file for themselves; Annual cap is 12 days.
- **Login:** Dewi.
- **Steps:** 1. **Leave → Request leave**. 2. Employee: Dewi (self). 3. Type `Annual`. 4. Leave dates: **Mon 3 Aug – Wed 5 Aug**. 5. Reason `Family trip`. 6. Note the preview and the quota line, then **Submit request**.
- **Expect:** Preview "3 workdays"; quota hint "12 of 12 days left (0 used)." Toast "Request submitted — Leave request created successfully." Row appears with status **Pending**.
- **Result:** [ ]

### LV-02 (V) Manager approves Staff leave
- **Rule (L12):** Staff leave is decided by their own Manager or any Owner.
- **Login:** Tono.
- **Steps:** 1. Open **Leave**. 2. On Dewi's row press **Approve** (confirm text "Approve leave for Dewi Staff, … (3 workdays)?"). 3. **Approve**. 4. Then see `ATT-08` for the calendar view.
- **Expect:** Toast "Leave approved". Status **Approved**. Dewi's quota line reads "9 of 12 days left (3 used)."
- **Result:** [ ]

### LV-03 (I) The filer can't approve their own filing
- **Rule (L11):** whoever filed a request never decides it.
- **Login:** Tono, then Owner.
- **Steps:** 1. As Tono, **Request leave** for Dewi: Annual, **Mon 24 Aug** (one day), reason `Errand`. Submit. 2. Look at the new row's actions. 3. Sign in as Owner and look at the same row.
- **Expect:** Tono sees **no Approve or Deny** on his own filing (Cancel is allowed). The Owner sees both. Leave it Pending for now; `LV-23` uses it.
- **Blocked by:** UI. (Direct API call would return "You cannot decide this leave request.")
- **Result:** [ ]

### LV-04 (I) Staff can't file for someone else
- **Rule (L9):** Staff file only for themselves.
- **Login:** Dewi.
- **Steps:** Open **Request leave** and open the Employee list.
- **Expect:** Only Dewi (self) is selectable.
- **Blocked by:** UI.
- **Result:** [ ]

### LV-05 (I) Manager can't file for another Manager's Staff
- **Login:** Rina.
- **Steps:** **Request leave** → Employee list.
- **Expect:** The list holds Rina, Budi and Sari only. No Dewi, no Tono.
- **Blocked by:** UI.
- **Result:** [ ]

### LV-06 (I) Reason rules
- **Rule (L6):** reason required, 2 to 500 characters.
- **Login:** Dewi.
- **Steps:** (a) Fill everything validly for a future day (e.g. Annual, Mon 31 Aug) but Reason `x`. (b) Paste 501 characters into Reason.
- **Expect:** (a) **Submit request** stays disabled. (b) Either the field stops at 500, or the server answers "Reason cannot exceed 500 characters." (`leave.reason_length`).
- **Blocked by:** UI (a); UI or Server (b).
- **Result:** [ ]

### LV-07 (I) Range with no workdays
- **Rule (L7):** the range must contain at least one workday.
- **Login:** Dewi.
- **Steps:** Pick **Sat 8 Aug – Sun 9 Aug**, then pick **Mon 17 Aug – Tue 18 Aug** (both holidays).
- **Expect:** Preview "No workdays in range" for both; **Submit request** is disabled.
- **Blocked by:** UI. (Server code `leave.no_workdays`: "Leave range contains no working days (weekends and holidays are off).")
- **Result:** [ ]

### LV-08 (V) Holidays inside a range aren't charged
- **Rule (L7, C2):** weekends and holidays, including Cuti Bersama, cost no quota.
- **Login:** Dewi, then Tono.
- **Steps:** 1. Dewi: **Request leave**, Annual, **Fri 14 Aug – Wed 19 Aug**, Reason `Long weekend`. 2. Check the preview. 3. Submit. 4. Tono approves.
- **Expect:** Preview "2 workdays" (14 and 19 Aug only). After approval Dewi's quota reads "7 of 12 days left (5 used)" (3 earlier plus 2). Cuti Bersama and Libur Nasional cost nothing.
- **Result:** [ ]

### LV-09 (V) Half-day Annual
- **Rule (L2, L4):** Annual only; charges 0.5.
- **Login:** Dewi, then Tono.
- **Steps:** 1. **Request leave**, Annual, **Thu 6 Aug**, tick **Half Day**, choose **Morning**, Reason `Dentist`. 2. Note the charge line. 3. Submit; Tono approves.
- **Expect:** Line "1 workday(s) × half day = 0.5 day(s) deducted from quota". After approval quota shows 6.5 days left (6.5 of 12, with 5.5 used). **No** `On leave` attendance row is created for 6 Aug (half-days don't materialize).
- **Result:** [ ]

### LV-10 (I) Half day on a non-Annual type
- **Steps:** In **Request leave**, choose `Sick`, then `Permission`, then `Unpaid`.
- **Expect:** The **Half Day** toggle is not offered for any of them.
- **Blocked by:** UI. (Server code `leave.half_day_not_allowed`.)
- **Result:** [ ]

### LV-11 (V) Hourly Izin
- **Rule (L3–L4):** Izin only; whole hours, same side of lunch, at most Max Izin Hours (3); charges hours ÷ net day (8 h).
- **Login:** Dewi, then Tono.
- **Steps:** 1. **Request leave**, `Permission`, **Fri 7 Aug**, tick **Hourly**, Start Hour `09:00`, End Hour `11:00`, Reason `Bank`. 2. Check the line. 3. Submit; Tono approves.
- **Expect:** "1 workday(s) × 2 hour(s) = 0.25 day(s) deducted from quota". After approval the Permission quota reads 5.75 of 6.
- **Result:** [ ]

### LV-12 (I) Hourly range beyond Max Izin Hours
- **Steps:** Tick Hourly, set Start Hour `09:00`, then open End Hour.
- **Expect:** Only `10:00` and `11:00` are offered (12:00 is never offered, and 13:00 would be 4 hours from 09:00, over the cap of 3). With Start Hour `13:00` the offered ends are `14:00`, `15:00`, `16:00`.
- **Blocked by:** UI. (Server: "Izin cannot exceed 3 hour(s); this request spans 4." — `leave.izin_hours_exceeded`.)
- **Result:** [ ]

### LV-13 (I) Hourly range that crosses lunch
- **Rule (L3):** start and end must both be before lunch or both after it.
- **Login:** Dewi.
- **Steps:** Permission, **Mon 10 Aug**, Hourly, Start Hour `11:00`, End Hour `13:00`, Reason `Errand`. Submit.
- **Expect:** Toast "Failed to submit request": "Start and end hour must both fall before or both fall after the lunch hour." (`leave.hourly_range_crosses_lunch`).
- **Blocked by:** Server.
- **Result:** [ ]

### LV-14 (V) Sick leave with a doctor's note
- **Rule (L5):** Sick requires a PDF, JPG or PNG up to 10 MB; no other type takes one.
- **Login:** Sari, then Rina.
- **Steps:** 1. Sari: **Request leave**, `Sick`, **Mon 10 Aug – Tue 11 Aug**, Reason `Fever`. 2. Upload a small PDF named `doctor-note.pdf` under "Doctor's note". 3. Submit. 4. Rina approves. 5. Open the row details.
- **Expect:** Submission succeeds. Details show the attachment under "Doctor's note"; downloading works. Sari's Sick quota shows 28 of 30 left.
- **Result:** [ ]

### LV-15 (I) Sick leave without a note, wrong file, too big
- **Login:** Sari.
- **Steps:** (a) Type `Sick`, valid dates, reason, **no file**. (b) Upload `note.docx`. (c) Upload a file over 10 MB.
- **Expect:** (a) **Submit request** is disabled. (b) "That file type is not accepted." (c) "The file is larger than 10MB."
- **Blocked by:** UI.
- **Result:** [ ]

### LV-16 (I) Overlapping an approved leave
- **Rule (L14):** new leave can't share hours with approved leave.
- **Login:** Dewi.
- **Steps:** **Request leave** for Dewi, Annual, open the date picker around Aug 3–5.
- **Expect:** 3, 4 and 5 Aug are shown as unavailable and can't be picked. (Server message if forced: "The requested dates overlap an already approved leave." — `leave.overlaps_approved`.) Also check: a **morning half day** on a day that has only an **afternoon** Izin is allowed.
- **Blocked by:** UI.
- **Result:** [ ]

### LV-17 (V) Owner leave is auto-approved
- **Rule (L10, L24):** an Owner's own leave needs no approval and has no cap.
- **Login:** Owner.
- **Steps:** **Request leave** for the Owner, Annual, **Mon 14 Sep – Wed 16 Sep**, Reason `Holiday`. Submit.
- **Expect:** The row is **Approved** immediately. No Approve/Deny buttons. No quota warning.
- **Result:** [ ]

### LV-18 (V) Owner files for Staff
- **Rule (L10):** leave an Owner files for someone else is approved immediately.
- **Login:** Owner.
- **Steps:** Request leave for Sari, `Sick`, **Fri 14 Aug** (note: holiday-free workday), PDF attached, Reason `Checkup`. Submit.
- **Expect:** Approved immediately.
- **Result:** [ ]

### LV-19 (I) A Manager's own leave needs an Owner
- **Rule (L12):** Manager leave is decided only by an Owner.
- **Login:** Rina, then Tono, then Owner.
- **Steps:** 1. Rina files Annual for herself, **Mon 21 Sep**. 2. Open the row as Tono. 3. Open it as Owner.
- **Expect:** Tono sees **no Approve/Deny**. The Owner does. After approval, Rina's leave is Approved.
- **Blocked by:** UI.
- **Result:** [ ]

### LV-20 (V) Deny with a note
- **Login:** Owner.
- **Steps:** Open Tono's pending filing for Dewi from `LV-03` (Mon 24 Aug). Press **Deny**, type `Peak week` in Note, **Deny**.
- **Expect:** Toast "Leave denied". Status **Denied**; details show Decision note "Peak week". Denied is final.
- **Result:** [ ]

### LV-21 (V) Withdraw your own leave
- **Rule (L16):** the employee can cancel their own; the reason is recorded as "Withdrawn by employee".
- **Login:** Dewi.
- **Steps:** Create and get approved a new Annual request for **Mon 7 Sep** (Dewi files, Tono approves). As Dewi press **Cancel** on it, confirm **Cancel leave**.
- **Expect:** Toast "Leave cancelled". The row shows "Cancelled — Withdrawn by employee". Quota returns.
- **Result:** [ ]

### LV-22 (V) Manager recalls approved leave
- **Rule (L16):** cancelling someone else's leave is "Recalled for work".
- **Login:** Dewi, then Tono.
- **Steps:** Dewi files Annual **Tue 8 Sep**; Tono approves it, then presses **Cancel** and confirms.
- **Expect:** Status **Cancelled**, reason "Recalled for work". If Dewi had punched that day the punch stays and wins the attendance status.
- **Result:** [ ]

### LV-23 (I) Colleagues can't cancel each other's leave
- **Login:** Rina.
- **Steps:** Look at Dewi's approved row in the Leave list.
- **Expect:** No **Cancel** button (Rina is not Dewi's Manager).
- **Blocked by:** UI.
- **Result:** [ ]

### LV-24 (V) Pending request cap per month (optional, repetitive)
- **Rule (L13):** at most 10 undecided requests per employee per calendar month of filing.
- **Login:** Dewi.
- **Steps:** File the same 1-day Annual request for **Mon 31 Aug** ten times. Then file an 11th.
- **Expect:** Requests 1–10 succeed. The 11th fails: "Dewi Staff already has 10 leave request(s) awaiting a decision this month. Wait for one to be decided first." (`leave.pending_limit`). Withdraw all ten afterwards.
- **Blocked by:** Server.
- **Result:** [ ]

---

## Part 2 — Editing

### LV-25 (V) Manager edits approved dates
- **Rule (L17):** the editor needs standing to decide; only dates change.
- **Login:** Tono.
- **Steps:** 1. Open Dewi's approved 3–5 Aug row; press **Edit** ("Edit Leave Dates"). 2. Change the end date to **Tue 4 Aug**. 3. Press **Save Changes**.
- **Expect:** Toast "Leave updated". Workdays 2. Details show "Edited By: Tono Manager on … — previously 3 Aug – 5 Aug". Quota returns one day.
- **Result:** [ ]

### LV-26 (V) Owner edits a Pending request: auto-approves
- **Login:** Tono, then Owner.
- **Steps:** Tono files Annual for Dewi, **Mon 31 Aug**. Owner opens it → **Edit** → change to **Tue 1 Sep** → **Save Changes**.
- **Expect:** The request is now **Approved**. (A Manager's edit of a pending request does not approve it.)
- **Note:** Don't use Budi here; his Annual quota ordering matters for `LV-34`.
- **Result:** [ ]

### LV-27 (I) Edit into a conflict
- **Login:** Tono.
- **Steps:** Edit Dewi's approved 14–19 Aug Annual leave and move its start date to **Fri 7 Aug**. Dewi already has an approved Izin 09:00–11:00 on 7 Aug, and a full-day Annual overlaps those hours.
- **Expect:** "Could not update leave": "The requested dates overlap an already approved leave." (`leave.overlaps_approved`). The request keeps its old dates.
- **Blocked by:** Server (the date picker may also grey 7 Aug out, which also passes).
- **Result:** [ ]

### LV-28 (I) Edit button hidden for outsiders
- **Login:** Rina.
- **Steps:** Look at Dewi's row.
- **Expect:** No **Edit** (Rina has no standing over Dewi).
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 3 — Visibility

### LV-29 (V) Details are private, existence is public
- **Rule (L20):** everyone sees that someone is away; type, reason and note are for the employee, their decider and Owners.
- **Login:** Tono, then Rina.
- **Steps:** 1. As Tono, file Annual leave for himself on **Mon 28 Sep**, Reason `Medical checkup`; the Owner approves it. 2. Sign in as Rina and look at the Leave list.
- **Expect:** Rina sees the row with type **Away** and the details dialog shows "Not shown" for reason. Tono and the Owner see the real type and reason.
- **Result:** [ ]

---

## Part 4 — Quota, probation, Unpaid

### LV-30 (I) Annual leave during probation
- **Rule (L26):** a probationer has no Annual leave; refused at filing, approval and edit.
- **Login:** Sari.
- **Steps:** **Request leave**, `Annual`, **Tue 22 Sep**, Reason `Trip`. Submit.
- **Expect:** "Failed to submit request": "Sari Probation is on probation until 2026-12-01 and has no annual leave yet." (`leave.probation_annual`).
- **Blocked by:** Server.
- **Result:** [ ]

### LV-31 (V) Unpaid leave while on probation
- **Rule (L27):** Unpaid is for probationers only; every Unpaid day is cut from salary (see `05`).
- **Login:** Sari, then Rina.
- **Steps:** Sari files `Unpaid`, **Wed 12 Aug – Thu 13 Aug**, Reason `Personal`. Rina approves.
- **Expect:** Approved. Quota line: Unpaid "28 of 30 days left (2 used)". The form shows a warning like "2 of these days are over your Unpaid quota and will be cut from your salary" (Unpaid is cut in full).
- **Result:** [ ]

### LV-32 (I) Unpaid leave for a confirmed employee
- **Login:** Dewi.
- **Steps:** Request leave, `Unpaid`, **Wed 9 Sep**, Reason `Personal`. Submit.
- **Expect:** "Failed to submit request": "Dewi Staff is not on probation; unpaid leave does not apply." (`leave.unpaid_not_on_probation`).
- **Blocked by:** Server.
- **Result:** [ ]

### LV-33 (I) Unpaid past the 30-day cap
- **Rule (L28):** Unpaid is the one type still hard-capped.
- **Login:** Sari.
- **Steps:** Request leave, `Unpaid`, **Mon 14 Sep – Fri 30 Oct** (35 workdays), Reason `Long absence`. Submit.
- **Expect:** "Only 28 Unpaid day(s) remain for 2026; this request uses 35." (`leave.quota_exceeded`).
- **Blocked by:** Server.
- **Result:** [ ]

### LV-34 (V) Going over quota is allowed and warned
- **Rule (L28–L29):** Annual, Sick and Izin overflow is allowed; the extra days are cut from salary.
- **Login:** Rina (on behalf of Budi), then Owner.
- **Steps:** 1. Rina: **Request leave**, Employee `Budi Staff`, Annual, **Mon 6 Jul – Wed 8 Jul**, Reason `Wedding`. 2. Read the warning under the form. 3. Submit. 4. Owner approves.
- **Expect:** Warning "2 of these days are over Budi Staff's Annual quota and will be cut from their salary." Request submitted; once approved the row shows the badge "2 days over quota (salary cut)". (Budi's Annual quota is 1, from `S5`.) Rina and Budi see days only, never rupiah.
- **Note:** This request is the base for the July payroll tests in `05`.
- **Result:** [ ]

### LV-35 (V/I) Owner sets and clears a quota override
- **Login:** Owner.
- **Steps:** 1. **Employees → Dewi Staff → Probation & leave quota → Leave quota overrides**, Annual `20`, save. 2. Request balance in **Request leave** for Dewi. 3. Enter `-1` and save. 4. Set `0` and save. 5. Clear the override by resetting to Default.
- **Expect:** (1) "Changes saved"; balance shows 20. (3) Rejected: "Entitled days cannot be negative." (`employee.quota_negative`) or the field blocks negative numbers. (4) Legal; balance shows 0 of 0 and new Annual requests become "over quota" days. (5) Back to 12.
- **Blocked by:** UI or Server for (3).
- **Result:** [ ]
- **Note:** Reset Dewi to Default (12) after this case; later cases assume it.

---

## Part 5 — Probation extensions

### PRB-01 (V) Manager requests an extension
- **Rule (L31):** a Manager may request more probation for their own direct Staff.
- **Login:** Rina.
- **Steps:** 1. **Probation → Request extension**. 2. Employee `Sari Probation`, New end date **2027-01-01**, Reason `Needs more coaching`. 3. **Submit request**.
- **Expect:** Toast "Request submitted — An owner will decide on the extension." Row shows Ends now 2026-12-01 → Proposed 2027-01-01, **Pending**.
- **Result:** [ ]

### PRB-02 (I) Not a later date
- **Rule:** the proposed date must be later than the current end.
- **Login:** Rina.
- **Steps:** Try a second request with New end date **2026-11-01**.
- **Expect:** The hint says "Must be later than the current probation end date." Failure text: "The proposed date must be later than the current probation end date." (`probation.not_an_extension`). *(If PRB-01 is still pending, you will instead meet PRB-03; do this case after PRB-05 or use a fresh employee.)*
- **Blocked by:** UI or Server.
- **Result:** [ ]

### PRB-03 (I) Only one pending request per employee
- **Login:** Rina.
- **Steps:** With PRB-01 pending, request another extension for Sari, **2027-02-01**.
- **Expect:** "Failed to submit request": "This employee already has a pending probation extension request." (`probation.pending_exists`).
- **Blocked by:** Server.
- **Result:** [ ]

### PRB-04 (I) Other Manager's Staff / an Owner
- **Login:** Rina, then Owner.
- **Steps:** (a) Rina opens the Employee list in **Request extension**. (b) Owner opens **Probation**.
- **Expect:** (a) Only Rina's direct Staff on probation (Sari) is selectable; Dewi is not. If forced: "You can only request a probation extension for your own direct staff." (b) The Owner has no **Request extension** button; Owners edit the date directly.
- **Blocked by:** UI.
- **Result:** [ ]

### PRB-05 (V) Owner approves
- **Login:** Owner.
- **Steps:** **Probation** → Sari's row → **Approve** ("Move Sari Probation's probation end from 2026-12-01 to 2027-01-01?") → **Approve**.
- **Expect:** Toast "Extension approved". Open **Employees → Sari Probation → Probation & leave quota**: Probation ends **2027-01-01**, shown as "Set by owner".
- **Result:** [ ]

### PRB-06 (V) Deny, and withdraw
- **Login:** Rina, Owner.
- **Steps:** Rina files another extension (e.g. to **2027-02-01**). (a) Owner presses **Deny** with a note. File a third; (b) Rina presses **Withdraw**.
- **Expect:** (a) "Extension denied". (b) "Request withdrawn". Both are final.
- **Result:** [ ]

### PRB-07 (I) Extension for a confirmed employee
- **Login:** Rina.
- **Steps:** Try to file an extension for **Budi Staff** (confirmed).
- **Expect:** Budi is not selectable (only people on probation). If forced: "Budi Staff is no longer on probation." (`probation.already_confirmed`).
- **Blocked by:** UI.
- **Result:** [ ]

### PRB-08 (V) Owner overrides the date directly
- **Login:** Owner.
- **Steps:** **Employees → Sari Probation → Probation & leave quota**. In "Change Probation End" pick **2026-12-15** and save. Then press **Clear**.
- **Expect:** First save shows "Probation ends 2026-12-15 — Set by owner"; **Clear** restores the three-month default (2026-12-01, "Default"). The annual entitlement note: ending in December gives 0 Annual days this year.
- **Result:** [ ]
- **Note:** After PRB-05 Sari's date was moved to 2027-01-01; Clear returns it to the default 2026-12-01. Fine for later cases (she stays on probation).
