import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { card } from '@/test/factories'
import { server } from '@/test/server'
import { API, problem, renderWithProviders } from '@/test/utils'
import type { JobOfferListItem } from '@/types/api'
import { CreateApplicationDialog } from './CreateApplicationDialog'

const offer: JobOfferListItem = {
  id: 'offer-1',
  companyId: 'c1',
  companyName: 'Acme',
  title: 'Backend Developer',
  location: 'Madrid',
  workMode: 'remote',
  contractType: 'fullTime',
  salaryMin: null,
  salaryMax: null,
  currency: null,
  requiredSkills: [],
  deadline: null,
  savedAt: '2026-10-01T10:00:00.000Z',
  application: null,
}

function mockLookups() {
  server.use(
    http.get(`${API}/jobs`, ({ request }) => {
      // The dialog must only list offers that do not have an application yet.
      expect(new URL(request.url).searchParams.get('hasApplication')).toBe('false')
      return HttpResponse.json({ items: [offer], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 })
    }),
    http.get(`${API}/resumes`, () => HttpResponse.json([])),
  )
}

const renderDialog = () =>
  renderWithProviders(<CreateApplicationDialog open onOpenChange={() => {}} />, {
    extraRoutes: [{ path: '/applications/:id', element: <p>Detalle de la postulación</p> }],
  })

describe('creating an application', () => {
  it('requires choosing an offer', async () => {
    mockLookups()
    const user = userEvent.setup()
    renderDialog()

    await screen.findByRole('option', { name: 'Backend Developer — Acme' })
    await user.click(screen.getByRole('button', { name: 'Crear postulación' }))

    expect(await screen.findByText('Selecciona una oferta.')).toBeInTheDocument()
  })

  it('creates the application and opens its detail', async () => {
    mockLookups()
    let body: Record<string, unknown> = {}
    server.use(
      http.post(`${API}/applications`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json(
          {
            summary: card({ id: 'new-app' }),
            history: [],
            resumeId: null,
            resumeFileName: null,
            coverLetter: null,
            source: 'linkedIn',
            notes: null,
            rejectionReason: null,
          },
          { status: 201 },
        )
      }),
    )
    const user = userEvent.setup()
    renderDialog()

    await screen.findByRole('option', { name: 'Backend Developer — Acme' })
    await user.selectOptions(screen.getByLabelText(/^Oferta/), 'offer-1')
    await user.selectOptions(screen.getByLabelText('Fuente'), 'linkedIn')
    await user.type(screen.getByLabelText('Próxima acción'), 'Escribir al reclutador')
    await user.click(screen.getByRole('button', { name: 'Crear postulación' }))

    expect(await screen.findByText('Detalle de la postulación')).toBeInTheDocument()
    expect(body).toMatchObject({
      jobOfferId: 'offer-1',
      status: 'applied',
      source: 'linkedIn',
      nextAction: 'Escribir al reclutador',
      resumeId: null,
    })
    expect(body.appliedOn).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('does not send an application date for offers not applied to yet', async () => {
    mockLookups()
    let body: Record<string, unknown> = {}
    server.use(
      http.post(`${API}/applications`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return problem(409, 'Ya existe una postulación para esta oferta.', 'application.duplicate')
      }),
    )
    const user = userEvent.setup()
    renderDialog()

    await screen.findByRole('option', { name: 'Backend Developer — Acme' })
    await user.selectOptions(screen.getByLabelText(/^Oferta/), 'offer-1')
    await user.selectOptions(screen.getByLabelText(/Estado inicial/), 'toApply')
    expect(screen.getByLabelText(/Fecha de postulación/)).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Crear postulación' }))

    expect(await screen.findByText('Ya existe una postulación para esta oferta.')).toBeInTheDocument()
    expect(body).toMatchObject({ status: 'toApply', appliedOn: null })
  })
})
