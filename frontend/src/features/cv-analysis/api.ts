import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiRequest } from '@/lib/api-client'
import type {
  AnalysisProvider,
  Resume,
  ResumeAnalysis,
  ResumeAnalysisSummary,
  ResumeDetail,
} from '@/types/api'

export const resumeKeys = {
  all: ['resumes'] as const,
  list: ['resumes', 'list'] as const,
  detail: (id: string) => ['resumes', 'detail', id] as const,
  providers: ['resume-analyses', 'providers'] as const,
  analyses: (resumeId?: string) => ['resume-analyses', 'list', resumeId ?? null] as const,
  analysis: (id: string) => ['resume-analyses', 'detail', id] as const,
}

export const MAX_RESUME_BYTES = 5 * 1024 * 1024

export function useResumes() {
  return useQuery({
    queryKey: resumeKeys.list,
    queryFn: ({ signal }) => api.get<Resume[]>('/resumes', undefined, signal),
  })
}

export function useResumeDetail(id: string | null) {
  return useQuery({
    queryKey: resumeKeys.detail(id ?? ''),
    queryFn: ({ signal }) => api.get<ResumeDetail>(`/resumes/${id}`, undefined, signal),
    enabled: Boolean(id),
  })
}

export function useUploadResume() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('file', file)
      return apiRequest<Resume>('/resumes', { method: 'POST', body: form })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resumeKeys.all }),
  })
}

export function useDeleteResume() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/resumes/${id}`),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: resumeKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['resume-analyses'] }),
        queryClient.invalidateQueries({ queryKey: ['applications'] }),
      ]),
  })
}

export function useAnalysisProviders() {
  return useQuery({
    queryKey: resumeKeys.providers,
    queryFn: ({ signal }) => api.get<AnalysisProvider[]>('/resume-analyses/providers', undefined, signal),
    staleTime: 5 * 60_000,
  })
}

export function useAnalyses(resumeId?: string) {
  return useQuery({
    queryKey: resumeKeys.analyses(resumeId),
    queryFn: ({ signal }) => api.get<ResumeAnalysisSummary[]>('/resume-analyses', { resumeId }, signal),
  })
}

export function useAnalysis(id: string | null) {
  return useQuery({
    queryKey: resumeKeys.analysis(id ?? ''),
    queryFn: ({ signal }) => api.get<ResumeAnalysis>(`/resume-analyses/${id}`, undefined, signal),
    enabled: Boolean(id),
  })
}

export interface CreateAnalysisInput {
  resumeId: string
  jobOfferId: string
  provider: string
  consentToExternalProcessing: boolean
}

export function useCreateAnalysis() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateAnalysisInput) => api.post<ResumeAnalysis>('/resume-analyses', body),
    onSuccess: (analysis) => {
      queryClient.setQueryData(resumeKeys.analysis(analysis.summary.id), analysis)
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: ['resume-analyses', 'list'] }),
        queryClient.invalidateQueries({ queryKey: resumeKeys.list }),
      ])
    },
  })
}

export function useDeleteAnalysis() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/resume-analyses/${id}`),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['resume-analyses'] }),
        queryClient.invalidateQueries({ queryKey: resumeKeys.list }),
      ]),
  })
}
