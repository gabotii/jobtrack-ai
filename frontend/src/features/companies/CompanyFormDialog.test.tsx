import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '@/test/server'
import { API, problem, renderWithProviders } from '@/test/utils'
import { CompanyFormDialog } from './CompanyFormDialog'

describe('company form', () => {
  it('validates the name, URL and email on the client', async () => {
    const user = userEvent.setup()
    renderWithProviders(<CompanyFormDialog open onOpenChange={() => {}} />)

    await user.type(await screen.findByLabelText(/Sitio web/), 'acme.com')
    await user.type(screen.getByLabelText('Correo de contacto'), 'no-es-un-correo')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('El nombre es obligatorio.')).toBeInTheDocument()
    expect(screen.getByText('Debe empezar por http:// o https://')).toBeInTheDocument()
    expect(screen.getByText('Correo no válido.')).toBeInTheDocument()
  })

  it('sends empty optional fields as null and closes on success', async () => {
    let body: Record<string, unknown> = {}
    server.use(
      http.post(`${API}/companies`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ id: 'c1', name: body.name }, { status: 201 })
      }),
    )
    const onOpenChange = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<CompanyFormDialog open onOpenChange={onOpenChange} />)

    await user.type(await screen.findByLabelText(/^Nombre/), '  Acme  ')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Empresa creada')).toBeInTheDocument()
    expect(onOpenChange).toHaveBeenCalledWith(false)
    expect(body).toMatchObject({ name: 'Acme', website: null, contactEmail: null, notes: null })
  })

  it('shows a duplicate name returned by the API on the name field', async () => {
    server.use(
      http.post(`${API}/companies`, () =>
        problem(409, 'Ya tienes una empresa registrada con ese nombre.', 'company.duplicate_name'),
      ),
    )
    const user = userEvent.setup()
    renderWithProviders(<CompanyFormDialog open onOpenChange={() => {}} />)

    await user.type(await screen.findByLabelText(/^Nombre/), 'Acme')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Ya tienes una empresa registrada con ese nombre.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Nombre/)).toHaveAttribute('aria-invalid', 'true')
  })
})
