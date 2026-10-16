import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { card } from '@/test/factories'
import { server } from '@/test/server'
import { API, problem, renderWithProviders } from '@/test/utils'
import type { ApplicationSummary } from '@/types/api'
import { KanbanPage } from './KanbanPage'

function mockBoard(initial: ApplicationSummary[]) {
  let cards = initial
  const statusRequests: unknown[] = []
  server.use(
    http.get(`${API}/applications/board`, () => HttpResponse.json(cards)),
    http.get(`${API}/companies`, () =>
      HttpResponse.json({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 }),
    ),
  )
  return {
    statusRequests,
    acceptMoves: () =>
      server.use(
        http.put(`${API}/applications/:id/status`, async ({ params, request }) => {
          const body = (await request.json()) as { status: ApplicationSummary['status'] }
          statusRequests.push({ id: params.id, ...body })
          cards = cards.map((c) =>
            c.id === params.id ? { ...c, status: body.status, allowedTransitions: ['offer'] } : c,
          )
          return HttpResponse.json(cards.find((c) => c.id === params.id))
        }),
      ),
    rejectMoves: () =>
      server.use(
        http.put(`${API}/applications/:id/status`, () =>
          problem(422, 'No se puede pasar de «Postulada» a «Entrevista».', 'application.invalid_transition'),
        ),
      ),
  }
}

const column = (name: string) => screen.getByRole('region', { name })

describe('Kanban board', () => {
  it('renders one column per status with its cards', async () => {
    mockBoard([
      card({ id: '1', jobTitle: 'Frontend Dev' }),
      card({ id: '2', jobTitle: 'Data Engineer', status: 'offer' }),
    ])
    renderWithProviders(<KanbanPage />)

    expect(await screen.findByText('Frontend Dev')).toBeInTheDocument()
    expect(within(column('Postulada')).getByText('Frontend Dev')).toBeInTheDocument()
    expect(within(column('Oferta recibida')).getByText('Data Engineer')).toBeInTheDocument()
    expect(
      within(column('Postulada')).getByText('Llamar al reclutador', { exact: false }),
    ).toBeInTheDocument()
  })

  it('moves a card with the accessible menu and persists the change', async () => {
    const board = mockBoard([card({ id: 'a1', jobTitle: 'Frontend Dev' })])
    board.acceptMoves()
    const user = userEvent.setup()
    renderWithProviders(<KanbanPage />)

    await user.click(await screen.findByRole('button', { name: 'Mover «Frontend Dev» a otro estado' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Entrevista' }))

    await waitFor(() => expect(within(column('Entrevista')).getByText('Frontend Dev')).toBeInTheDocument())
    expect(board.statusRequests).toEqual([
      { id: 'a1', status: 'interview', note: null, rejectionReason: null },
    ])
    expect(await screen.findByText('«Frontend Dev» movida a Entrevista')).toBeInTheDocument()
  })

  it('only offers the transitions the API allows', async () => {
    mockBoard([card({ id: 'a1', jobTitle: 'Frontend Dev', allowedTransitions: ['rejected', 'withdrawn'] })])
    const user = userEvent.setup()
    renderWithProviders(<KanbanPage />)

    await user.click(await screen.findByRole('button', { name: 'Mover «Frontend Dev» a otro estado' }))

    const items = await screen.findAllByRole('menuitem')
    expect(items.map((item) => item.textContent)).toEqual(['Rechazada', 'Retirada'])
  })

  it('rolls the card back and reports the error when the API rejects the move', async () => {
    const board = mockBoard([card({ id: 'a1', jobTitle: 'Frontend Dev' })])
    board.rejectMoves()
    const user = userEvent.setup()
    renderWithProviders(<KanbanPage />)

    await user.click(await screen.findByRole('button', { name: 'Mover «Frontend Dev» a otro estado' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Entrevista' }))

    expect(await screen.findByText(/No se pudo mover la postulación/)).toBeInTheDocument()
    await waitFor(() => expect(within(column('Postulada')).getByText('Frontend Dev')).toBeInTheDocument())
    expect(within(column('Entrevista')).queryByText('Frontend Dev')).not.toBeInTheDocument()
  })

  it('shows a helpful empty state for new users', async () => {
    mockBoard([])
    renderWithProviders(<KanbanPage />)

    expect(await screen.findByText('Tu tablero está vacío')).toBeInTheDocument()
  })
})
