# 01 — Attendance Policy, Holidays and Attendance

Phase A. Run after `00-test-bed-setup.md`. Pages: **Attendance policy** (`/attendance/settings`),
**Attendance** (`/attendance`), **Attendance devices** (`/attendance/devices`).

---

## Part 1 — Attendance policy

### CAL-01 (V) Save a valid policy
- **Rule (C5–C7):** policy fields validate and every change is recorded in history.
- **Login:** Owner.
- **Steps:** 1. Open **Attendance policy**. 2. Change Clock-in grace to `20`, press **Save changes**. 3. Change it back to `15` and save again.
- **Expect:** Toast "Policy updated — Attendance days are being recomputed under the new policy." both times. The form shows the saved values after a reload.
- **Result:** [ ]

### CAL-02 (I) Shift ends after 18:00
- **Rule (C6):** a shift can't end later than 18:00, so the 18:30 OT start never overlaps regular hours.
- **Login:** Owner.
- **Steps:** Set Shift end `18:30`. Save.
- **Expect:** Toast "Failed to update policy": "Shift cannot end after 18:00." (`attendance_policy.shift_end_too_late`). Values stay as before.
- **Blocked by:** Server.
- **Result:** [ ]

### CAL-03 (I) Shift start not before end
- **Steps:** Shift start `17:00`, Shift end `08:00`. Save.
- **Expect:** "Shift start must be before shift end." (`attendance_policy.shift_window`).
- **Blocked by:** Server.
- **Result:** [ ]

### CAL-04 (I) Shift too short
- **Rule (C6):** a shift must be longer than 60 minutes.
- **Steps:** Shift start `08:00`, Shift end `08:45`. Save.
- **Expect:** "Shift must be longer than 60 minutes, to leave room for the assumed 1-hour lunch." (`attendance_policy.shift_too_short`).
- **Blocked by:** Server.
- **Result:** [ ]

### CAL-05 (I) Negative grace, zero Max Izin, bad time zone
- **Steps (three separate saves):** (a) Clock-in grace `-5`. (b) Max Izin Hours `0`. (c) Time zone `Mars/Base`.
- **Expect:** (a) "Clock-in grace minutes must be zero or positive." (b) "Max Izin hours must be positive." (c) "Time zone must be a valid IANA time zone id." Each is a red "Failed to update policy" toast, or an inline form error that stops the submit.
- **Blocked by:** UI or Server.
- **Result:** [ ]

### CAL-06 (I) Staff can't open the policy page
- **Rule:** only Owner and Manager can edit the policy.
- **Login:** Budi.
- **Steps:** Check the sidebar, then open `/attendance/settings` directly.
- **Expect:** No "Attendance policy" item in the nav (verify), and the page shows "You don't have permission to view this page."
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 2 — Holidays (on the same page, "Holidays" card)

Declare these first. Later leave and OT cases depend on them.

### CAL-07 (V) Declare a national holiday
- **Rule (C2):** a holiday is one named date; "Libur Nasional" or "Cuti Bersama".
- **Login:** Owner.
- **Steps:** 1. In **Holidays**, use "Next month" until August 2026 shows. 2. Click **17**. 3. Name `HUT RI`, Type `Libur Nasional`. 4. **Add Holiday**.
- **Expect:** Toast "Holiday saved — Leave counts on this date are being updated." 17 Aug is marked on the calendar.
- **Result:** [ ]

### CAL-08 (V) Declare a Cuti Bersama
- **Steps:** Click **18** in August, Name `Cuti Bersama Test`, Type `Cuti Bersama`, **Add Holiday**.
- **Expect:** Saved; 18 Aug marked, and the kind label reads "Cuti Bersama".
- **Note:** Both kinds behave identically in every rule. Cuti Bersama never touches leave quota and is never cut from salary.
- **Result:** [ ]

### CAL-09 (V) Rename an existing holiday
- **Steps:** Click 18 Aug → change Name to `Cuti Bersama` → **Save Changes**.
- **Expect:** "Holiday saved". Nothing else changes (renaming recounts nothing).
- **Result:** [ ]

### CAL-10 (V) Remove a holiday
- **Steps:** 1. Declare `Temp` as Libur Nasional on **Fri 28 Aug**. 2. Click 28 Aug again → **Remove**.
- **Expect:** Toast "Holiday removed". 28 Aug is a normal workday again.
- **Result:** [ ]

### CAL-11 (I) Holiday with empty or oversized name
- **Rule:** name required, at most 100 characters.
- **Steps:** (a) Click 20 Aug, leave Name empty, try **Add Holiday**. (b) Enter 101 characters, try again.
- **Expect:** (a) "Holiday name is required." (`holiday.name_required`) or the button stays disabled. (b) "Holiday name cannot exceed 100 characters." (`holiday.name_length`).
- **Blocked by:** UI or Server.
- **Result:** [ ]

> The "holiday in a closed payroll month" refusal is tested after July closes: see `05-potongan-cuti.md` PC-14.

---

## Part 3 — Punches and day status

Sign in as **Rina** (Manager of Budi and Sari) unless stated. Open **Attendance**, choose **August 2026** in the
month dropdown. The table has one row per date; press the arrow on a date to expand its employees.

### ATT-01 (V) Complete day with two manual punches
- **Rule (A5–A6):** first punch is tap-in, last is tap-out; on time = tap-in ≤ 08:15 and tap-out ≥ 16:45.
- **Login:** Rina.
- **Steps:** 1. **Add manual log**. 2. Employee `Budi Staff`, Date & time `2026-08-03 08:00`, Type `Clock in`, **Save log**. 3. Again: `2026-08-03 17:05`, Type `Clock out`, **Save log**. 4. Expand Mon 03-08 and find Budi.
- **Expect:** Toast "Log saved — Attendance log recorded successfully." Budi shows In 08:00, Out 17:05, status **Complete**.
- **Result:** [ ]

### ATT-02 (V) Grace boundary is inclusive
- **Steps:** For Budi on **Wed 5 Aug**: In `08:15`, Out `17:00`.
- **Expect:** **Complete** (08:15 is exactly start plus grace).
- **Result:** [ ]

### ATT-03 (I) One minute too late
- **Steps:** For Budi on **Tue 4 Aug**: In `08:16`, Out `17:00`.
- **Expect:** **Incomplete**. The In cell reads 08:16.
- **Result:** [ ]

### ATT-04 (I) Only one punch
- **Rule (A5–A6):** tap-out exists only with two or more punches; one punch is Incomplete.
- **Steps:** For Budi on **Thu 6 Aug**: In `08:00` only.
- **Expect:** **Incomplete**, Out shows `—`.
- **Result:** [ ]

### ATT-05 (V) Absent is derived, never stored
- **Rule (A13):** employed, on a workday, no row = Absent.
- **Steps:** Look at **Mon 10 Aug** (leave Budi with no punches).
- **Expect:** The date row's **Absent** count includes Budi; expanded, his row reads **Absent** and sorts at the top (problems first).
- **Result:** [ ]

### ATT-06 (I) Weekends and holidays are never absent
- **Rule (A14):** nobody is absent from a day nobody works.
- **Steps:** Look at **Sat 8 Aug**, **Mon 17 Aug** (holiday) and **Tue 18 Aug** (holiday).
- **Expect:** Every cell on those rows shows `–`, text muted. No Absent counts.
- **Result:** [ ]

### ATT-07 (V) Punch on a day off without OT
- **Rule (A14):** punches on a day off with no OT assignment read "Worked On Day Off" and are not judged.
- **Steps:** Add manual logs for Budi on **Mon 17 Aug**: In `09:00`, Out `12:00`. Expand 17 Aug.
- **Expect:** Budi's status is **Worked On Day Off**.
- **Result:** [ ]

### ATT-08 (V/suspect) Multi-day leave on the calendar
- **Rule (A7):** one `On leave` row is created, on the first workday of approved leave.
- **Run after:** `LV-02` (Dewi's Annual leave, 3–5 Aug, approved).
- **Login:** Rina or Owner.
- **Steps:** Expand **Mon 3 Aug**, **Tue 4 Aug** and **Wed 5 Aug** and find Dewi.
- **Expect (by design):** 3 Aug shows **On leave** with an Annual badge. 4 and 5 Aug show nothing special for her.
- **Suspected defect:** 4 and 5 Aug read **Absent**, because no row exists and the calendar only counts rows. If so, record it; leave days are being counted as absences. Compare the Absent and On Leave counts on those dates.
- **Result:** [ ]

### ATT-09 (V) Problems-only toggle
- **Steps:** Turn on **Only Dates With Problems**.
- **Expect:** Weekends, holidays, future dates, and today-in-progress disappear. Dates with Absent or Incomplete rows remain.
- **Result:** [ ]

### ATT-10 (V) Today is in progress
- **Rule (A15):** today reports arrivals until shift end plus grace (17:15).
- **Steps:** Open the current month, look at today's row (before 17:15 Jakarta).
- **Expect:** Summary reads "N clocked in · M not in yet" with an **In progress** marker. After 17:15 it flips to the normal five count columns.
- **Result:** [ ]

### ATT-11 (V) Export the period
- **Steps:** Press **Export Period**.
- **Expect:** A CSV downloads for the shown month. It includes absent rows and the leave type column, and **no salary or rupiah** column (Staff and Managers can run it).
- **Result:** [ ]

### ATT-12 (I) Staff can't write attendance
- **Rule (A3):** Staff never add or edit punches.
- **Login:** Budi.
- **Steps:** Open **Attendance** in August.
- **Expect:** No **Add manual log** button and no Add Punch in the details dialog. Only Budi's own rows appear.
- **Blocked by:** UI.
- **Result:** [ ]

### ATT-13 (I) Manager can't write outside their line
- **Rule (A3):** a Manager writes only for themselves and their direct Staff.
- **Login:** Rina.
- **Steps:** **Add manual log** → open the Employee list.
- **Expect:** Dewi Staff (Tono's) and Tono are not selectable; if they are, saving shows "Failed to save log": "You cannot record attendance for this employee."
- **Blocked by:** UI or Server.
- **Result:** [ ]

### ATT-14 (V) Edit a punch, and note the suspect
- **Rule (A3):** a writer can correct a punch; the day recomputes.
- **Login:** Rina.
- **Steps:** 1. Open Budi's 4 Aug details dialog ("View log details"). 2. Press **Edit** on the 08:16 punch and change it to `08:10`. Save.
- **Expect:** Toast "Changes saved". 4 Aug now shows **Complete**.
- **Suspect:** the edit handler doesn't check the punch's source. If you have a **Device** punch, try to edit it too and record whether it is allowed.
- **Result:** [ ]

### ATT-15 (V) Punch notes
- **Steps:** In the details dialog, open **Notes**, write `Forgot card`, press **Add**; then delete it.
- **Expect:** The note appears, then disappears. Failure shows "Note action failed".
- **Result:** [ ]

### ATT-16 (V/I) Attendance devices are Owner only
- **Login:** Owner, then Rina.
- **Steps:** (Owner) **Attendance devices → Register device**, Device id `test-reader-1`, Name `Front door`, **Register**. (Rina) Open the same page.
- **Expect:** (Owner) A "Device secret" dialog shows once ("Shown once. It cannot be retrieved again"). Device appears Enabled; toggle with **Disable device**. (Rina) "Only an Owner can manage attendance devices."
- **Result:** [ ]
