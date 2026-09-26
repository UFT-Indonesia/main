import { useLocale } from 'next-intl';

export type DateLocale = 'id-ID' | 'en-GB';

/**
 * The Intl locale for dates and times, following the app language. English maps to en-GB, not
 * en: day-first order and a 24-hour clock, same as the Indonesian view and the date pickers.
 * Money stays id-ID regardless — salaries are Rupiah.
 */
export function useDateLocale(): DateLocale {
  return useLocale() === 'en' ? 'en-GB' : 'id-ID';
}
