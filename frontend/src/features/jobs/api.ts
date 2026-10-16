import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type {
  ContractType,
  JobOffer,
  JobOfferListItem,
  PagedResult,
  SaveJobOfferRequest,
  WorkMode,
} from '@/types/api'

export interface JobQuery {
  search?: string
  companyId?: string
  workMode?: WorkMode
  contractType?: ContractType
  hasApplication?: boolean
  sort?: 'savedAt' | 'title' | 'deadline' | 'company'
  desc?: boolean
  page?: number
  pageSize?: number
}

export const jobKeys = {
  all: ['jobs'] as const,
  list: (query: JobQuery) => ['jobs', 'list', query] as const,
  detail: (id: string) => ['jobs', 'detail', id] as const,
}

export function useJobs(query: JobQuery, enabled = true) {
  return useQuery({
    queryKey: jobKeys.list(query),
    queryFn: ({ signal }) => api.get<PagedResult<JobOfferListItem>>('/jobs', { ...query }, signal),
    placeholderData: keepPreviousData,
    enabled,
  })
}

export function useJob(id: string) {
  return useQuery({
    queryKey: jobKeys.detail(id),
    queryFn: ({ signal }) => api.get<JobOffer>(`/jobs/${id}`, undefined, signal),
  })
}

function useInvalidateJobs() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all(
      [['jobs'], ['companies'], ['applications'], ['dashboard']].map((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      ),
    )
}

export function useSaveJob(id?: string) {
  const invalidate = useInvalidateJobs()
  return useMutation({
    mutationFn: (body: SaveJobOfferRequest) =>
      id ? api.put<JobOffer>(`/jobs/${id}`, body) : api.post<JobOffer>('/jobs', body),
    onSuccess: invalidate,
  })
}

export function useDeleteJob() {
  const invalidate = useInvalidateJobs()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/jobs/${id}`),
    onSuccess: invalidate,
  })
}
