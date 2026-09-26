'use client';

import {
  CalendarDate,
  ZonedDateTime,
  getLocalTimeZone,
  parseAbsolute,
  parseDate,
  today,
} from '@internationalized/date';
import { CalendarDays } from 'lucide-react';
import { type DateLocale, useDateLocale } from '@/hooks/use-date-locale';
import {
  Button as AriaButton,
  Calendar,
  CalendarCell,
  CalendarGrid,
  CalendarGridBody,
  CalendarGridHeader,
  CalendarHeaderCell,
  DateInput,
  DatePicker as AriaDatePicker,
  DateRangePicker as AriaDateRangePicker,
  DateSegment,
  Dialog,
  Group,
  Heading,
  Popover,
  RangeCalendar,
  TimeField,
} from 'react-aria-components';
import type { DateValue } from 'react-aria-components';
import { cn } from '@/lib/utils';

/**
 * Date and date-range pickers over react-aria-components.
 *
 * These speak plain strings, not `CalendarDate`: `"YYYY-MM-DD"` for dates and a UTC ISO string
 * for date-times, which is the currency the API and `types.ts` already use everywhere. Keeping
 * `@internationalized/date` an internal detail means zod schemas, `superRefine` comparisons and
 * request payloads are untouched by adopting this.
 *
 * The date-time variant is the reason the timezone bug cannot come back: the value is a
 * `ZonedDateTime` in the attendance policy's zone, so there is no naked `new Date(string)` in
 * the write path to be silently reinterpreted in the browser's zone.
 */

const fieldStyles =
  'flex h-9 w-full items-center rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-within:outline-none focus-within:ring-2 focus-within:ring-ring disabled:cursor-not-allowed disabled:opacity-50';

const segmentStyles =
  'rounded px-0.5 tabular-nums outline-none focus:bg-primary focus:text-primary-foreground placeholder-shown:text-muted-foreground';

const cellStyles =
  'm-0.5 flex h-7 w-7 cursor-default items-center justify-center rounded-md text-sm outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring selected:bg-primary selected:text-primary-foreground disabled:cursor-not-allowed disabled:text-muted-foreground/40 disabled:line-through disabled:hover:bg-transparent unavailable:cursor-not-allowed unavailable:bg-muted unavailable:text-muted-foreground/50 unavailable:hover:bg-muted';

/**
 * A date that carries an approved leave which doesn't conflict with what's being filed — still
 * pickable, just worth a hint that part of the day is already spoken for. Skipped whenever the
 * cell is selected or unavailable, both of which already carry their own, stronger color.
 */
const partialCellStyles = 'bg-warning/20';

const popoverStyles =
  'z-50 rounded-md border border-border bg-background p-3 text-foreground shadow-md';

/** Turns a flat "YYYY-MM-DD" list into the matcher `isDateUnavailable` wants. */
function unavailableMatcher(dates: string[] | undefined) {
  if (!dates?.length) return undefined;
  const blocked = new Set(dates);
  return (date: DateValue) => blocked.has(date.toString());
}

function CalendarBody({ partialDates }: { partialDates?: string[] }) {
  const partial = partialDates?.length ? new Set(partialDates) : undefined;

  return (
    <>
      <header className="mb-2 flex items-center justify-between gap-2">
        <AriaButton slot="previous" className="rounded px-2 py-1 text-sm hover:bg-accent">
          ‹
        </AriaButton>
        <Heading className="text-sm font-medium" />
        <AriaButton slot="next" className="rounded px-2 py-1 text-sm hover:bg-accent">
          ›
        </AriaButton>
      </header>
      <CalendarGrid>
        <CalendarGridHeader>
          {(day) => (
            <CalendarHeaderCell className="h-8 w-8 text-xs font-normal text-muted-foreground">
              {day}
            </CalendarHeaderCell>
          )}
        </CalendarGridHeader>
        <CalendarGridBody>
          {(date) => (
            <CalendarCell
              date={date}
              className={(render) =>
                cn(
                  cellStyles,
                  partial?.has(date.toString()) && !render.isSelected && !render.isUnavailable
                    && partialCellStyles,
                )
              }
            />
          )}
        </CalendarGridBody>
      </CalendarGrid>
    </>
  );
}

/** Matches `formatPunchedAt` elsewhere in the app, so the value doesn't change shape when a row
 * flips from its static display into this field. */
function formatDateTime(date: ZonedDateTime, locale: DateLocale): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' })
    .format(date.toDate());
}

function TriggerButton() {
  return (
    <AriaButton className="ml-auto shrink-0 rounded p-0.5 text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring">
      <CalendarDays className="h-4 w-4" />
    </AriaButton>
  );
}

interface DatePickerFieldProps {
  /** "YYYY-MM-DD", or empty for no selection. */
  value: string;
  onChange: (value: string) => void;
  /** "YYYY-MM-DD" dates that must not be selectable. */
  blockedDates?: string[];
  isDisabled?: boolean;
  'aria-label'?: string;
  className?: string;
}

/** Single date. Value in and out is `"YYYY-MM-DD"`; empty string means unset. */
export function DatePickerField({
  value,
  onChange,
  blockedDates,
  isDisabled,
  className,
  ...rest
}: DatePickerFieldProps) {
  return (
    <AriaDatePicker
      aria-label={rest['aria-label']}
      value={value ? parseDate(value) : null}
      onChange={(next) => onChange(next ? next.toString() : '')}
      isDisabled={isDisabled}
      isDateUnavailable={unavailableMatcher(blockedDates)}
      className={cn('flex flex-col gap-1', className)}
    >
      <Group className={fieldStyles}>
        <DateInput className="flex flex-1 items-center gap-0.5">
          {(segment) => <DateSegment segment={segment} className={segmentStyles} />}
        </DateInput>
        <TriggerButton />
      </Group>
      <Popover className={popoverStyles}>
        <Dialog>
          <Calendar>
            <CalendarBody />
          </Calendar>
        </Dialog>
      </Popover>
    </AriaDatePicker>
  );
}

interface DateTimePickerFieldProps {
  /** UTC ISO instant, or empty for no selection. */
  value: string;
  onChange: (isoUtc: string) => void;
  /** IANA zone the wall-clock time is entered in — the attendance policy's zone. */
  timeZone: string;
  /** "YYYY-MM-DD" dates that must not be selectable. */
  blockedDates?: string[];
  isDisabled?: boolean;
  'aria-label'?: string;
  className?: string;
  /** Drops the calendar-icon trigger for compact inline uses, e.g. a table cell, where the
   * field already looking like an input is affordance enough. Clicking anywhere in the field
   * opens the same calendar + time popover instead; segments stay keyboard-editable either way. */
  hideTrigger?: boolean;
}

/**
 * Date + time in one segmented field. Value in and out is a UTC ISO instant; the segments show
 * the wall clock in `timeZone`, and `hideTimeZone` keeps the zone abbreviation out of the UI —
 * everyone here is in the same zone, so displaying it is noise.
 *
 * The popover pairs the calendar with a `TimeField` so both halves of the value can be set
 * without leaving it — `shouldCloseOnSelect` is off because picking a date would otherwise close
 * the popover before the time is touched.
 */
export function DateTimePickerField({
  value,
  onChange,
  timeZone,
  blockedDates,
  isDisabled,
  className,
  hideTrigger,
  ...rest
}: DateTimePickerFieldProps) {
  const parsed: ZonedDateTime | null = value ? parseAbsolute(value, timeZone) : null;
  const dateLocale = useDateLocale();

  return (
    <AriaDatePicker
      aria-label={rest['aria-label']}
      granularity="minute"
      hideTimeZone
      value={parsed}
      onChange={(next) => onChange(next ? next.toDate().toISOString() : '')}
      isDisabled={isDisabled}
      isDateUnavailable={unavailableMatcher(blockedDates)}
      shouldCloseOnSelect={false}
      className={cn('flex flex-col gap-1', className)}
    >
      <Group className={fieldStyles}>
        {hideTrigger ? (
          <AriaButton className="flex-1 text-center tabular-nums outline-none">
            {parsed ? formatDateTime(parsed, dateLocale) : '–'}
          </AriaButton>
        ) : (
          <>
            <DateInput className="flex flex-1 items-center gap-0.5">
              {(segment) => <DateSegment segment={segment} className={segmentStyles} />}
            </DateInput>
            <TriggerButton />
          </>
        )}
      </Group>
      <Popover className={popoverStyles}>
        <Dialog>
          <div className="space-y-3">
            <Calendar>
              <CalendarBody />
            </Calendar>
            {parsed && (
              <TimeField
                aria-label="Time"
                value={parsed}
                onChange={(next) => next && onChange(next.toDate().toISOString())}
                hideTimeZone
                className="flex justify-center gap-0.5 border-t border-border pt-3"
              >
                <DateInput className="flex items-center gap-0.5">
                  {(segment) => <DateSegment segment={segment} className={segmentStyles} />}
                </DateInput>
              </TimeField>
            )}
          </div>
        </Dialog>
      </Popover>
    </AriaDatePicker>
  );
}

interface DateRangePickerFieldProps {
  /** Both "YYYY-MM-DD"; empty strings mean unset. */
  start: string;
  end: string;
  onChange: (start: string, end: string) => void;
  /** "YYYY-MM-DD" dates that must not be selectable. */
  blockedDates?: string[];
  /** "YYYY-MM-DD" dates that are pickable but carry a non-conflicting approved leave. */
  partialDates?: string[];
  isDisabled?: boolean;
  'aria-label'?: string;
  className?: string;
}

/**
 * One control for a start/end pair. `allowsNonContiguousRanges` is deliberately off: a range
 * dragged across blocked days is truncated rather than swallowing them.
 */
export function DateRangePickerField({
  start,
  end,
  onChange,
  blockedDates,
  partialDates,
  isDisabled,
  className,
  ...rest
}: DateRangePickerFieldProps) {
  const value =
    start && end ? { start: parseDate(start), end: parseDate(end) } : null;

  return (
    <AriaDateRangePicker
      aria-label={rest['aria-label']}
      value={value}
      onChange={(next) => onChange(next?.start?.toString() ?? '', next?.end?.toString() ?? '')}
      isDisabled={isDisabled}
      isDateUnavailable={unavailableMatcher(blockedDates)}
      className={cn('flex flex-col gap-1', className)}
    >
      <Group className={fieldStyles}>
        <DateInput slot="start" className="flex items-center gap-0.5">
          {(segment) => <DateSegment segment={segment} className={segmentStyles} />}
        </DateInput>
        <span aria-hidden className="px-2 text-muted-foreground">
          –
        </span>
        <DateInput slot="end" className="flex items-center gap-0.5">
          {(segment) => <DateSegment segment={segment} className={segmentStyles} />}
        </DateInput>
        <TriggerButton />
      </Group>
      <Popover className={popoverStyles}>
        <Dialog>
          <RangeCalendar>
            <CalendarBody partialDates={partialDates} />
          </RangeCalendar>
        </Dialog>
      </Popover>
    </AriaDateRangePicker>
  );
}

/** "YYYY-MM-DD" for today in `timeZone` — never the browser's zone, never UTC. */
export function todayInZone(timeZone: string): string {
  return today(timeZone).toString();
}

export { getLocalTimeZone, type CalendarDate };
