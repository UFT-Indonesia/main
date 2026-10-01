'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { useHolidays, useRemoveHoliday, useSaveHoliday } from '@/hooks/use-attendance-settings';
import { useDateLocale } from '@/hooks/use-date-locale';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import type { Holiday, HolidayKind } from '@/lib/api/types';
import { cn } from '@/lib/utils';

const KINDS: HolidayKind[] = ['National', 'Collective'];

// ponytail: mirrors Holiday.NameMaxLength on the server.
const NAME_MAX_LENGTH = 100;

/** "YYYY-MM-DD" for a UTC-midnight Date — the grid works in calendar dates only, never zones. */
function ymd(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** Every date of the month, Monday-first, padded with nulls so the first lands in its column. */
function monthCells(year: number, month: number): (string | null)[] {
  const first = new Date(Date.UTC(year, month, 1));
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const leading = (first.getUTCDay() + 6) % 7;

  return [
    ...Array.from({ length: leading }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => ymd(new Date(Date.UTC(year, month, i + 1)))),
  ];
}

function isWeekend(date: string): boolean {
  const dow = new Date(`${date}T00:00:00Z`).getUTCDay();
  return dow === 0 || dow === 6;
}

/**
 * One month at a time, prev/next to walk the year. Click a date to declare a holiday on it, or
 * to rename or remove the one already there. Weekends stay clickable — a holiday on a Saturday
 * changes nothing today, but it records the date if the working week ever grows.
 */
export function HolidayCalendar({ canEdit }: { canEdit: boolean }) {
  const t = useTranslations('attendanceSettings.holidays');
  const tKind = useTranslations('attendance.holidayKind');
  const dateLocale = useDateLocale();
  const toast = useToast();

  const now = new Date();
  const [cursor, setCursor] = useState({ year: now.getFullYear(), month: now.getMonth() });
  const [selected, setSelected] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [kind, setKind] = useState<HolidayKind>('National');

  const cells = monthCells(cursor.year, cursor.month);
  const from = ymd(new Date(Date.UTC(cursor.year, cursor.month, 1)));
  const to = ymd(new Date(Date.UTC(cursor.year, cursor.month + 1, 0)));
  const { data, isLoading } = useHolidays(from, to);
  const byDate = new Map((data ?? []).map((holiday) => [holiday.date, holiday]));

  const saveMutation = useSaveHoliday();
  const removeMutation = useRemoveHoliday();
  const busy = saveMutation.isPending || removeMutation.isPending;

  const existing: Holiday | undefined = selected ? byDate.get(selected) : undefined;

  const monthLabel = new Intl.DateTimeFormat(dateLocale, { month: 'long', year: 'numeric', timeZone: 'UTC' })
    .format(new Date(Date.UTC(cursor.year, cursor.month, 1)));
  const weekdayLabels = Array.from({ length: 7 }, (_, i) =>
    new Intl.DateTimeFormat(dateLocale, { weekday: 'short', timeZone: 'UTC' })
      // 5 Jan 2026 is a Monday.
      .format(new Date(Date.UTC(2026, 0, 5 + i))));
  const formatSelected = (date: string) =>
    new Intl.DateTimeFormat(dateLocale, { dateStyle: 'full', timeZone: 'UTC' })
      .format(new Date(`${date}T00:00:00Z`));

  const step = (delta: number) => {
    setSelected(null);
    setCursor(({ year, month }) => {
      const next = new Date(Date.UTC(year, month + delta, 1));
      return { year: next.getUTCFullYear(), month: next.getUTCMonth() };
    });
  };

  const pick = (date: string) => {
    if (!canEdit) return;
    const holiday = byDate.get(date);
    setSelected(date);
    setName(holiday?.name ?? '');
    setKind(holiday?.kind ?? 'National');
  };

  const onSave = async () => {
    if (!selected) return;
    try {
      await saveMutation.mutateAsync({ date: selected, body: { name: name.trim(), kind } });
      toast.success(t('savedTitle'), t('savedDescription'));
      setSelected(null);
    } catch (err) {
      toast.error(t('errorTitle'), extractApiError(err).message);
    }
  };

  const onRemove = async () => {
    if (!selected) return;
    try {
      await removeMutation.mutateAsync(selected);
      toast.success(t('removedTitle'), t('savedDescription'));
      setSelected(null);
    } catch (err) {
      toast.error(t('errorTitle'), extractApiError(err).message);
    }
  };

  return (
    <section className="max-w-lg space-y-3 rounded-lg border border-border bg-card p-4">
      <header>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
      </header>

      <div className="flex items-center justify-between">
        <Button variant="ghost" size="icon" onClick={() => step(-1)} aria-label={t('previousMonth')}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="text-sm font-medium">{monthLabel}</span>
        <Button variant="ghost" size="icon" onClick={() => step(1)} aria-label={t('nextMonth')}>
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>

      <div className={cn('grid grid-cols-7 gap-1', isLoading && 'opacity-50')}>
        {weekdayLabels.map((label) => (
          <div key={label} className="text-center text-xs text-muted-foreground">
            {label}
          </div>
        ))}
        {cells.map((date, i) =>
          date === null ? (
            <div key={`blank-${i}`} />
          ) : (
            <button
              key={date}
              type="button"
              onClick={() => pick(date)}
              disabled={!canEdit}
              title={byDate.get(date)?.name}
              className={cn(
                'flex h-12 flex-col items-center justify-start rounded-md border border-transparent p-1 text-sm tabular-nums',
                canEdit && 'hover:bg-accent',
                isWeekend(date) && 'text-muted-foreground',
                byDate.has(date) && 'bg-destructive/10 text-destructive',
                selected === date && 'border-primary',
              )}
            >
              <span>{Number(date.slice(8))}</span>
              {byDate.has(date) && (
                <span className="w-full truncate text-[10px] leading-tight">{byDate.get(date)!.name}</span>
              )}
            </button>
          ),
        )}
      </div>

      {selected && (
        <div className="space-y-3 rounded-md border border-border p-3">
          <p className="text-sm font-medium">{formatSelected(selected)}</p>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="holiday-name">{t('name')}</Label>
              <Input
                id="holiday-name"
                value={name}
                maxLength={NAME_MAX_LENGTH}
                placeholder={t('namePlaceholder')}
                onChange={(e) => setName(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="holiday-kind">{t('kind')}</Label>
              <Select
                id="holiday-kind"
                value={kind}
                onChange={(e) => setKind(e.target.value as HolidayKind)}
              >
                {KINDS.map((k) => (
                  <option key={k} value={k}>
                    {tKind(k)}
                  </option>
                ))}
              </Select>
            </div>
          </div>

          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setSelected(null)} disabled={busy}>
              {t('cancel')}
            </Button>
            {existing && (
              <Button variant="destructive" onClick={onRemove} disabled={busy}>
                {t('remove')}
              </Button>
            )}
            <Button onClick={onSave} disabled={busy || name.trim().length === 0}>
              {existing ? t('update') : t('declare')}
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}
