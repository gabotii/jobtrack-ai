import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { CalendarEvent, CalendarEventStatus, SaveCalendarEventRequest } from '@/types/api'

export const calendarKeys = {
  all: ['calendar'] as const,
  range: (from: string, to: string) => ['calendar', 'range', from, to] as const,
  upcoming: (applicationId?: string, limit = 5) =>
    ['calendar', 'upcoming', applicationId ?? null, limit] as const,
}

/** Events overlapping [from, to) — both UTC ISO instants. */
export function useCalendarEvents(from: string | null, to: string | null) {
  return useQuery({
    queryKey: calendarKeys.range(from ?? '', to ?? ''),
    queryFn: ({ signal }) => api.get<CalendarEvent[]>('/calendar/events', { from, to }, signal),
    enabled: Boolean(from && to),
    placeholderData: keepPreviousData,
  })
}

export function useUpcomingEvents(applicationId?: string, limit = 5) {
  return useQuery({
    queryKey: calendarKeys.upcoming(applicationId, limit),
    queryFn: ({ signal }) =>
      api.get<CalendarEvent[]>('/calendar/events/upcoming', { applicationId, limit }, signal),
  })
}

function useInvalidateCalendar() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: calendarKeys.all }),
      queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
    ])
}

export function useSaveEvent(id?: string) {
  const invalidate = useInvalidateCalendar()
  return useMutation({
    mutationFn: (body: SaveCalendarEventRequest) =>
      id
        ? api.put<CalendarEvent>(`/calendar/events/${id}`, body)
        : api.post<CalendarEvent>('/calendar/events', body),
    onSuccess: invalidate,
  })
}

export function useChangeEventStatus() {
  const invalidate = useInvalidateCalendar()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: CalendarEventStatus }) =>
      api.put<CalendarEvent>(`/calendar/events/${id}/status`, { status }),
    onSuccess: invalidate,
  })
}

export function useDeleteEvent() {
  const invalidate = useInvalidateCalendar()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/calendar/events/${id}`),
    onSuccess: invalidate,
  })
}
