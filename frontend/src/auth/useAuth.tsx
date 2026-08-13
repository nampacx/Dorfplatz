import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import type { Me, Role } from '../types'

interface AuthState {
  me: Me | null
  loading: boolean
  hasRole: (role: Role) => boolean
  isModerator: boolean
  isAdmin: boolean
  /** Signed in AND let onto the board by an admin. Signing in is not enough. */
  isApproved: boolean
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthState | undefined>(undefined)

/** SWA identity payload from /.auth/me — tells us whether anyone is signed in at all. */
interface ClientPrincipal {
  userId: string
  userRoles: string[]
  identityProvider: string
  userDetails: string
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)

  async function refresh() {
    setLoading(true)
    try {
      const res = await fetch('/.auth/me')
      const data = (await res.json()) as { clientPrincipal: ClientPrincipal | null }
      if (!data.clientPrincipal) {
        setMe(null)
        return
      }
      // Signed in — load the app-level profile (roles resolved server-side).
      const profile = await api.get<Me>('/account/me')
      setMe(profile)
    } catch {
      setMe(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void refresh()
  }, [])

  const hasRole = (role: Role) => me?.roles.includes(role) ?? false

  return (
    <AuthContext.Provider
      value={{
        me,
        loading,
        hasRole,
        isModerator: hasRole('moderator') || hasRole('admin'),
        isAdmin: hasRole('admin'),
        isApproved: me?.status === 'Approved',
        refresh,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
