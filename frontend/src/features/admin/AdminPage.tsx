import { useEffect, useState } from 'react'
import { api } from '../../api/client'
import { EmptyState, ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import type { AccessDecision, AccessStatus, AdminUser, AdminUserPage } from '../../types'

const STATUS_LABELS: Record<AdminUser['status'], string> = {
  Pending: 'Wartet',
  Approved: 'Freigegeben',
  Rejected: 'Abgelehnt',
  Suspended: 'Gesperrt',
}

const MAX_REASON = 500
const MEMBER_PAGE_SIZE = 20
const PENDING_PAGE_SIZE = 200

const MEMBER_FILTERS: Array<{
  key: 'all' | 'Approved' | 'Rejected' | 'Suspended'
  label: string
  statuses: AccessStatus[]
}> = [
  { key: 'all', label: 'Alle', statuses: ['Approved', 'Rejected', 'Suspended'] },
  { key: 'Approved', label: 'Freigegeben', statuses: ['Approved'] },
  { key: 'Rejected', label: 'Abgelehnt', statuses: ['Rejected'] },
  { key: 'Suspended', label: 'Gesperrt', statuses: ['Suspended'] },
]

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/** POST an access decision. Reason is only sent when one was asked for. */
function decideAccess(user: AdminUser, decision: AccessDecision, reason?: string) {
  return api.post(`/members/${user.id}/approval`, { decision, reason })
}

function buildMembersPath({
  search,
  statuses,
  page,
  pageSize,
}: {
  search: string
  statuses?: AccessStatus[]
  page?: number
  pageSize?: number
}) {
  const params = new URLSearchParams()
  const trimmedSearch = search.trim()
  if (trimmedSearch) params.set('search', trimmedSearch)
  if (statuses && statuses.length > 0) params.set('statuses', statuses.join(','))
  if (page) params.set('page', String(page))
  if (pageSize) params.set('pageSize', String(pageSize))
  const query = params.toString()
  return query ? `/members?${query}` : '/members'
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
function ApprovalQueue({ search, reloadToken, onDecide }: { search: string; reloadToken: number; onDecide: () => void }) {
  const { data, loading, error } = useAsync(
    () => api.get<AdminUserPage>(buildMembersPath({ search, statuses: ['Pending'], page: 1, pageSize: PENDING_PAGE_SIZE })),
    [search, reloadToken],
  )
  const [busyId, setBusyId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const waiting = data?.items ?? []

  async function decide(u: AdminUser, decision: AccessDecision) {
    setBusyId(u.id)
    setActionError(null)
    try {
      await decideAccess(u, decision)
      onDecide()
    } catch (e) {
      setActionError((e as Error).message)
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="panel" style={{ marginBottom: '1.5rem' }}>
      <h2 style={{ marginTop: 0 }}>
        Freischalten{(data?.totalCount ?? 0) > 0 && <span className="badge pending" style={{ marginLeft: '0.5rem' }}>{data?.totalCount}</span>}
      </h2>
      {error && <ErrorNotice message={error} />}
      {actionError && <ErrorNotice message={actionError} />}
      {loading ? (
        <Loading label="Freischaltungen laden…" />
      ) : waiting.length === 0 ? (
        <p style={{ color: 'var(--ink-soft)', margin: 0 }}>
          {search.trim() ? 'Niemand wartet mit dieser Suche auf eine Freigabe.' : 'Niemand wartet auf eine Freigabe.'}
        </p>
      ) : (
        <div className="approval-list">
          {waiting.map((u) => (
            <div key={u.id} className="approval-item">
              <div className="admin-member-main">
                <div className="admin-member-name">{u.displayName ?? u.email ?? '—'}</div>
                <div className="u-label admin-member-email">{u.email} · seit {fmtDate(u.createdAt)}</div>
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
      {(data?.totalCount ?? 0) > waiting.length && (
        <p className="admin-overflow-note">
          Es werden die ersten {waiting.length} wartenden Konten gezeigt. Verfeinere die Suche, um ältere Einträge zu finden.
        </p>
      )}
    </div>
  )
}

function UserTable({ search, reloadToken }: { search: string; reloadToken: number }) {
  const [page, setPage] = useState(1)
  const [filter, setFilter] = useState<(typeof MEMBER_FILTERS)[number]['key']>('all')
  // Which member a reason is being asked about, and for which direction.
  const [pending, setPending] = useState<{ user: AdminUser; decision: 'suspend' | 'approve' } | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const selectedFilter = MEMBER_FILTERS.find((option) => option.key === filter) ?? MEMBER_FILTERS[0]
  const { data, loading, error, reload } = useAsync(
    () => api.get<AdminUserPage>(buildMembersPath({
      search,
      statuses: selectedFilter.statuses,
      page,
      pageSize: MEMBER_PAGE_SIZE,
    })),
    [search, filter, page, reloadToken],
  )

  useEffect(() => {
    setPage(1)
  }, [search, filter])

  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / MEMBER_PAGE_SIZE))

  useEffect(() => {
    if (data && page > totalPages) setPage(totalPages)
  }, [data, page, totalPages])

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
      <div className="panel">
        <div className="admin-panel-head">
          <div className="admin-panel-copy">
            <h2 style={{ marginTop: 0 }}>Bestehende Mitglieder</h2>
            <p className="u-label admin-panel-meta">
              {data?.totalCount === 1 ? '1 Eintrag' : `${data?.totalCount ?? 0} Einträge`}
              {data && data.totalCount > 0 && <> · Seite {data.page} von {totalPages}</>}
            </p>
          </div>
          {data && data.totalCount > MEMBER_PAGE_SIZE && (
            <div className="admin-pager">
              <button className="btn small secondary" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
                Zurück
              </button>
              <p className="u-label admin-pager-status">Seite {page} / {totalPages}</p>
              <button
                className="btn small secondary"
                disabled={page >= totalPages}
                onClick={() => setPage((current) => current + 1)}
              >
                Weiter
              </button>
            </div>
          )}
        </div>

        <div className="toolbar" role="tablist" aria-label="Mitglieder nach Status filtern">
          {MEMBER_FILTERS.map((option) => (
            <button
              key={option.key}
              type="button"
              className={`chip${filter === option.key ? ' active' : ''}`}
              onClick={() => setFilter(option.key)}
            >
              {option.label}
            </button>
          ))}
        </div>

        {actionError && <ErrorNotice message={actionError} />}
        {data && data.items.length === 0 ? (
          <EmptyState>Keine bestehenden Mitglieder für diese Auswahl gefunden.</EmptyState>
        ) : (
          <table>
            <thead>
              <tr><th>Mitglied</th><th>Zugang</th><th>Rollen</th><th>Aktionen</th></tr>
            </thead>
            <tbody>
              {data?.items.map((u) => {
                const isMod = u.roles.includes('moderator')
                const isAdmin = u.roles.includes('admin')
                return (
                  <tr key={u.id}>
                    <td>
                      <div className="admin-member-main">
                        <div className="admin-member-name">{u.displayName ?? '—'}</div>
                        <div className="admin-member-email">{u.email}</div>
                      </div>
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
                    <td>
                      <div className="admin-role-list">
                        {u.roles.map((r) => <span key={r} className="badge role">{r}</span>)}
                      </div>
                    </td>
                    <td>
                      {u.status === 'Rejected' ? (
                        <div className="stack-actions admin-table-actions">
                          <button className="btn small secondary" onClick={() => readmit(u)}>Doch freigeben</button>
                        </div>
                      ) : u.status === 'Suspended' ? (
                        <div className="stack-actions admin-table-actions">
                          <button
                            className="btn small"
                            onClick={() => setPending({ user: u, decision: 'approve' })}
                          >
                            Freischalten
                          </button>
                        </div>
                      ) : (
                        <div className="stack-actions admin-table-actions">
                          <button className="btn small secondary" onClick={() => toggle(u, 'moderator', !isMod)}>
                            {isMod ? 'Moderator entziehen' : 'Zum Moderator machen'}
                          </button>
                          <button className="btn small secondary" onClick={() => toggle(u, 'admin', !isAdmin)}>
                            {isAdmin ? 'Admin entziehen' : 'Zum Admin machen'}
                          </button>
                          <button
                            className="btn small danger"
                            onClick={() => setPending({ user: u, decision: 'suspend' })}
                          >
                            Zugang entziehen
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </div>

      {pending && (
        <ReasonDialog
          title={pending.decision === 'suspend' ? 'Zugang entziehen' : 'Zugang wiederherstellen'}
          intro={
            pending.decision === 'suspend'
              ? `${pending.user.displayName ?? pending.user.email ?? 'Dieses Mitglied'} kann den Dorfplatz danach nicht mehr nutzen. Die Begründung wird dem Mitglied angezeigt und protokolliert.`
              : `${pending.user.displayName ?? pending.user.email ?? 'Dieses Mitglied'} kann den Dorfplatz danach wieder nutzen. Die Begründung wird protokolliert.`
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
  const [search, setSearch] = useState('')
  const [reloadToken, setReloadToken] = useState(0)

  return (
    <RequireRole role="admin">
      <div className="page-head">
        <h1>Administration</h1>
        <p>Mitglieder verwalten und Moderatoren ernennen.</p>
      </div>
      <div className="panel admin-search-panel">
        <div className="field admin-search-field">
          <label htmlFor="admin-member-search">Mitglieder suchen</label>
          <input
            id="admin-member-search"
            type="search"
            value={search}
            placeholder="Name oder E-Mail"
            onChange={(e) => setSearch(e.target.value)}
          />
          <small>Die Suche filtert Freischaltungen und bestehende Mitglieder gleichzeitig.</small>
        </div>
      </div>
      <ApprovalQueue search={search} reloadToken={reloadToken} onDecide={() => setReloadToken((value) => value + 1)} />
      <UserTable search={search} reloadToken={reloadToken} />
    </RequireRole>
  )
}
