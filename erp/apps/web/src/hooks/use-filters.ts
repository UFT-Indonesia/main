'use client';

import { useCallback, useMemo, useState } from 'react';
import {
  defaultValueFor,
  isComplete,
  operatorsFor,
  serializeFilters,
  type FilterFieldDef,
  type FilterOp,
  type FilterRow,
  type FilterValue,
} from '@/lib/filters/types';

let nextRowId = 0;

/**
 * Owns one screen's filter rows and derives the `filter=` query param from them.
 *
 * Resetting to page 1 lives here rather than at each call site, which is what the old filter
 * components got wrong: every setter had to remember `setPage(1)` itself, and one of them needed
 * a ref guard to stop a re-created callback from resetting the page on its own.
 */
export function useFilters(
  fields: readonly FilterFieldDef[],
  onChange?: () => void,
  initialRows?: () => Array<Omit<FilterRow, 'id'>>,
) {
  // Seeded once. A screen may land on a default view — the leave calendar opens on requests
  // still standing — and the user is then free to change or clear it like any other row.
  const [rows, setRows] = useState<FilterRow[]>(() =>
    (initialRows?.() ?? []).map((row) => ({ ...row, id: `row-${nextRowId++}` })),
  );

  const update = useCallback(
    (next: FilterRow[]) => {
      setRows(next);
      onChange?.();
    },
    [onChange],
  );

  const addRow = useCallback(() => {
    const field = fields[0];
    const op = field && operatorsFor(field)[0];
    if (!field || !op) return;
    update([
      ...rows,
      { id: `row-${nextRowId++}`, field: field.key, op, value: defaultValueFor(field, op) },
    ]);
  }, [fields, rows, update]);

  const removeRow = useCallback((id: string) => update(rows.filter((row) => row.id !== id)), [rows, update]);

  const clear = useCallback(() => update([]), [update]);

  /** Changing the field resets operator and value: last screen's operator rarely fits the new type. */
  const setField = useCallback(
    (id: string, key: string) => {
      const field = fields.find((candidate) => candidate.key === key);
      const op = field && operatorsFor(field)[0];
      if (!field || !op) return;
      update(rows.map((row) => (row.id === id ? { ...row, field: key, op, value: defaultValueFor(field, op) } : row)));
    },
    [fields, rows, update],
  );

  /** Changing the operator keeps the value only when the new operator takes the same shape. */
  const setOp = useCallback(
    (id: string, op: FilterOp) => {
      update(
        rows.map((row) => {
          if (row.id !== id) return row;
          const field = fields.find((candidate) => candidate.key === row.field);
          if (!field) return row;
          const shapeChanged = (row.op === 'between') !== (op === 'between');
          return { ...row, op, value: shapeChanged ? defaultValueFor(field, op) : row.value };
        }),
      );
    },
    [fields, rows, update],
  );

  const setValue = useCallback(
    (id: string, value: FilterValue) => update(rows.map((row) => (row.id === id ? { ...row, value } : row))),
    [rows, update],
  );

  const filter = useMemo(() => serializeFilters(rows, fields), [rows, fields]);
  const activeCount = useMemo(() => rows.filter(isComplete).length, [rows]);

  return { rows, filter, activeCount, addRow, removeRow, clear, setField, setOp, setValue };
}
