import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { PagedResult, Reminder, SaveReminderRequest } from '@/types/api'

export interface ReminderQuery {
  status?: 'pending' | 'completed' | 'all'
  applicationId?: string
  page?: number
  pageSize?: number
}

export const reminderKeys = {
  all: ['reminders'] as const,
  list: (query: ReminderQuery) => ['reminders', 'list', query] as const,
}

export function useReminders(query: ReminderQuery) {
  return useQuery({
    queryKey: reminderKeys.list(query),
    queryFn: ({ signal }) => api.get<PagedResult<Reminder>>('/reminders', { ...query }, signal),
    placeholderData: keepPreviousData,
  })
}

function useInvalidateReminders() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: reminderKeys.all }),
      queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
    ])
}

export function useSaveReminder(id?: string) {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: (body: SaveReminderRequest) =>
      id ? api.put<Reminder>(`/reminders/${id}`, body) : api.post<Reminder>('/reminders', body),
    onSuccess: invalidate,
  })
}

export function useToggleReminder() {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: ({ id, completed }: { id: string; completed: boolean }) =>
      api.post<Reminder>(`/reminders/${id}/${completed ? 'complete' : 'reopen'}`),
    onSuccess: invalidate,
  })
}

export function useDeleteReminder() {
  const invalidate = useInvalidateReminders()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/reminders/${id}`),
    onSuccess: invalidate,
  })
}
