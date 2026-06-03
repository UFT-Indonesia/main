'use client';

import { useEffect, useId, useRef, useState } from 'react';
import { Check, ChevronDown, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useDebounce } from '@/hooks/use-debounce';

export interface ComboboxOption {
  value: string;
  label: string;
  meta?: string;
}

interface ComboboxProps {
  value: string;
  onChange: (value: string) => void;
  options: ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  onSearchChange?: (search: string) => void;
  loading?: boolean;
  disabled?: boolean;
  error?: boolean;
  clearable?: boolean;
}

export function Combobox({
  value,
  onChange,
  options,
  placeholder = 'Select…',
  searchPlaceholder = 'Search…',
  onSearchChange,
  loading,
  disabled,
  error,
  clearable,
}: ComboboxProps) {
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 500);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const selected = options.find((o) => o.value === value);

  useEffect(() => {
    onSearchChange?.(debouncedSearch);
  }, [debouncedSearch, onSearchChange]);

  useEffect(() => {
    function handleOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
        setSearch('');
      }
    }
    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, []);

  function openDropdown() {
    setOpen(true);
    setTimeout(() => inputRef.current?.focus(), 0);
  }

  function closeDropdown() {
    setOpen(false);
    setSearch('');
  }

  function handleToggle() {
    if (open) {
      closeDropdown();
    } else {
      openDropdown();
    }
  }

  function handleSelect(optValue: string) {
    onChange(optValue);
    closeDropdown();
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        role="combobox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-haspopup="listbox"
        disabled={disabled}
        onClick={handleToggle}
        className={cn(
          'flex h-9 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background',
          'focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
          'disabled:cursor-not-allowed disabled:opacity-50',
          error && 'border-destructive',
        )}
      >
        <span className={cn('truncate', !selected && 'text-muted-foreground')}>
          {selected ? (
            <span className="flex items-center gap-1.5">
              {selected.label}
              {selected.meta && (
                <span className="rounded border border-border px-1 py-px text-xs text-muted-foreground">
                  {selected.meta}
                </span>
              )}
            </span>
          ) : (
            placeholder
          )}
        </span>
        <span className="flex shrink-0 items-center gap-0.5">
          {clearable && value && (
            <X
              className="h-3.5 w-3.5 text-muted-foreground hover:text-foreground"
              onClick={(e) => {
                e.stopPropagation();
                onChange('');
              }}
            />
          )}
          <ChevronDown className={cn('h-4 w-4 text-muted-foreground transition-transform', open && 'rotate-180')} />
        </span>
      </button>

      {open && (
        <div className="absolute z-50 mt-1 w-full rounded-md border border-border bg-background text-foreground shadow-md">
          <div className="border-b border-border p-2">
            <input
              ref={inputRef}
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={searchPlaceholder}
              className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            />
          </div>
          <ul id={listboxId} role="listbox" className="max-h-56 overflow-y-auto py-1">
            {loading ? (
              <li className="px-3 py-2 text-sm text-muted-foreground">Loading…</li>
            ) : options.length === 0 ? (
              <li className="px-3 py-2 text-sm text-muted-foreground">No results.</li>
            ) : (
              options.map((opt) => (
                <li
                  key={opt.value}
                  role="option"
                  aria-selected={opt.value === value}
                  onClick={() => handleSelect(opt.value)}
                  className={cn(
                    'flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-sm',
                    'hover:bg-accent hover:text-accent-foreground',
                    opt.value === value && 'bg-accent text-accent-foreground',
                  )}
                >
                  <span className="flex items-center gap-1.5 truncate">
                    {opt.label}
                    {opt.meta && (
                      <span className="rounded border border-border px-1 py-px text-xs text-muted-foreground">
                        {opt.meta}
                      </span>
                    )}
                  </span>
                  {opt.value === value && <Check className="h-3.5 w-3.5 shrink-0" />}
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  );
}
