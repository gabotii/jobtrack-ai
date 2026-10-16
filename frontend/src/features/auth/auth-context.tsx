import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { apiRequest, refreshSession, setAccessToken, setSessionExpiredHandler } from '@/lib/api-client'
import type { AuthResponse, User } from '@/types/api'

type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface RegisterInput {
  fullName: string
  email: string
  password: string
  timeZone: string
}

interface AuthContextValue {
  status: AuthStatus
  user: User | null
  login: (email: string, password: string) => Promise<void>
  register: (input: RegisterInput) => Promise<void>
  logout: () => Promise<void>
  /** Applies a new session (e.g. after a password change) or an updated profile. */
  applySession: (session: AuthResponse) => void
  setUser: (user: User) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUserState] = useState<User | null>(null)

  const applySession = useCallback((session: AuthResponse) => {
    setAccessToken(session.accessToken)
    setUserState(session.user)
    setStatus('authenticated')
  }, [])

  const clearSession = useCallback(() => {
    setAccessToken(null)
    setUserState(null)
    setStatus('anonymous')
    queryClient.clear()
  }, [queryClient])

  // Restore the session from the HttpOnly refresh cookie on first load.
  useEffect(() => {
    let cancelled = false
    refreshSession().then((session) => {
      if (cancelled) return
      if (session) applySession(session)
      else setStatus('anonymous')
    })
    return () => {
      cancelled = true
    }
  }, [applySession])

  useEffect(() => {
    setSessionExpiredHandler(clearSession)
    return () => setSessionExpiredHandler(null)
  }, [clearSession])

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      applySession,
      setUser: setUserState,
      login: async (email, password) => {
        const session = await apiRequest<AuthResponse>('/auth/login', {
          method: 'POST',
          body: { email, password },
          anonymous: true,
        })
        applySession(session)
      },
      register: async (input) => {
        const session = await apiRequest<AuthResponse>('/auth/register', {
          method: 'POST',
          body: input,
          anonymous: true,
        })
        applySession(session)
      },
      logout: async () => {
        try {
          await apiRequest<void>('/auth/logout', { method: 'POST', anonymous: true })
        } finally {
          clearSession()
        }
      },
    }),
    [status, user, applySession, clearSession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}

/** The signed-in user; only call inside routes protected by <RequireAuth>. */
export function useCurrentUser(): User {
  const { user } = useAuth()
  if (!user) throw new Error('useCurrentUser requires an authenticated session')
  return user
}
