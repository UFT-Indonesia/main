# GSS07 — Locale-Aware Date Formatting

Follow-up split out of the GSS05 PR review (2026-09-25, finding #8). **Implemented on
`refactor/locale-aware-formatting`; not yet checked in a running app.**

## Outcome (grilling session, 2026-09-26)

- English stays. The translations are complete and the architecture spec promises it. There is no
  language switcher yet (English only via the `NEXT_LOCALE` cookie) — a separate task.
- English dates use `en-GB`, not `en`: day-first and a 24-hour clock, same as the Indonesian view
  and the date pickers (`providers.tsx` already pins those to `en-GB`).
  `Rab, 05 Agu 2026 14.30` → `Wed, 05 Aug 2026 14:30`.
- Money stays `id-ID` for every language: salaries are Rupiah (`Rp 5.500.000`). The 3 currency
  sites in `audit-log-summary.tsx` and `employee-table.tsx` are untouched.
- `hooks/use-date-locale.ts` maps the app language to the Intl locale. The 10 date sites read it.
- `formatLeaveDate` is used ~30 times across 9 files the list below missed. It became
  `useFormatLeaveDate()`, so every caller follows the language too.

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

- ~~Is English actually used by anyone? If not, dropping `en.json` is the lazier fix.~~ Kept —
  see Outcome.
