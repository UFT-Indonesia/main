# 04 — Gaji Premi (Overtime Pay Periods, Close, Rapel)

Phases A, B and C. Pages: **Gaji Premi** (`/payroll/gaji-premi`, Owner only) and the **Overtime →
Gaji Premi Saya** tab (Staff). Run after `03-overtime.md`.

A period is two calendar months starting in an odd month (Jan–Feb, Mar–Apr, …). The payout is on the
15th of the month after the period ends. Closing is Owner-only and irreversible.

## Periods used here

| Period | Start | Ends | Payout | Role in this guide |
|---|---|---|---|---|
| Mar–Apr | 1 Mar | 30 Apr | 15 May | First close; holds OT item 10 |
| May–Jun | 1 May | 30 Jun | 15 Jul | Empty; used for the "earlier period still open" check |
| **Jul–Aug** | 1 Jul | 31 Aug | **15 Sep** | Main period, closed in Phase B |
| Sep–Oct | 1 Sep | 31 Oct | 15 Nov | Still open on 13 Oct; "not ended" check and where rapel is paid |

## Expected figures for Jul–Aug (Budi), if every OT case in `03` ran as written

| Month | OT days | Hours | Notes |
|---|---|---|---|
| July | 7 | 16 | 13th 3 h, 14th 2 h, 15th 1 h, 16th 2 h, 18th 6 h, 19th 2 h, 21st 0 h |
| August | 1 | 3 | 17th 3 h |
| Money | | | July Rp 170.000, August Rp 25.000, total **Rp 195.000** |

Rina also has one approved OT (Wed 29 Jul 18:30–20:30, no punches) = 1 day, 0 h, Rp 0. Only **Approved** assignments
count as days.

---

## Part 1 — Reading the period (Phase A)

### GP-01 (V) Period table
- **Rule (G1–G3):** an open period is computed live and labelled an estimate.
- **Login:** Owner.
- **Steps:** 1. **Gaji Premi**. 2. Period picker → **Jul–Aug 2026**. 3. Find Budi's row. 4. Click it to open the day-by-day list.
- **Expect:** "Open" badge and "Estimate, not final". Budi row matches the table above. Column headers read "OT Days (July)", "Hours (July)" etc. Day list shows window, punches, counted hours, tier and status. Payout reads "Payout 15 Sep 2026".
- **Result:** [ ]

### GP-02 (I) Period not ended
- **Rule (G4):** a period closes only after it has ended.
- **Login:** Owner.
- **Steps:** Pick **Sep–Oct 2026**. Hover the **Close Period** button.
- **Expect:** The button is disabled, with the title "A period closes only after it ends, and only after the earlier ones." (Server code `gaji_premi.not_ended`: "A period can only be closed after it has ended.") This stays true until 1 Nov 2026.
- **Blocked by:** UI.
- **Result:** [ ]

### GP-03 (I) An earlier period still has open overtime
- **Rule (G4):** periods close in order.
- **Login:** Owner.
- **Steps:** Pick **May–Jun 2026** (ended, empty). Note the **Close Period** button state, because **Mar–Apr** still holds Budi's open OT (item 10).
- **Expect:** **Close Period** is disabled with the same hint, because open overtime exists earlier (server code `gaji_premi.close_in_order`: "An earlier period still has open overtime; close it first.").
- **Blocked by:** UI.
- **Result:** [ ]

### GP-04 (I) Non-owners can't see it
- **Login:** Rina, Budi.
- **Steps:** Look for **Gaji Premi** in the sidebar; open `/payroll/gaji-premi` directly.
- **Expect:** No sidebar item. The page shows the server refusal "Only an Owner can see Gaji Premi." (or an access-denied view).
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 2 — Closing (Phase B, irreversible)

Before closing Jul–Aug make sure these exist, because the close **expires** them:
a **Pending** OT assignment with punches (28 Jul), a **Pending** correction request (21 Jul).

### GP-05 (V) Close Mar–Apr
- **Rule (G4):** close freezes counted hours and pay; periods close in order.
- **Login:** Owner.
- **Steps:** 1. Pick **Mar–Apr 2026**. 2. **Close Period** → read the dialog ("Mar–Apr … will be frozen … This cannot be undone.") → **Close Period**.
- **Expect:** Toast "Period closed — Counted hours and pay are now frozen." Badge **Closed**, "Closed by System Owner". Budi's March line: 1 OT day, 2 h, Rp 25.000.
- **Result:** [ ]

### GP-06 (I) A closed period refuses new work
- **Rule (G5):** after close, anything dated in the period is refused; only a rapel gets in.
- **Login:** Owner.
- **Steps:** **Overtime → Assign Overtime**, Budi, date **Tue 3 Mar 2026**.
- **Expect:** "Could not create the OT assignment": "That date is in a closed Gaji Premi period; only a rapel can still claim it." (`overtime.period_closed`). Also: the frozen 2 Mar OT offers no **Edit End Time** or **Cancel OT** (server: "This overtime's period is closed.").
- **Blocked by:** Server.
- **Result:** [ ]

### GP-07 (V) Close Jul–Aug
- **Rule (G4):** close expires undecided OT and corrections; punches stay claimed; Pending OT pays Rp 0.
- **Login:** Owner.
- **Steps:** 1. Pick **Jul–Aug 2026** and confirm the Close button is enabled now that Mar–Apr is closed. 2. Note the status of the 28 Jul OT (Pending) and the 21 Jul correction (Pending). 3. **Close Period** and confirm.
- **Expect:** Toast "Period closed". Budi's totals freeze at Rp 195.000. The 28 Jul OT now reads **Expired** with Pay Rp 0, its punches still claimed (they do not return to the regular day). The 21 Jul correction reads **Expired** under **Corrections**. The 21 Jul OT stays Approved with 0 frozen hours.
- **Result:** [ ]

### GP-08 (I) Closing twice
- **Steps:** Re-open Jul–Aug.
- **Expect:** No Close button, or it is disabled. (Server: "This period is already closed." — `gaji_premi.already_closed`.)
- **Blocked by:** UI.
- **Result:** [ ]

---

## Part 3 — Rapel (Phase C)

A **rapel** is a late claim on an *approved* OT assignment of a *closed* period. Budi's 21 Jul OT froze
at 0 hours (missing tap-out), so it is the natural claim.

**Deadline note.** An employee can request a rapel until one month after the payout date. For Jul–Aug
that is **15 Oct 2026**. If you run this after 15 Oct, run `GP-12` instead of `GP-09`.

### GP-09 (V) Employee requests a rapel
- **Rule (G6):** claimed times in, system suggests the amount; the employee never enters an amount.
- **Login:** Budi.
- **Steps:** 1. **Overtime → Gaji Premi Saya**. 2. On the **Jul–Aug** row press **Request Rapel**. 3. Overtime: pick the **21 Jul** one. 4. Worked From `18:30`, Worked Until `20:30`. 5. Note `Reader failed, supervisor saw me`. 6. Upload a PDF/JPG/PNG as WA Proof. 7. Read the summary line. 8. **Send Request**.
- **Expect:** Summary like "21 Jul, 18:30–20:30: 2 counted hours. Suggested Rp 25.000 (tier minus what was already paid)." Toast "Rapel requested — An Owner will decide it." The row's Rapel column shows a claim "Claim · OT 21 Jul".
- **Result:** [ ]

### GP-10 (I) A claim that adds nothing
- **Rule (G6):** a claim paying no more than already frozen is refused.
- **Login:** Budi.
- **Steps:** Request Rapel for the **15 Jul** OT (frozen Rp 0, left early), claim `18:30`–`19:30` (1 hour).
- **Expect:** "Rapel failed": "These hours pay no more than this overtime already paid." (`rapel.adds_nothing`). One hour is tier Rp 0.
- **Blocked by:** Server.
- **Result:** [ ]

### GP-11 (I) One rapel per OT assignment
- **Login:** Budi.
- **Steps:** Request Rapel again for **21 Jul**.
- **Expect:** "Rapel failed": "This overtime already has a rapel." (`rapel.duplicate`).
- **Blocked by:** Server.
- **Result:** [ ]

### GP-12 (I) After the deadline
- **Rule (G8):** employee requests close one month after the payout date; the Owner can still add one.
- **Run on:** any day after **15 Oct 2026**.
- **Login:** Budi, then Owner.
- **Steps:** (a) Budi: **Gaji Premi Saya** → Jul–Aug row. (b) Owner: **Gaji Premi → Add Rapel** (see `GP-16`).
- **Expect:** (a) **Request Rapel** is visible but disabled, with "The request deadline (one month after payout) has passed. Contact the Owner." (server: `rapel.deadline_passed`). (b) The Owner's add is not time-limited.
- **Blocked by:** UI.
- **Result:** [ ]

### GP-13 (V) Owner approves with the suggested amount
- **Rule (G7):** only an Owner decides; an approved rapel is paid on the next open period.
- **Login:** Owner.
- **Steps:** 1. **Gaji Premi**, open the **Rapel Requests** list. 2. Budi's claim → **Approve** ("Pay Budi Staff this amount on the next payout."). 3. Leave the amount as suggested. 4. **Approve**. 5. Pick **Sep–Oct 2026**.
- **Expect:** Toast "Rapel approved". **Sep–Oct** shows Budi with a rapel line "Rapel · OT 21 Jul" of Rp 25.000, and the period total includes it. **Jul–Aug** totals do not change (it is closed). Budi's **Gaji Premi Saya** shows the line under the Sep–Oct row.
- **Result:** [ ]

### GP-14 (V) Owner rejects a claim
- **Rule (G7):** the Owner may reject; a rejected claim stays in history and no longer blocks a new one.
- **Login:** Budi, then Owner.
- **Steps:** 1. Budi: **Request Rapel** for the **15 Jul** OT, From `18:30`, Until `21:30` (3 h; suggested Rp 25.000), note, proof, **Send Request**. 2. Owner: **Gaji Premi** → **Rapel Requests** → that claim → **Reject** → note `No evidence` → **Reject**.
- **Expect:** Toast "Rapel rejected". On Budi's **Gaji Premi Saya** the claim shows as rejected with the Owner's note; the Sep–Oct total is unchanged. Budi can file a new claim for 15 Jul afterwards (only Pending and Approved count toward "one per OT").
- **Result:** [ ]

### GP-15 (I) Only an Owner decides
- **Login:** Rina.
- **Steps:** Look for Approve/Reject on a rapel request.
- **Expect:** Nowhere to do it: Managers have no Gaji Premi page and no rapel buttons. (Server: "Only an Owner decides a rapel.")
- **Blocked by:** UI.
- **Result:** [ ]

### GP-16 (V/I) Owner adds a rapel directly, with an override
- **Rule (G7):** the Owner may add one with the same fields; it is approved on the spot and may override the amount.
- **Login:** Owner.
- **Steps:** 1. **Gaji Premi** → **Jul–Aug** → **Add Rapel** (title "Add Rapel"). 2. Overtime: Rina's **29 Jul** one. 3. Worked From `18:30`, Worked Until `20:30`. 4. Amount (Rp) `40000`. 5. Note and WA Proof. 6. **Add Rapel**.
- **Invalid checks:** (I1) Pick **Sep–Oct** (open): **Add Rapel** is disabled. (I2) In the dialog, enter Amount `0`.
- **Expect:** Toast "Rapel added". The line is approved immediately and appears on **Sep–Oct** as Rina's "Rapel · OT 29 Jul" of Rp 40.000. (I1) Disabled. (I2) The server answers "Rapel amount must be greater than zero." (`rapel.amount`).
- **Blocked by:** UI (I1); Server (I2).
- **Result:** [ ]

### GP-17 (I) Only closed, approved OT can be claimed
- **Login:** Owner.
- **Steps:** Open **Add Rapel** on **Jul–Aug** and open the Overtime list. Then try the **28 Jul** Expired OT.
- **Expect:** The list only shows Approved OT of that period; the Expired and Rejected ones are absent. If the list is empty: "No approved overtime in this period." Server codes: `rapel.not_claimable`, `rapel.period_open`.
- **Blocked by:** UI.
- **Result:** [ ]

### GP-18 (V) The employee's own table
- **Login:** Budi.
- **Steps:** **Overtime → Gaji Premi Saya**.
- **Expect:** One row per period, newest first: Sep–Oct (marked "Estimate, not final", with Budi's Rp 25.000 rapel line), Jul–Aug (Closed; Rp 195.000 plus claim history), Mar–Apr (Closed; Rp 25.000). Rina and the Owner do not have this tab.
- **Result:** [ ]
