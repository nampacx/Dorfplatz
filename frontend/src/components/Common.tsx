import { useEffect, useState, type ReactNode } from 'react'
import { useAuth } from '../auth/useAuth'
import type { Role } from '../types'

export function Loading({ label = 'Lädt…' }: { label?: string }) {
  return <div className="empty">{label}</div>
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="empty">{children}</div>
}

export function ErrorNotice({ message }: { message: string }) {
  return <div className="notice error">{message}</div>
}

/** Prompts sign-in via SWA. Both providers are offered. */
export function LoginGate({ message }: { message?: string }) {
  return (
    <div className="gate">
      <h2>Anmeldung erforderlich</h2>
      <p>{message ?? 'Bitte melde dich an, um fortzufahren.'}</p>
      <div className="providers">
        <a className="btn" href="/.auth/login/aad?post_login_redirect_uri=/">Mit Microsoft anmelden</a>
        <a className="btn secondary" href="/.auth/login/google?post_login_redirect_uri=/">Mit Google anmelden</a>
      </div>
    </div>
  )
}

/** Gate content behind auth and (optionally) a role. */
export function RequireRole({ role, children }: { role?: Role; children: ReactNode }) {
  const { me, loading, hasRole } = useAuth()
  if (loading) return <Loading />
  if (!me) return <LoginGate />
  if (role && !(hasRole(role) || hasRole('admin'))) {
    return <EmptyState>Für diesen Bereich fehlt dir die Berechtigung.</EmptyState>
  }
  return <>{children}</>
}

/** Tiny data-loading hook. */
export function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const reload = () => {
    setLoading(true)
    setError(null)
    fn()
      .then(setData)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(reload, deps)
  return { data, error, loading, reload, setData }
}
