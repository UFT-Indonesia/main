'use client';

import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
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
  const optionId = (i: number) => `${listboxId}-opt-${i}`;
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [activeIndex, setActiveIndex] = useState(-1);
  const debouncedSearch = useDebounce(search, 500);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  const selected = options.find((o) => o.value === value);

  useEffect(() => {
    onSearchChange?.(debouncedSearch);
  }, [debouncedSearch, onSearchChange]);

  useEffect(() => {
    function handleOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
        setSearch('');
        setActiveIndex(-1);
      }
    }
    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, []);

  // Reset/clamp active index when option set changes or panel opens.
  useEffect(() => {
    if (!open) return;
    if (options.length === 0) {
      setActiveIndex(-1);
      return;
    }
    setActiveIndex((prev) => {
      if (prev >= 0 && prev < options.length) return prev;
      const selectedIdx = options.findIndex((o) => o.value === value);
      return selectedIdx >= 0 ? selectedIdx : 0;
    });
  }, [open, options, value]);

  // Keep active option in view.
  useEffect(() => {
    if (!open || activeIndex < 0 || !listRef.current) return;
    const el = listRef.current.querySelector<HTMLLIElement>(`#${CSS.escape(optionId(activeIndex))}`);
    el?.scrollIntoView({ block: 'nearest' });
  }, [activeIndex, open]); // eslint-disable-line react-hooks/exhaustive-deps

  function openDropdown(initialActive?: number) {
    setOpen(true);
    if (typeof initialActive === 'number') setActiveIndex(initialActive);
    setTimeout(() => inputRef.current?.focus(), 0);
  }

  function closeDropdown(returnFocus = true) {
    setOpen(false);
    setSearch('');
    setActiveIndex(-1);
    if (returnFocus) {
      // Return focus to trigger button for keyboard users.
      const btn = containerRef.current?.querySelector<HTMLButtonElement>('button[role="combobox"]');
      btn?.focus();
    }
  }

  function handleToggle() {
    if (open) {
      closeDropdown(false);
    } else {
      openDropdown();
    }
  }

  function handleSelect(optValue: string) {
    onChange(optValue);
    closeDropdown();
  }

  function moveActive(delta: number) {
    if (options.length === 0) return;
    setActiveIndex((prev) => {
      const start = prev < 0 ? (delta > 0 ? -1 : 0) : prev;
      const next = (start + delta + options.length) % options.length;
      return next;
    });
  }

  function handleTriggerKeyDown(e: KeyboardEvent<HTMLButtonElement>) {
    if (disabled) return;
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowUp':
        e.preventDefault();
        openDropdown(e.key === 'ArrowDown' ? 0 : Math.max(0, options.length - 1));
        break;
      case 'Enter':
      case ' ':
        e.preventDefault();
        if (!open) openDropdown();
        break;
      case 'Escape':
        if (open) {
          e.preventDefault();
          closeDropdown(false);
        }
        break;
    }
  }

  function handleInputKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        moveActive(1);
        break;
      case 'ArrowUp':
        e.preventDefault();
        moveActive(-1);
        break;
      case 'Home':
        e.preventDefault();
        if (options.length) setActiveIndex(0);
        break;
      case 'End':
        e.preventDefault();
        if (options.length) setActiveIndex(options.length - 1);
        break;
      case 'Enter':
        e.preventDefault();
        if (activeIndex >= 0 && options[activeIndex]) {
          handleSelect(options[activeIndex].value);
        }
        break;
      case 'Escape':
        e.preventDefault();
        closeDropdown();
        break;
      case 'Tab':
        // Allow natural focus exit; close panel.
        closeDropdown(false);
        break;
    }
  }

  const activeDescendant = open && activeIndex >= 0 ? optionId(activeIndex) : undefined;

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        role="combobox"
        aria-expanded={open}
        aria-controls={open ? listboxId : undefined}
        aria-haspopup="listbox"
        disabled={disabled}
        onClick={handleToggle}
        onKeyDown={handleTriggerKeyDown}
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
              role="button"
              aria-label="Clear selection"
              tabIndex={0}
              className="h-3.5 w-3.5 text-muted-foreground hover:text-foreground"
              onClick={(e) => {
                e.stopPropagation();
                onChange('');
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  e.stopPropagation();
                  onChange('');
                }
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
              onKeyDown={handleInputKeyDown}
              placeholder={searchPlaceholder}
              role="searchbox"
              aria-controls={listboxId}
              aria-activedescendant={activeDescendant}
              autoComplete="off"
              className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            />
          </div>
          <ul
            ref={listRef}
            id={listboxId}
            role="listbox"
            className="max-h-56 overflow-y-auto py-1"
          >
            {loading ? (
              <li className="px-3 py-2 text-sm text-muted-foreground">Loading…</li>
            ) : options.length === 0 ? (
              <li className="px-3 py-2 text-sm text-muted-foreground">No results.</li>
            ) : (
              options.map((opt, i) => {
                const isActive = i === activeIndex;
                const isSelected = opt.value === value;
                return (
                  <li
                    key={opt.value}
                    id={optionId(i)}
                    role="option"
                    aria-selected={isSelected}
                    onMouseEnter={() => setActiveIndex(i)}
                    onMouseDown={(e) => {
                      // Prevent input blur before click registers.
                      e.preventDefault();
                    }}
                    onClick={() => handleSelect(opt.value)}
                    className={cn(
                      'flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-sm',
                      isActive && 'bg-accent text-accent-foreground',
                      isSelected && !isActive && 'bg-accent/50',
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
                    {isSelected && <Check className="h-3.5 w-3.5 shrink-0" />}
                  </li>
                );
              })
            )}
          </ul>
        </div>
      )}
    </div>
  );
}
