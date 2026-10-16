import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type {
  ApplicationDetail,
  ApplicationStatus,
  ApplicationSummary,
  ChangeStatusRequest,
  CreateApplicationRequest,
  PagedResult,
  UpdateApplicationRequest,
} from '@/types/api'

export interface ApplicationQuery {
  search?: string
  status?: ApplicationStatus[]
  companyId?: string
  from?: string
  to?: string
  sort?: 'updated' | 'appliedOn' | 'company' | 'followUp'
  desc?: boolean
  page?: number
  pageSize?: number
}

export interface BoardFilters {
  search?: string
  companyId?: string
  from?: string
  to?: string
}

export const applicationKeys = {
  all: ['applications'] as const,
  list: (query: ApplicationQuery) => ['applications', 'list', query] as const,
  board: (filters: BoardFilters) => ['applications', 'board', filters] as const,
  detail: (id: string) => ['applications', 'detail', id] as const,
}

export function useApplications(query: ApplicationQuery) {
  return useQuery({
    queryKey: applicationKeys.list(query),
    queryFn: ({ signal }) => api.get<PagedResult<ApplicationSummary>>('/applications', { ...query }, signal),
    placeholderData: keepPreviousData,
  })
}

export function useBoard(filters: BoardFilters) {
  return useQuery({
    queryKey: applicationKeys.board(filters),
    queryFn: ({ signal }) => api.get<ApplicationSummary[]>('/applications/board', { ...filters }, signal),
    placeholderData: keepPreviousData,
  })
}

export function useApplication(id: string) {
  return useQuery({
    queryKey: applicationKeys.detail(id),
    queryFn: ({ signal }) => api.get<ApplicationDetail>(`/applications/${id}`, undefined, signal),
  })
}

function useInvalidateApplications() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all(
      [['applications'], ['jobs'], ['companies'], ['dashboard'], ['calendar']].map((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      ),
    )
}

export function useCreateApplication() {
  const invalidate = useInvalidateApplications()
  return useMutation({
    mutationFn: (body: CreateApplicationRequest) => api.post<ApplicationDetail>('/applications', body),
    onSuccess: invalidate,
  })
}

export function useUpdateApplication(id: string) {
  const invalidate = useInvalidateApplications()
  return useMutation({
    mutationFn: (body: UpdateApplicationRequest) => api.put<ApplicationDetail>(`/applications/${id}`, body),
    onSuccess: invalidate,
  })
}

export function useDeleteApplication() {
  const invalidate = useInvalidateApplications()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/applications/${id}`),
    onSuccess: invalidate,
  })
}

interface ChangeStatusVariables extends ChangeStatusRequest {
  id: string
}

/**
 * Persists a status change. Board caches are updated optimistically so a dragged card stays where it was dropped;
 * if the API rejects the move the previous state is restored and the error is re-thrown for the caller to report.
 */
export function useChangeStatus() {
  const queryClient = useQueryClient()
  const invalidate = useInvalidateApplications()

  return useMutation({
    mutationFn: ({ id, ...body }: ChangeStatusVariables) =>
      api.put<ApplicationSummary>(`/applications/${id}/status`, body),
    onMutate: async ({ id, status }) => {
      await queryClient.cancelQueries({ queryKey: ['applications', 'board'] })
      const snapshots = queryClient.getQueriesData<ApplicationSummary[]>({
        queryKey: ['applications', 'board'],
      })
      queryClient.setQueriesData<ApplicationSummary[]>({ queryKey: ['applications', 'board'] }, (cards) =>
        cards?.map((card) => (card.id === id ? { ...card, status, allowedTransitions: [] } : card)),
      )
      return { snapshots }
    },
    onError: (_error, _variables, context) => {
      context?.snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data))
    },
    onSettled: invalidate,
  })
}
