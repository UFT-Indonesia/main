'use client';

import * as React from 'react';
import { useEffect, useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { ListFilter, Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { MultiCombobox } from '@/components/ui/combobox';
import { DatePickerField, DateRangePickerField } from '@/components/ui/date-picker';
import { useDebounce } from '@/hooks/use-debounce';
import { useEmployees } from '@/hooks/use-employees';
import { employeeLookupFilter } from '@/lib/filters/quick';
import { cn } from '@/lib/utils';
import {
  BLANK_OPS,
  operatorsFor,
  type FilterFieldDef,
  type FilterOp,
  type FilterRow,
  type FilterValue,
} from '@/lib/filters/types';

interface FilterBuilderProps {
  fields: readonly FilterFieldDef[];
  rows: FilterRow[];
  activeCount: number;
  onAddRow: () => void;
  onRemoveRow: (id: string) => void;
  onClear: () => void;
  onFieldChange: (id: string, field: string) => void;
  onOpChange: (id: string, op: FilterOp) => void;
  onValueChange: (id: string, value: FilterValue) => void;
}

/**
 * The one filter UI. Every menu passes its own field descriptors and gets the same button,
 * the same operators per datatype, and the same behaviour — there is deliberately no
 * per-feature filter component any more.
 */
export function FilterBuilder({
  fields,
  rows,
  activeCount,
  onAddRow,
  onRemoveRow,
  onClear,
  onFieldChange,
  onOpChange,
  onValueChange,
}: FilterBuilderProps) {
  const t = useTranslations();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, []);

  return (
    <div ref={containerRef} className="relative">
      <Button
        type="button"
        variant="outline"
        size="sm"
        aria-expanded={open}
        onClick={() => setOpen((wasOpen) => !wasOpen)}
      >
        <ListFilter className="h-4 w-4" />
        {t('filters.button')}
        {activeCount > 0 && (
          <span className="rounded bg-primary px-1.5 text-xs text-primary-foreground">{activeCount}</span>
        )}
      </Button>

      {open && (
        <div className="absolute left-0 z-50 mt-1 w-[min(42rem,calc(100vw-2rem))] rounded-md border border-border bg-background p-3 shadow-md">
          {rows.length === 0 ? (
            <p className="px-1 py-2 text-sm text-muted-foreground">{t('filters.empty')}</p>
          ) : (
            <div className="space-y-2">
              {rows.map((row, index) => {
                const field = fields.find((candidate) => candidate.key === row.field);
                if (!field) return null;

                return (
                  <div key={row.id} className="flex flex-wrap items-start gap-2">
                    <span className="w-12 shrink-0 pt-2 text-xs text-muted-foreground">
                      {index === 0 ? t('filters.where') : t('filters.and')}
                    </span>

                    <Select
                      aria-label={t('filters.field')}
                      className="w-40 shrink-0"
                      value={row.field}
                      onChange={(e) => onFieldChange(row.id, e.target.value)}
                    >
                      {fields.map((candidate) => (
                        <option key={candidate.key} value={candidate.key}>
                          {t(candidate.labelKey)}
                        </option>
                      ))}
                    </Select>

                    <Select
                      aria-label={t('filters.operator')}
                      className="w-36 shrink-0"
                      value={row.op}
                      onChange={(e) => onOpChange(row.id, e.target.value as FilterOp)}
                    >
                      {operatorsFor(field).map((op) => (
                        <option key={op} value={op}>
                          {t(`filters.ops.${op}`)}
                        </option>
                      ))}
                    </Select>

                    <div className="min-w-48 flex-1">
                      <ValueEditor
                        field={field}
                        op={row.op}
                        value={row.value}
                        onChange={(value) => onValueChange(row.id, value)}
                      />
                    </div>

                    <button
                      type="button"
                      aria-label={t('filters.removeRow')}
                      onClick={() => onRemoveRow(row.id)}
                      className="mt-1 rounded p-1 text-muted-foreground hover:bg-accent hover:text-foreground"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </div>
                );
              })}
            </div>
          )}

          <div className="mt-3 flex items-center justify-between border-t border-border pt-3">
            <Button type="button" variant="ghost" size="sm" onClick={onAddRow}>
              <Plus className="h-4 w-4" />
              {t('filters.addRow')}
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onClear}
              disabled={rows.length === 0}
              className={cn(rows.length === 0 && 'invisible')}
            >
              {t('filters.clearAll')}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

interface ValueEditorProps {
  field: FilterFieldDef;
  op: FilterOp;
  value: FilterValue;
  onChange: (value: FilterValue) => void;
}

/** The datatype decides the control, exactly as the operator list decided the choices above. */
function ValueEditor({ field, op, value, onChange }: ValueEditorProps) {
  const t = useTranslations();

  // "is empty" / "is not empty" ask nothing.
  if (BLANK_OPS.includes(op)) return null;

  if (op === 'between') {
    const [from, to] = Array.isArray(value) ? value : ['', ''];
    if (field.type === 'number') {
      return (
        <div className="flex items-center gap-2">
          <DebouncedInput
            type="number"
            aria-label={t('filters.from')}
            value={String(from ?? '')}
            onChange={(next) => onChange([String(next), String(to ?? '')] as [string, string])}
          />
          <span className="text-xs text-muted-foreground">{t('filters.to')}</span>
          <DebouncedInput
            type="number"
            aria-label={t('filters.to')}
            value={String(to ?? '')}
            onChange={(next) => onChange([String(from ?? ''), String(next)] as [string, string])}
          />
        </div>
      );
    }
    return (
      <DateRangePickerField
        start={String(from ?? '')}
        end={String(to ?? '')}
        onChange={(start, end) => onChange([start, end])}
        aria-label={t('filters.dateRange')}
      />
    );
  }

  switch (field.type) {
    case 'enum':
      return (
        <MultiCombobox
          values={Array.isArray(value) ? (value as string[]) : []}
          onChange={(values) => onChange(values)}
          options={(field.options ?? []).map((option) => ({
            value: option,
            label: field.optionLabelPrefix ? t(`${field.optionLabelPrefix}.${option}`) : option,
          }))}
          placeholder={t('filters.selectValues')}
          aria-label={t('filters.value')}
        />
      );

    case 'relation':
      return (
        <EmployeeRelationValue
          values={Array.isArray(value) ? (value as string[]) : []}
          onChange={onChange}
        />
      );

    case 'date':
      return (
        <DatePickerField
          value={typeof value === 'string' ? value : ''}
          onChange={(next) => onChange(next)}
          aria-label={t('filters.value')}
        />
      );

    case 'number':
      return (
        <DebouncedInput
          type="number"
          aria-label={t('filters.value')}
          value={String(value ?? '')}
          onChange={onChange}
        />
      );

    case 'bool':
      return (
        <Select
          aria-label={t('filters.value')}
          value={value === false ? 'false' : 'true'}
          onChange={(e) => onChange(e.target.value === 'true')}
        >
          <option value="true">{t('filters.yes')}</option>
          <option value="false">{t('filters.no')}</option>
        </Select>
      );

    default:
      return (
        <DebouncedInput
          aria-label={t('filters.value')}
          value={typeof value === 'string' ? value : ''}
          onChange={onChange}
          placeholder={field.type === 'convertedText' ? t('filters.exactValue') : undefined}
        />
      );
  }
}

/** Employee relation values, searched remotely the way EmployeePicker does for single select. */
function EmployeeRelationValue({
  values,
  onChange,
}: {
  values: string[];
  onChange: (values: string[]) => void;
}) {
  const t = useTranslations();
  const [search, setSearch] = useState('');
    // Not restricted to Active: a filter may legitimately target someone who has left.
  const employeesQuery = useEmployees({ filter: employeeLookupFilter(search, false), pageSize: 50 });

  return (
    <MultiCombobox
      values={values}
      onChange={onChange}
      options={(employeesQuery.data?.items ?? []).map((employee) => ({
        value: employee.id,
        label: employee.fullName,
        meta: employee.role,
      }))}
      onSearchChange={setSearch}
      loading={employeesQuery.isLoading}
      placeholder={t('filters.selectValues')}
      searchPlaceholder={t('common.search')}
      aria-label={t('filters.value')}
    />
  );
}


/**
 * A text or number value, held locally while typing and published on the same 500ms debounce the
 * old search boxes used. Without this every keystroke would be a query, since the builder applies
 * filters live rather than behind an Apply button.
 */
function DebouncedInput({
  value,
  onChange,
  ...rest
}: {
  value: string;
  onChange: (value: string) => void;
} & Omit<React.ComponentProps<typeof Input>, 'value' | 'onChange'>) {
  const [local, setLocal] = useState(value);
  const debounced = useDebounce(local, 500);
  const lastPublished = useRef(value);

  useEffect(() => {
    if (debounced === lastPublished.current) return;
    lastPublished.current = debounced;
    onChange(debounced);
  }, [debounced, onChange]);

  // Adopt an outside change (clearing a filter, switching operator) without clobbering typing.
  useEffect(() => {
    if (value !== lastPublished.current) {
      lastPublished.current = value;
      setLocal(value);
    }
  }, [value]);

  return <Input {...rest} value={local} onChange={(e) => setLocal(e.target.value)} />;
}
