/**
 * Date conversion helpers.
 *
 * Inputs from <input type="date"> are "YYYY-MM-DD" with no timezone.
 * `new Date("YYYY-MM-DD")` parses as UTC midnight, which is wrong for users
 * outside UTC: a filter for "today" in UTC+7 would silently drop 7 hours.
 *
 * These helpers anchor date-only strings to LOCAL midnight, then serialize
 * to ISO UTC so the API receives an instant that matches what the user sees.
 */

const parseLocalYmd = (dateStr: string): Date | undefined => {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateStr);
  if (!m) return undefined;
  const [, y, mo, d] = m;
  return new Date(Number(y), Number(mo) - 1, Number(d));
};

/** "YYYY-MM-DD" → ISO UTC at local 00:00:00 of that day. */
export function localDateStartToUtcIso(dateStr: string): string | undefined {
  if (!dateStr) return undefined;
  const d = parseLocalYmd(dateStr);
  return d?.toISOString();
}

/** "YYYY-MM-DD" → ISO UTC at local 00:00:00 of the NEXT day (exclusive upper bound). */
export function localDateEndExclusiveToUtcIso(dateStr: string): string | undefined {
  if (!dateStr) return undefined;
  const d = parseLocalYmd(dateStr);
  if (!d) return undefined;
  d.setDate(d.getDate() + 1);
  return d.toISOString();
}
