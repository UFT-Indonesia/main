import type { FilterRow } from './types';

/**
 * The employee pickers need "active employees whose name contains X" without showing a filter
 * builder. Rather than keeping legacy query params alive just for them, they build the same
 * rows the builder would — one filter path, no second way to ask.
 */
export function employeeLookupFilter(
  search: string,
  activeOnly = true,
  role?: 'Owner' | 'Manager' | 'Staff',
): string | undefined {
  const rows: Array<Pick<FilterRow, 'field' | 'op' | 'value'>> = [];

  if (activeOnly) {
    rows.push({ field: 'status', op: 'in', value: ['Active'] });
  }

  if (role) {
    rows.push({ field: 'role', op: 'in', value: [role] });
  }

  const needle = search.trim();
  if (needle) {
    rows.push({ field: 'fullName', op: 'contains', value: needle });
  }

  return rows.length > 0 ? JSON.stringify(rows) : undefined;
}

/** Active headcount, optionally narrowed to one role — the dashboard's four count queries. */
export function activeHeadcountFilter(role?: 'Owner' | 'Manager' | 'Staff'): string {
  const rows: Array<{ field: string; op: string; value: unknown }> = [
    { field: 'status', op: 'in', value: ['Active'] },
  ];

  if (role) {
    rows.push({ field: 'role', op: 'in', value: [role] });
  }

  return JSON.stringify(rows);
}
