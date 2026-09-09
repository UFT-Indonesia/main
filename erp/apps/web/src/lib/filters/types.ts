/**
 * The filter builder's vocabulary. Operator ids and value shapes are the API contract — they
 * travel verbatim in the `filter=` query param and are validated again server-side by
 * FilterFieldMap, so this table has to mirror the C# one. A mismatch is not silent: the API
 * answers 400 `filter.invalid_operator`.
 */

export type FilterFieldType =
  | 'text'
  /** A value object EF stores through a converter (NIK, NPWP): equality only, no substring. */
  | 'convertedText'
  | 'enum'
  | 'relation'
  | 'date'
  | 'number'
  | 'bool';

export type FilterOp =
  | 'contains'
  | 'ncontains'
  | 'startswith'
  | 'is'
  | 'isnot'
  | 'empty'
  | 'nempty'
  | 'in'
  | 'nin'
  | 'before'
  | 'after'
  | 'between'
  | 'gt'
  | 'lt';

/** Operators that take no value at all. */
export const BLANK_OPS: readonly FilterOp[] = ['empty', 'nempty'];

const OPS: Record<FilterFieldType, readonly FilterOp[]> = {
  text: ['contains', 'ncontains', 'startswith', 'is', 'isnot'],
  convertedText: ['is', 'isnot'],
  enum: ['in', 'nin'],
  relation: ['in', 'nin'],
  date: ['is', 'before', 'after', 'between'],
  number: ['is', 'isnot', 'gt', 'lt', 'between'],
  bool: ['is'],
};

export interface FilterFieldDef {
  key: string;
  type: FilterFieldType;
  /** Absolute i18n key for the field's label, reusing the column labels already translated. */
  labelKey: string;
  /** Only a nullable column offers the blank checks, matching the server's field map. */
  nullable?: boolean;
  /** enum only: the allowed values, and the i18n prefix their labels live under. */
  options?: readonly string[];
  optionLabelPrefix?: string;
  /** relation only: which picker to render. */
  relation?: 'employee';
  /** Fields whose values are redacted per row; the API refuses these for anyone else (403). */
  ownerOnly?: boolean;
}

export type FilterValue = string | number | boolean | string[] | [string, string] | [number, number] | null;

export interface FilterRow {
  /** Local only, so React can key rows that are not yet complete. Never sent. */
  id: string;
  field: string;
  op: FilterOp;
  value: FilterValue;
}

export function operatorsFor(field: FilterFieldDef): readonly FilterOp[] {
  const base = OPS[field.type];
  return field.nullable && field.type !== 'enum' && field.type !== 'relation' && field.type !== 'bool'
    ? [...base, 'empty', 'nempty']
    : base;
}

export function defaultValueFor(field: FilterFieldDef, op: FilterOp): FilterValue {
  if (BLANK_OPS.includes(op)) return null;
  if (op === 'between') return ['', ''];
  switch (field.type) {
    case 'enum':
    case 'relation':
      return [];
    case 'bool':
      return true;
    case 'number':
      // Held as a string while typing; coerced to a number at serialize time.
      return '';
    default:
      return '';
  }
}

/**
 * Whether a row says anything yet. A half-built row — field chosen, value still blank — is not
 * an error and not a request for "everything": it is simply ignored until it means something.
 */
export function isComplete(row: FilterRow): boolean {
  if (!row.field || !row.op) return false;
  if (BLANK_OPS.includes(row.op)) return true;

  const { value } = row;
  if (value === null || value === undefined) return false;
  if (Array.isArray(value)) {
    if (value.length === 0) return false;
    // A range needs both ends; a set needs at least one member.
    return row.op === 'between' ? value.every((part) => part !== '' && part !== null) : true;
  }
  if (typeof value === 'string') return value.trim() !== '';
  return true;
}

/**
 * The `filter=` query param, or undefined when nothing is filtering.
 *
 * Number inputs are held as strings while being typed — an in-progress "-" or "" is not a
 * number — so they are coerced here, against the field's declared type rather than by guessing
 * from the value.
 */
export function serializeFilters(
  rows: readonly FilterRow[],
  fields: readonly FilterFieldDef[],
): string | undefined {
  const byKey = new Map(fields.map((field) => [field.key, field]));

  const complete = rows.filter(isComplete).map((row) => ({
    field: row.field,
    op: row.op,
    value: coerce(row, byKey.get(row.field)),
  }));

  return complete.length > 0 ? JSON.stringify(complete) : undefined;
}

function coerce(row: FilterRow, field: FilterFieldDef | undefined): FilterValue {
  if (BLANK_OPS.includes(row.op)) return null;
  if (field?.type !== 'number') return row.value;

  return Array.isArray(row.value)
    ? (row.value.map(Number) as [number, number])
    : Number(row.value);
}
