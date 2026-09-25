# GSS07 — Locale-Aware Date Formatting

Follow-up split out of the GSS05 PR review (2026-09-25, finding #8). **Not started.**

## The gap

The app ships `en` and `id` messages, and `app/layout.tsx` resolves the locale with
`getLocale()` — but every date/time formatter hardcodes `'id-ID'`. An English user still gets
Indonesian weekday and month names (`Rab, 05 Agu 2026`); it only goes unnoticed for months
spelled the same in both languages, like "September".

13 hardcoded `'id-ID'` call sites across 10 files:

- `app/attendance/page.tsx` (`formatMonth`, new in GSS05)
- `app/attendance/devices/page.tsx`
- `app/employees/audit-log/page.tsx`
- `components/attendance/attendance-calendar-table.tsx`
- `components/attendance/view-log-details-dialog.tsx`
- `components/ui/date-picker.tsx`
- `components/probation/probation-dialogs.tsx`
- `components/leave/leave-dialogs.tsx`
- `components/employees/audit-log-summary.tsx`
- `components/employees/employee-table.tsx`

## Scope

Replace the literal with the active locale (`useLocale()` from `next-intl`, or its
`useFormatter()` which already carries it) in one pass across all sites. Fixing one file alone
would make the app inconsistent, which is why GSS05 left `formatMonth` as it was.

## Open question

- Is English actually used by anyone? If not, dropping `en.json` is the lazier fix.
