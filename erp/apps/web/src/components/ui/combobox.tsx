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
      // eslint-disable-next-line react-hooks/set-state-in-effect
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
          {clearable && value && !disabled && (
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


interface MultiComboboxProps {
  values: string[];
  onChange: (values: string[]) => void;
  options: ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  onSearchChange?: (search: string) => void;
  loading?: boolean;
  disabled?: boolean;
  'aria-label'?: string;
}

/**
 * The same combobox in multi-select form, for the filter builder's "is any of". Selections show
 * as removable chips in the control and the panel stays open between picks, since choosing three
 * people is the normal case rather than the exception.
 */
export function MultiCombobox({
  values,
  onChange,
  options,
  placeholder = 'Select…',
  searchPlaceholder = 'Search…',
  onSearchChange,
  loading,
  disabled,
  'aria-label': ariaLabel,
}: MultiComboboxProps) {
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 500);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

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

  // A chip has to keep its name after the user types a new search, which drops its option from
  // the list. The label is known at the moment it is picked, so remember it then rather than
  // trying to reconstruct it later.
  const [pickedLabels, setPickedLabels] = useState<Record<string, string>>({});
  const labelFor = (value: string) =>
    options.find((option) => option.value === value)?.label ?? pickedLabels[value] ?? value;

  function toggle(value: string, label?: string) {
    onChange(values.includes(value) ? values.filter((v) => v !== value) : [...values, value]);
    if (label) {
      setPickedLabels((prev) => (prev[value] === label ? prev : { ...prev, [value]: label }));
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        role="combobox"
        aria-expanded={open}
        aria-controls={open ? listboxId : undefined}
        aria-haspopup="listbox"
        aria-label={ariaLabel}
        disabled={disabled}
        onClick={() => {
          setOpen((wasOpen) => !wasOpen);
          if (!open) setTimeout(() => inputRef.current?.focus(), 0);
        }}
        className={cn(
          'flex min-h-9 w-full flex-wrap items-center gap-1 rounded-md border border-input bg-background px-2 py-1 text-left text-sm',
          'focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
          'disabled:cursor-not-allowed disabled:opacity-50',
        )}
      >
        {values.length === 0 ? (
          <span className="px-1 text-muted-foreground">{placeholder}</span>
        ) : (
          values.map((value) => (
            <span
              key={value}
              className="flex items-center gap-1 rounded bg-accent px-1.5 py-0.5 text-xs text-accent-foreground"
            >
              {labelFor(value)}
              <X
                role="button"
                aria-label={`Remove ${labelFor(value)}`}
                tabIndex={0}
                className="h-3 w-3 hover:text-foreground"
                onClick={(e) => {
                  e.stopPropagation();
                  toggle(value);
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    e.stopPropagation();
                    toggle(value);
                  }
                }}
              />
            </span>
          ))
        )}
        <ChevronDown className={cn('ml-auto h-4 w-4 shrink-0 text-muted-foreground', open && 'rotate-180')} />
      </button>

      {open && (
        <div className="absolute z-50 mt-1 w-full rounded-md border border-border bg-background text-foreground shadow-md">
          <div className="border-b border-border p-2">
            <input
              ref={inputRef}
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') {
                  e.preventDefault();
                  setOpen(false);
                  setSearch('');
                }
              }}
              placeholder={searchPlaceholder}
              role="searchbox"
              aria-controls={listboxId}
              autoComplete="off"
              className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            />
          </div>
          <ul id={listboxId} role="listbox" aria-multiselectable className="max-h-56 overflow-y-auto py-1">
            {loading ? (
              <li className="px-3 py-2 text-sm text-muted-foreground">Loading…</li>
            ) : options.length === 0 ? (
              <li className="px-3 py-2 text-sm text-muted-foreground">No results.</li>
            ) : (
              options.map((opt) => {
                const isSelected = values.includes(opt.value);
                return (
                  <li
                    key={opt.value}
                    role="option"
                    aria-selected={isSelected}
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => toggle(opt.value, opt.label)}
                    className={cn(
                      'flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-sm hover:bg-accent',
                      isSelected && 'bg-accent/50',
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
