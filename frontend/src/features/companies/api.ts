import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { Company, PagedResult, SaveCompanyRequest } from '@/types/api'

export interface CompanyQuery {
  search?: string
  sort?: 'name' | 'createdAt'
  desc?: boolean
  page?: number
  pageSize?: number
}

export const companyKeys = {
  all: ['companies'] as const,
  list: (query: CompanyQuery) => ['companies', 'list', query] as const,
  detail: (id: string) => ['companies', 'detail', id] as const,
}

export function useCompanies(query: CompanyQuery) {
  return useQuery({
    queryKey: companyKeys.list(query),
    queryFn: ({ signal }) => api.get<PagedResult<Company>>('/companies', { ...query }, signal),
    placeholderData: keepPreviousData,
  })
}

/** All companies for selectors (the API caps a page at 100; enough for a personal job search). */
export function useCompanyOptions() {
  return useCompanies({ pageSize: 100, sort: 'name' })
}

export function useCompany(id: string) {
  return useQuery({
    queryKey: companyKeys.detail(id),
    queryFn: ({ signal }) => api.get<Company>(`/companies/${id}`, undefined, signal),
  })
}

function useInvalidateCompanies() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: companyKeys.all }),
      queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
    ])
}

export function useSaveCompany(id?: string) {
  const invalidate = useInvalidateCompanies()
  return useMutation({
    mutationFn: (body: SaveCompanyRequest) =>
      id ? api.put<Company>(`/companies/${id}`, body) : api.post<Company>('/companies', body),
    onSuccess: invalidate,
  })
}

export function useDeleteCompany() {
  const invalidate = useInvalidateCompanies()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/companies/${id}`),
    onSuccess: invalidate,
  })
}
