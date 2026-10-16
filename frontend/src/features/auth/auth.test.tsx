import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/server'
import { API, problem, renderWithProviders, session } from '@/test/utils'
import { LoginPage } from './LoginPage'
import { RegisterPage } from './RegisterPage'
import { RedirectIfAuthenticated, RequireAuth } from './RequireAuth'

describe('protected routes', () => {
  it('redirect anonymous visitors to the login screen', async () => {
    renderWithProviders(
      <RequireAuth>
        <p>Contenido privado</p>
      </RequireAuth>,
      { route: '/kanban', path: '/kanban', signedIn: false },
    )

    expect(await screen.findByText('Pantalla de login')).toBeInTheDocument()
    expect(screen.queryByText('Contenido privado')).not.toBeInTheDocument()
  })

  it('show private content once the session is restored from the refresh cookie', async () => {
    renderWithProviders(
      <RequireAuth>
        <p>Contenido privado</p>
      </RequireAuth>,
      { route: '/kanban', path: '/kanban', signedIn: true },
    )

    expect(await screen.findByText('Contenido privado')).toBeInTheDocument()
  })
})

describe('login', () => {
  const renderLogin = () =>
    renderWithProviders(
      <RedirectIfAuthenticated>
        <LoginPage />
      </RedirectIfAuthenticated>,
      {
        route: '/entrar',
        path: '/entrar',
        signedIn: false,
        extraRoutes: [{ path: '/', element: <p>Panel</p> }],
      },
    )

  it('validates the form before calling the API', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.click(await screen.findByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Introduce un correo electrónico válido.')).toBeInTheDocument()
    expect(screen.getByText('Introduce tu contraseña.')).toBeInTheDocument()
    expect(screen.getByLabelText(/Correo electrónico/)).toHaveAttribute('aria-invalid', 'true')
  })

  it('shows the API error for wrong credentials', async () => {
    server.use(http.post(`${API}/auth/login`, () => problem(401, 'Credenciales no válidas.', 'unauthorized')))
    const user = userEvent.setup()
    renderLogin()

    await user.type(await screen.findByLabelText(/Correo electrónico/), 'ana@test.local')
    await user.type(screen.getByLabelText(/Contraseña/), 'Wrong123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Credenciales no válidas.')
  })

  it('signs in and navigates to the dashboard', async () => {
    let body: unknown
    server.use(
      http.post(`${API}/auth/login`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(session)
      }),
    )
    const user = userEvent.setup()
    renderLogin()

    await user.type(await screen.findByLabelText(/Correo electrónico/), 'ana@test.local')
    await user.type(screen.getByLabelText(/Contraseña/), 'Secret123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Panel')).toBeInTheDocument()
    expect(body).toEqual({ email: 'ana@test.local', password: 'Secret123' })
  })
})

describe('registration', () => {
  it('enforces the password policy and maps a taken email to the field', async () => {
    server.use(
      http.post(`${API}/auth/register`, () =>
        problem(409, 'Ya existe una cuenta con ese correo electrónico.', 'auth.email_taken'),
      ),
    )
    const user = userEvent.setup()
    renderWithProviders(<RegisterPage />, { route: '/register', path: '/register', signedIn: false })

    await user.type(await screen.findByLabelText(/Nombre completo/), 'Ana')
    await user.type(screen.getByLabelText(/Correo electrónico/), 'ana@test.local')
    await user.type(screen.getByLabelText(/^Contraseña/), 'weak')
    await user.type(screen.getByLabelText(/Repite la contraseña/), 'weak')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByText('Mínimo 8 caracteres.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText(/^Contraseña/))
    await user.type(screen.getByLabelText(/^Contraseña/), 'Strong123')
    await user.clear(screen.getByLabelText(/Repite la contraseña/))
    await user.type(screen.getByLabelText(/Repite la contraseña/), 'Strong123')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))

    await waitFor(() =>
      expect(screen.getByText('Ya existe una cuenta con ese correo electrónico.')).toBeInTheDocument(),
    )
  })
})
