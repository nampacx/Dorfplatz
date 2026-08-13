import { useState } from 'react'
import { api } from '../../api/client'
import { ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import type { AdminUser } from '../../types'

const STATUS_LABELS: Record<AdminUser['status'], string> = {
  Pending: 'Wartet',
  Approved: 'Freigegeben',
  Rejected: 'Abgelehnt',
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/**
 * People waiting to be let onto the board, shown above the member list because
 * it is the one thing on this page that needs acting on rather than reading.
 */
function ApprovalQueue({ users, onDecide }: { users: AdminUser[]; onDecide: () => void }) {
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const waiting = users.filter((u) => u.status === 'Pending')

  async function decide(u: AdminUser, approve: boolean) {
    setBusyId(u.id)
    setError(null)
    try {
      await api.post(`/members/${u.id}/approval`, { approve })
      onDecide()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="panel" style={{ marginBottom: '1.5rem' }}>
      <h2 style={{ marginTop: 0 }}>
        Freigaben{waiting.length > 0 && <span className="badge pending" style={{ marginLeft: '0.5rem' }}>{waiting.length}</span>}
      </h2>
      {error && <ErrorNotice message={error} />}
      {waiting.length === 0 ? (
        <p style={{ color: 'var(--ink-soft)', margin: 0 }}>Niemand wartet auf eine Freigabe.</p>
      ) : (
        <div className="approval-list">
          {waiting.map((u) => (
            <div key={u.id} className="approval-item">
              <div>
                <div style={{ fontWeight: 600 }}>{u.displayName ?? u.email ?? '—'}</div>
                <div className="u-label">{u.email} · seit {fmtDate(u.createdAt)}</div>
              </div>
              <div className="stack-actions">
                <button className="btn" disabled={busyId === u.id} onClick={() => decide(u, true)}>
                  Freigeben
                </button>
                <button className="btn secondary" disabled={busyId === u.id} onClick={() => decide(u, false)}>
                  Ablehnen
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function UserTable() {
  const { data, loading, error, reload } = useAsync(() => api.get<AdminUser[]>('/members'), [])

  async function toggle(u: AdminUser, role: 'moderator' | 'admin', grant: boolean) {
    await api.post(`/members/${u.id}/roles`, { role, grant })
    reload()
  }

  async function readmit(u: AdminUser) {
    await api.post(`/members/${u.id}/approval`, { approve: true })
    reload()
  }

  if (loading) return <Loading />
  if (error) return <ErrorNotice message={error} />

  return (
    <>
      <ApprovalQueue users={data ?? []} onDecide={reload} />
      <div className="panel">
      <table>
        <thead>
          <tr><th>Mitglied</th><th>Zugang</th><th>Rollen</th><th>Aktionen</th></tr>
        </thead>
        <tbody>
          {data?.map((u) => {
            const isMod = u.roles.includes('moderator')
            const isAdmin = u.roles.includes('admin')
            return (
              <tr key={u.id}>
                <td>
                  <div style={{ fontWeight: 600 }}>{u.displayName ?? '—'}</div>
                  <div style={{ color: 'var(--ink-soft)', fontSize: '0.82rem' }}>{u.email}</div>
                </td>
                <td>
                  <span className={`badge ${u.status === 'Approved' ? 'published' : u.status === 'Rejected' ? 'rejected' : 'pending'}`}>
                    {STATUS_LABELS[u.status]}
                  </span>
                </td>
                <td>{u.roles.map((r) => <span key={r} className="badge role" style={{ marginRight: 4 }}>{r}</span>)}</td>
                <td>
                  {u.status === 'Rejected' ? (
                    <button className="btn small secondary" onClick={() => readmit(u)}>Doch freigeben</button>
                  ) : (
                    <>
                      <button className="btn small secondary" onClick={() => toggle(u, 'moderator', !isMod)}>
                        {isMod ? 'Moderator entziehen' : 'Zum Moderator machen'}
                      </button>{' '}
                      <button className="btn small secondary" onClick={() => toggle(u, 'admin', !isAdmin)}>
                        {isAdmin ? 'Admin entziehen' : 'Zum Admin machen'}
                      </button>
                    </>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      </div>
    </>
  )
}

export default function AdminPage() {
  return (
    <RequireRole role="admin">
      <div className="page-head">
        <h1>Administration</h1>
        <p>Mitglieder verwalten und Moderatoren ernennen.</p>
      </div>
      <UserTable />
    </RequireRole>
  )
}
