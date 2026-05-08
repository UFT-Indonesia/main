import { getRequestConfig } from 'next-intl/server';
import { cookies } from 'next/headers';
import type { AbstractIntlMessages } from 'next-intl';

export const SUPPORTED_LOCALES = ['id', 'en'] as const;
export type Locale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: Locale = 'id';

export default getRequestConfig(async () => {
  const cookieStore = await cookies();
  const requested = cookieStore.get('NEXT_LOCALE')?.value;
  const locale: Locale = (SUPPORTED_LOCALES as readonly string[]).includes(requested ?? '')
    ? (requested as Locale)
    : DEFAULT_LOCALE;

  const messages = (await import(`../../messages/${locale}.json`))
    .default as AbstractIntlMessages;

  return {
    locale,
    messages,
    timeZone: 'Asia/Jakarta',
    now: new Date(),
  };
});
