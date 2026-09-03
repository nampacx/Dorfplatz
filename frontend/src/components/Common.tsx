import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import ProviderButtons from '../auth/ProviderButtons'
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

/** Prompts sign-in in place, offering the same providers as the login page. */
export function LoginGate({ message }: { message?: string }) {
  return (
    <div className="gate">
      <h2>Anmeldung erforderlich</h2>
      <p>{message ?? 'Bitte melde dich an, um fortzufahren.'}</p>
      <ProviderButtons />
    </div>
  )
}

/**
 * Shown to someone who has signed in but may not use the board — not let on
 * yet, turned away, or suspended.
 *
 * It names the account, because the most common confusion is having signed in
 * with the wrong one of several Microsoft accounts, and offers the way out —
 * signing out — rather than leaving them on a dead end. A suspended member is
 * shown the reason an admin gave, so they know what happened without having to
 * ask.
 */
export function PendingGate() {
  const { me } = useAuth()
  const suspended = me?.status === 'Suspended'
  const rejected = me?.status === 'Rejected'
  return (
    <div className="gate">
      <h2>{suspended ? 'Zugang gesperrt' : rejected ? 'Kein Zugang' : 'Dein Zugang wird geprüft'}</h2>
      <p>
        {suspended
          ? 'Ein Administrator hat den Zugang zu diesem Konto vorübergehend gesperrt.'
          : rejected
            ? 'Dieses Konto wurde nicht für das schwarze Brett freigegeben.'
            : 'Ein Administrator muss dein Konto freigeben, bevor du Dienstleistungen, den Marktplatz und Nachrichten nutzen kannst. Events kannst du schon jetzt lesen.'}
      </p>
      {suspended && me?.statusReason && (
        <div className="notice error" style={{ marginBottom: '1rem' }}>
          <strong>Begründung:</strong> {me.statusReason}
        </div>
      )}
      <p className="u-label" style={{ marginBottom: '1.5rem' }}>
        Angemeldet als {me?.email ?? me?.displayName ?? 'unbekannt'}
      </p>
      <div className="stack-actions" style={{ maxWidth: 320, margin: '0 auto' }}>
        <Link className="btn" to="/events">Zu den Events</Link>
        <a className="btn secondary" href="/.auth/logout?post_logout_redirect_uri=/">
          Mit einem anderen Konto anmelden
        </a>
      </div>
    </div>
  )
}

/**
 * Gate content behind membership: signed in, and approved. Used by the parts
 * of the board that are for members only — Dienstleistungen, Marktplatz, chat.
 */
export function RequireMember({ message, children }: { message?: string; children: ReactNode }) {
  const { me, loading, isApproved } = useAuth()
  if (loading) return <Loading />
  if (!me) return <LoginGate message={message} />
  if (!isApproved) return <PendingGate />
  return <>{children}</>
}

/** Gate content behind auth and (optionally) a role. */
export function RequireRole({ role, children }: { role?: Role; children: ReactNode }) {
  const { me, loading, hasRole, isApproved } = useAuth()
  if (loading) return <Loading />
  if (!me) return <LoginGate />
  // A role granted to an account still waiting on approval does not open
  // anything: the server checks approval before it checks the role.
  if (!isApproved) return <PendingGate />
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
