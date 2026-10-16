import { TZDate } from '@date-fns/tz'
import { format, formatDistanceToNowStrict, parseISO } from 'date-fns'
import { es } from 'date-fns/locale'
import type { Instant, IsoDate } from '@/types/api'

/**
 * Time-zone rules used across the UI:
 * - The API stores instants in UTC. The UI shows them in the user's profile time zone (the same zone the API uses
 *   to compute "today"), so an interview at 10:00 Madrid time always reads 10:00 for a Madrid-based user.
 * - Forms collect a wall-clock date + time + IANA zone and convert them to a UTC instant with {@link toInstant}.
 */

export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
}

export function supportedTimeZones(): string[] {
  try {
    return Intl.supportedValuesOf('timeZone')
  } catch {
    return ['UTC']
  }
}

/** UTC instant for a wall-clock date and time in an IANA zone. Times inside a DST gap move forward, like clocks do. */
export function toInstant(date: IsoDate, time: string, timeZone: string): Instant {
  const [year, month, day] = date.split('-').map(Number)
  const [hours, minutes] = time.split(':').map(Number)
  return new Date(new TZDate(year, month - 1, day, hours, minutes, timeZone).getTime()).toISOString()
}

/** Wall-clock date and time of an instant in a zone, for pre-filling forms. */
export function fromInstant(instant: Instant, timeZone: string): { date: IsoDate; time: string } {
  const zoned = new TZDate(parseISO(instant).getTime(), timeZone)
  return { date: format(zoned, 'yyyy-MM-dd'), time: format(zoned, 'HH:mm') }
}

/** "2026-10-14T10:00:00": the instant as a floating wall-clock string in the zone (used by the calendar grid). */
export function toWallClock(instant: Instant | Date, timeZone: string): string {
  const time = typeof instant === 'string' ? parseISO(instant).getTime() : instant.getTime()
  return format(new TZDate(time, timeZone), "yyyy-MM-dd'T'HH:mm:ss")
}

export function todayIn(timeZone: string): IsoDate {
  return format(new TZDate(Date.now(), timeZone), 'yyyy-MM-dd')
}

export function formatDate(value: IsoDate | null | undefined): string {
  if (!value) return '—'
  return format(parseISO(value), 'd MMM yyyy', { locale: es })
}

export function formatDateTime(instant: Instant, timeZone: string): string {
  return format(new TZDate(parseISO(instant).getTime(), timeZone), 'EEE d MMM yyyy, HH:mm', { locale: es })
}

export function formatTime(instant: Instant, timeZone: string): string {
  return format(new TZDate(parseISO(instant).getTime(), timeZone), 'HH:mm')
}

export function formatRelative(instant: Instant): string {
  return formatDistanceToNowStrict(parseISO(instant), { addSuffix: true, locale: es })
}

/** "lun 13 oct" style label for chart axes and compact lists. */
export function formatShortDate(value: IsoDate): string {
  return format(parseISO(value), 'd MMM', { locale: es })
}

export function isPast(instant: Instant): boolean {
  return parseISO(instant).getTime() < Date.now()
}
