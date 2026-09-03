import { useState } from 'react'
import { api } from '../../api/client'
import { ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import type { AccessDecision, AdminUser } from '../../types'

const STATUS_LABELS: Record<AdminUser['status'], string> = {
  Pending: 'Wartet',
  Approved: 'Freigegeben',
  Rejected: 'Abgelehnt',
  Suspended: 'Gesperrt',
}

const MAX_REASON = 500

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/** POST an access decision. Reason is only sent when one was asked for. */
function decideAccess(user: AdminUser, decision: AccessDecision, reason?: string) {
  return api.post(`/members/${user.id}/approval`, { decision, reason })
}

/**
 * Asks for the reason behind taking access away or giving it back.
 *
 * The reason is mandatory — that is the whole point of the dialog, it is what
 * ends up in the audit log — so the confirm button stays disabled until
 * something has been typed.
 */
function ReasonDialog({
  title,
  intro,
  label,
  confirmLabel,
  danger,
  onCancel,
  onConfirm,
}: {
  title: string
  intro: string
  label: string
  confirmLabel: string
  danger?: boolean
  onCancel: () => void
  onConfirm: (reason: string) => Promise<void>
}) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function confirm() {
    if (!reason.trim()) return
    setBusy(true)
    setError(null)
    try {
      await onConfirm(reason.trim())
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <Sheet title={title} onClose={onCancel}>
      <div>
        <h2 style={{ marginTop: 0 }}>{title}</h2>
        <p style={{ color: 'var(--ink-soft)' }}>{intro}</p>
        {error && <ErrorNotice message={error} />}
        <div className="field">
          <label htmlFor="access-reason">{label}</label>
          <textarea
            id="access-reason"
            value={reason}
            maxLength={MAX_REASON}
            onChange={(e) => setReason(e.target.value)}
          />
        </div>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button className="btn secondary" disabled={busy} onClick={onCancel}>Abbrechen</button>
          <button
            className={danger ? 'btn danger' : 'btn'}
            disabled={busy || !reason.trim()}
            onClick={confirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </Sheet>
  )
}

/**
 * People waiting to be let onto the board, shown above the member list because
 * it is the one thing on this page that needs acting on rather than reading.
 */
function ApprovalQueue({ users, onDecide }: { users: AdminUser[]; onDecide: () => void }) {
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const waiting = users.filter((u) => u.status === 'Pending')

  async function decide(u: AdminUser, decision: AccessDecision) {
    setBusyId(u.id)
    setError(null)
    try {
      await decideAccess(u, decision)
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
                <button className="btn" disabled={busyId === u.id} onClick={() => decide(u, 'approve')}>
                  Freigeben
                </button>
                <button className="btn secondary" disabled={busyId === u.id} onClick={() => decide(u, 'reject')}>
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
  // Which member a reason is being asked about, and for which direction.
  const [pending, setPending] = useState<{ user: AdminUser; decision: 'suspend' | 'approve' } | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  async function toggle(u: AdminUser, role: 'moderator' | 'admin', grant: boolean) {
    setActionError(null)
    try {
      await api.post(`/members/${u.id}/roles`, { role, grant })
      reload()
    } catch (e) {
      setActionError((e as Error).message)
    }
  }

  /** Re-admitting someone who was only ever turned away needs no reason. */
  async function readmit(u: AdminUser) {
    setActionError(null)
    try {
      await decideAccess(u, 'approve')
      reload()
    } catch (e) {
      setActionError((e as Error).message)
    }
  }

  if (loading) return <Loading />
  if (error) return <ErrorNotice message={error} />

  return (
    <>
      <ApprovalQueue users={data ?? []} onDecide={reload} />
      <div className="panel">
      {actionError && <ErrorNotice message={actionError} />}
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
                  <span className={`badge ${u.status === 'Approved' ? 'published' : u.status === 'Pending' ? 'pending' : 'rejected'}`}>
                    {STATUS_LABELS[u.status]}
                  </span>
                  {u.status === 'Suspended' && u.statusReason && (
                    <div className="u-label" style={{ marginTop: '0.35rem', whiteSpace: 'normal' }}>
                      {u.statusReason}
                    </div>
                  )}
                </td>
                <td>{u.roles.map((r) => <span key={r} className="badge role" style={{ marginRight: 4 }}>{r}</span>)}</td>
                <td>
                  {u.status === 'Rejected' ? (
                    <button className="btn small secondary" onClick={() => readmit(u)}>Doch freigeben</button>
                  ) : u.status === 'Suspended' ? (
                    <button
                      className="btn small"
                      onClick={() => setPending({ user: u, decision: 'approve' })}
                    >
                      Zugang wiederherstellen
                    </button>
                  ) : (
                    <>
                      <button className="btn small secondary" onClick={() => toggle(u, 'moderator', !isMod)}>
                        {isMod ? 'Moderator entziehen' : 'Zum Moderator machen'}
                      </button>{' '}
                      <button className="btn small secondary" onClick={() => toggle(u, 'admin', !isAdmin)}>
                        {isAdmin ? 'Admin entziehen' : 'Zum Admin machen'}
                      </button>
                      {u.status === 'Approved' && (
                        <>
                          {' '}
                          <button
                            className="btn small danger"
                            onClick={() => setPending({ user: u, decision: 'suspend' })}
                          >
                            Zugang entziehen
                          </button>
                        </>
                      )}
                    </>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      </div>

      {pending && (
        <ReasonDialog
          title={pending.decision === 'suspend' ? 'Zugang entziehen' : 'Zugang wiederherstellen'}
          intro={
            pending.decision === 'suspend'
              ? `${pending.user.displayName ?? pending.user.email ?? 'Dieses Mitglied'} kann das schwarze Brett danach nicht mehr nutzen. Die Begründung wird dem Mitglied angezeigt und protokolliert.`
              : `${pending.user.displayName ?? pending.user.email ?? 'Dieses Mitglied'} kann das schwarze Brett danach wieder nutzen. Die Begründung wird protokolliert.`
          }
          label="Begründung"
          confirmLabel={pending.decision === 'suspend' ? 'Zugang entziehen' : 'Wiederherstellen'}
          danger={pending.decision === 'suspend'}
          onCancel={() => setPending(null)}
          onConfirm={async (reason) => {
            await decideAccess(pending.user, pending.decision, reason)
            setPending(null)
            reload()
          }}
        />
      )}
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
