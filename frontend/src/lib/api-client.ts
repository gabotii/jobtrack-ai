import type { AuthResponse } from '@/types/api'

export const API_BASE = '/api/v1'

/** Error returned by the API as RFC 9457 problem details. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, code?: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
  }
}

/** Human-readable message for any thrown value, suitable for a toast. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const firstFieldError = Object.values(error.fieldErrors)[0]?.[0]
    return firstFieldError ?? error.message
  }
  if (error instanceof TypeError) {
    return 'No se pudo conectar con el servidor. Revisa tu conexión.'
  }
  return 'Ha ocurrido un error inesperado.'
}

// The access token lives only in memory (never in localStorage) to limit the impact of XSS.
// The long-lived refresh token is an HttpOnly cookie the browser sends to /api/v1/auth only.
let accessToken: string | null = null
let refreshInFlight: Promise<AuthResponse | null> | null = null
let onSessionExpired: (() => void) | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function setSessionExpiredHandler(handler: (() => void) | null) {
  onSessionExpired = handler
}

type QueryValue = string | number | boolean | null | undefined | readonly (string | number)[]

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  query?: Record<string, QueryValue>
  signal?: AbortSignal
  /** Skip the Authorization header and the automatic refresh (auth endpoints). */
  anonymous?: boolean
}

export function buildQuery(query: Record<string, QueryValue> | undefined): string {
  if (!query) return ''
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue
    if (Array.isArray(value)) value.forEach((v) => params.append(key, String(v)))
    else params.append(key, String(value))
  }
  const text = params.toString()
  return text ? `?${text}` : ''
}

async function parseError(response: Response): Promise<ApiError> {
  let body: { detail?: string; title?: string; code?: string; errors?: Record<string, string[]> } = {}
  try {
    body = await response.json()
  } catch {
    // Non-JSON error bodies (e.g. a proxy error page) fall back to a generic message.
  }
  const fallback =
    response.status === 429
      ? 'Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.'
      : response.status >= 500
        ? 'El servidor no está disponible en este momento.'
        : 'La solicitud no se pudo completar.'
  return new ApiError(response.status, body.detail ?? body.title ?? fallback, body.code, body.errors ?? {})
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  let body: BodyInit | undefined
  if (options.body instanceof FormData) {
    body = options.body
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(options.body)
  }
  if (!options.anonymous && accessToken) {
    headers.Authorization = `Bearer ${accessToken}`
  }
  // Absolute URL on the page's own origin (same-origin keeps the SameSite=Strict refresh cookie working).
  const url = new URL(`${API_BASE}${path}${buildQuery(options.query)}`, window.location.origin)
  return fetch(url, {
    method: options.method ?? 'GET',
    headers,
    body,
    signal: options.signal,
    credentials: 'same-origin',
  })
}

async function refreshOnce(): Promise<AuthResponse | null> {
  const response = await send('/auth/refresh', { method: 'POST', anonymous: true })
  if (!response.ok) return null
  return (await response.json()) as AuthResponse
}

/**
 * Exchanges the refresh cookie for a new access token. Concurrent callers share one request. If another tab rotated
 * the cookie at the same moment, the first attempt fails; a single delayed retry then uses the new cookie.
 */
export function refreshSession(): Promise<AuthResponse | null> {
  refreshInFlight ??= (async () => {
    try {
      let session = await refreshOnce()
      if (!session) {
        await new Promise((resolve) => setTimeout(resolve, 400))
        session = await refreshOnce()
      }
      setAccessToken(session?.accessToken ?? null)
      return session
    } catch {
      return null
    } finally {
      refreshInFlight = null
    }
  })()
  return refreshInFlight
}

/** Calls the API; on 401 it refreshes the session once and retries before giving up. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options)

  if (response.status === 401 && !options.anonymous) {
    const session = await refreshSession()
    if (session) {
      response = await send(path, options)
    } else {
      onSessionExpired?.()
    }
  }

  if (!response.ok) {
    throw await parseError(response)
  }
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

/** Downloads a protected file and hands it to the browser (the Authorization header rules out a plain link). */
export async function downloadFile(path: string, fileName: string): Promise<void> {
  let response = await send(path, {})
  if (response.status === 401 && (await refreshSession())) {
    response = await send(path, {})
  }
  if (!response.ok) throw await parseError(response)
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export const api = {
  get: <T>(path: string, query?: RequestOptions['query'], signal?: AbortSignal) =>
    apiRequest<T>(path, { query, signal }),
  post: <T>(path: string, body?: unknown) => apiRequest<T>(path, { method: 'POST', body }),
  put: <T>(path: string, body?: unknown) => apiRequest<T>(path, { method: 'PUT', body }),
  delete: (path: string) => apiRequest<void>(path, { method: 'DELETE' }),
}
