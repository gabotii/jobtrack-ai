import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '@/test/server'
import { API, problem, session } from '@/test/utils'
import {
  api,
  ApiError,
  buildQuery,
  errorMessage,
  setAccessToken,
  setSessionExpiredHandler,
} from './api-client'

describe('api client', () => {
  it('sends the access token and parses JSON', async () => {
    setAccessToken('abc')
    server.use(
      http.get(`${API}/companies`, ({ request }) =>
        HttpResponse.json({
          auth: request.headers.get('Authorization'),
          page: new URL(request.url).searchParams.get('page'),
        }),
      ),
    )

    const result = await api.get<{ auth: string; page: string }>('/companies', { page: 2, search: '' })

    expect(result).toEqual({ auth: 'Bearer abc', page: '2' })
  })

  it('refreshes the session once on 401 and retries the request', async () => {
    setAccessToken('expired')
    const seenTokens: (string | null)[] = []
    server.use(
      http.get(`${API}/dashboard`, ({ request }) => {
        const token = request.headers.get('Authorization')
        seenTokens.push(token)
        return token === `Bearer ${session.accessToken}`
          ? HttpResponse.json({ ok: true })
          : problem(401, 'expired')
      }),
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(session)),
    )

    await expect(api.get('/dashboard')).resolves.toEqual({ ok: true })
    expect(seenTokens).toEqual(['Bearer expired', `Bearer ${session.accessToken}`])
  })

  it('reports an expired session when the refresh also fails', async () => {
    const expired = vi.fn()
    setSessionExpiredHandler(expired)
    server.use(
      http.get(`${API}/dashboard`, () => problem(401, 'expired')),
      http.post(`${API}/auth/refresh`, () => problem(401, 'no session')),
    )

    await expect(api.get('/dashboard')).rejects.toBeInstanceOf(ApiError)
    expect(expired).toHaveBeenCalledOnce()
    setSessionExpiredHandler(null)
  })

  it('turns problem details into ApiError with code and field errors', async () => {
    server.use(
      http.post(`${API}/companies`, () =>
        problem(400, 'Revisa los datos enviados.', 'validation_failed', {
          name: ['El nombre es obligatorio.'],
        }),
      ),
    )

    const error = await api.post('/companies', {}).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).code).toBe('validation_failed')
    expect((error as ApiError).fieldErrors.name).toEqual(['El nombre es obligatorio.'])
    expect(errorMessage(error)).toBe('El nombre es obligatorio.')
  })

  it('uses friendly fallbacks for non-JSON errors and network failures', async () => {
    server.use(http.get(`${API}/broken`, () => new HttpResponse('<html>Bad gateway</html>', { status: 502 })))

    const error = await api.get('/broken').catch((e: unknown) => e)

    expect(errorMessage(error)).toBe('El servidor no está disponible en este momento.')
    expect(errorMessage(new TypeError('Failed to fetch'))).toMatch(/No se pudo conectar/)
  })

  it('builds query strings with arrays and skips empty values', () => {
    expect(buildQuery({ status: ['applied', 'interview'], search: '', page: 1, companyId: undefined })).toBe(
      '?status=applied&status=interview&page=1',
    )
  })
})
