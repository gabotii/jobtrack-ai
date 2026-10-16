import type { ApplicationSummary } from '@/types/api'

export function card(overrides: Partial<ApplicationSummary> = {}): ApplicationSummary {
  return {
    id: 'a1',
    jobOfferId: 'o1',
    jobTitle: 'Backend Developer',
    companyId: 'c1',
    companyName: 'Acme',
    location: 'Madrid',
    workMode: 'hybrid',
    status: 'applied',
    statusChangedAt: '2026-10-01T10:00:00.000Z',
    appliedOn: '2026-10-01',
    nextAction: 'Llamar al reclutador',
    followUpOn: null,
    createdAt: '2026-10-01T10:00:00.000Z',
    updatedAt: '2026-10-01T10:00:00.000Z',
    allowedTransitions: ['screening', 'interview', 'offer', 'rejected', 'withdrawn'],
    ...overrides,
  }
}
