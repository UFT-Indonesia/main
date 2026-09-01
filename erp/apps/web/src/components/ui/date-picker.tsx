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

export interface BlockedRange {
  /** "YYYY-MM-DD", inclusive. */
  startDate: string;
  /** "YYYY-MM-DD", inclusive. */
  endDate: string;
}

const fieldStyles =
  'flex h-9 w-full items-center rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-within:outline-none focus-within:ring-2 focus-within:ring-ring disabled:cursor-not-allowed disabled:opacity-50';

const segmentStyles =
  'rounded px-0.5 tabular-nums outline-none focus:bg-primary focus:text-primary-foreground placeholder-shown:text-muted-foreground';

const cellStyles =
  'm-0.5 flex h-7 w-7 cursor-default items-center justify-center rounded-md text-sm outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring selected:bg-primary selected:text-primary-foreground disabled:cursor-not-allowed disabled:text-muted-foreground/40 disabled:line-through disabled:hover:bg-transparent unavailable:cursor-not-allowed unavailable:bg-muted unavailable:text-muted-foreground/50 unavailable:hover:bg-muted';

const popoverStyles =
  'z-50 rounded-md border border-border bg-background p-3 text-foreground shadow-md';

/** Turns the API's blocked ranges into the matcher `isDateUnavailable` wants. */
function unavailable(ranges: BlockedRange[] | undefined) {
  if (!ranges?.length) return undefined;
  const parsed = ranges.map((r) => [parseDate(r.startDate), parseDate(r.endDate)] as const);
  return (date: DateValue) =>
    parsed.some(([from, to]) => date.compare(from) >= 0 && date.compare(to) <= 0);
}

function CalendarBody() {
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
        <CalendarGridBody>{(date) => <CalendarCell date={date} className={cellStyles} />}</CalendarGridBody>
      </CalendarGrid>
    </>
  );
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
  blocked?: BlockedRange[];
  isDisabled?: boolean;
  'aria-label'?: string;
  className?: string;
}

/** Single date. Value in and out is `"YYYY-MM-DD"`; empty string means unset. */
export function DatePickerField({
  value,
  onChange,
  blocked,
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
      isDateUnavailable={unavailable(blocked)}
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
  blocked?: BlockedRange[];
  isDisabled?: boolean;
  'aria-label'?: string;
  className?: string;
}

/**
 * Date + time in one segmented field. Value in and out is a UTC ISO instant; the segments show
 * the wall clock in `timeZone`, and `hideTimeZone` keeps the zone abbreviation out of the UI —
 * everyone here is in the same zone, so displaying it is noise.
 */
export function DateTimePickerField({
  value,
  onChange,
  timeZone,
  blocked,
  isDisabled,
  className,
  ...rest
}: DateTimePickerFieldProps) {
  const parsed: ZonedDateTime | null = value ? parseAbsolute(value, timeZone) : null;

  return (
    <AriaDatePicker
      aria-label={rest['aria-label']}
      granularity="minute"
      hideTimeZone
      value={parsed}
      onChange={(next) => onChange(next ? next.toDate().toISOString() : '')}
      isDisabled={isDisabled}
      isDateUnavailable={unavailable(blocked)}
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

interface DateRangePickerFieldProps {
  /** Both "YYYY-MM-DD"; empty strings mean unset. */
  start: string;
  end: string;
  onChange: (start: string, end: string) => void;
  blocked?: BlockedRange[];
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
  blocked,
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
      isDateUnavailable={unavailable(blocked)}
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
            <CalendarBody />
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
