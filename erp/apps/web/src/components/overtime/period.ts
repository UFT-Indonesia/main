/** Year and month of a "YYYY-MM…" string. */
function yearMonth(ymd: string): [number, number] {
  const [year = '0', month = '0'] = ymd.split('-');
  return [Number(year), Number(month)];
}

/** "YYYY-MM-DD" of the first day of the Gaji Premi period holding `ymd`: two months from an odd month. */
export function periodStartOf(ymd: string): string {
  const [year, month] = yearMonth(ymd);
  const start = month % 2 === 1 ? month : month - 1;
  return `${year}-${String(start).padStart(2, '0')}-01`;
}

/** The first day of the period `count` periods before `start`. */
export function periodStartBefore(start: string, count: number): string {
  const [year, month] = yearMonth(start);
  const index = year * 12 + (month - 1) - count * 2;
  return `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}-01`;
}

/** Last day of the period starting on `start`: two months on, minus a day. */
export function periodEnd(start: string): string {
  const [year, month] = yearMonth(start);
  return new Date(Date.UTC(year, month + 1, 0)).toISOString().slice(0, 10);
}

/** "Sep – Oct 2026", or "Nov 2025 – Dec 2025" never: a period never spans a year boundary. */
export function periodLabel(start: string, end: string, locale: string): string {
  const month = new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' });
  const year = new Date(`${start}T00:00:00Z`).getUTCFullYear();
  return `${month.format(new Date(`${start}T00:00:00Z`))} – ${month.format(new Date(`${end}T00:00:00Z`))} ${year}`;
}

/** Short month name of a date, for the "Sep / Oct" columns. */
export function monthName(ymd: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' }).format(new Date(`${ymd}T00:00:00Z`));
}

/** The first and last day of a "YYYY-MM" month. */
export function monthRange(month: string): { from: string; to: string } {
  const [year, m] = yearMonth(month);
  const last = new Date(Date.UTC(year, m, 0)).getUTCDate();
  return { from: `${month}-01`, to: `${month}-${String(last).padStart(2, '0')}` };
}
