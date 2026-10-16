import { describe, expect, it } from 'vitest'
import { fromInstant, toInstant, toWallClock } from './dates'

describe('time zone conversion', () => {
  it.each([
    ['2026-01-15', '10:00', 'Europe/Madrid', '2026-01-15T09:00:00.000Z'], // UTC+1 in winter
    ['2026-07-15', '10:00', 'Europe/Madrid', '2026-07-15T08:00:00.000Z'], // UTC+2 in summer
    ['2026-07-15', '10:00', 'America/Mexico_City', '2026-07-15T16:00:00.000Z'], // no DST, UTC-6
    ['2026-03-10', '09:30', 'America/New_York', '2026-03-10T13:30:00.000Z'], // two days after the US DST change
    ['2026-03-27', '23:30', 'Europe/Madrid', '2026-03-27T22:30:00.000Z'],
  ])('%s %s in %s is %s', (date, time, zone, expected) => {
    expect(toInstant(date, time, zone)).toBe(expected)
  })

  it('round-trips instants to the wall clock of the zone they were scheduled in', () => {
    const instant = toInstant('2026-10-25', '02:30', 'Europe/Madrid') // the hour that repeats when DST ends
    expect(fromInstant(instant, 'Europe/Madrid').date).toBe('2026-10-25')
    expect(fromInstant('2026-07-01T08:00:00.000Z', 'Europe/Madrid')).toEqual({
      date: '2026-07-01',
      time: '10:00',
    })
    expect(fromInstant('2026-07-01T08:00:00.000Z', 'America/Santiago')).toEqual({
      date: '2026-07-01',
      time: '04:00',
    })
  })

  it('renders floating wall-clock strings for the calendar grid', () => {
    expect(toWallClock('2026-07-01T23:30:00.000Z', 'Europe/Madrid')).toBe('2026-07-02T01:30:00')
  })
})
