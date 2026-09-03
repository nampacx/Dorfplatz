import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { ErrorNotice, LoginGate, Loading, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import EventDetail, { STATUS_LABELS } from '../events/EventDetail'
import EventFormModal from '../events/EventFormModal'
import type { EventPost } from '../../types'

/**
 * The destinations the bottom tab bar cannot hold. Moderation and Admin are
 * rare and role-gated, and signing out is deliberate — all three belong with
 * the account controls rather than taking a permanent slot in navigation.
 */
function ManagementLinks() {
  const { isModerator, isAdmin } = useAuth()
  return (
    <div className="panel" style={{ marginTop: '2rem' }}>
      <h2 style={{ marginTop: 0 }}>Verwaltung</h2>
      <div className="stack-actions">
        {isModerator && <Link className="btn secondary" to="/moderation">Events moderieren</Link>}
        {isAdmin && <Link className="btn secondary" to="/admin">Mitglieder verwalten</Link>}
        <a className="btn secondary" href="/.auth/logout?post_logout_redirect_uri=/">Abmelden</a>
      </div>
    </div>
  )
}

function MyEvents() {
  const { data, loading, reload } = useAsync(() => api.get<EventPost[]>('/events/mine'), [])
  const [selected, setSelected] = useState<EventPost | null>(null)
  const [editing, setEditing] = useState<EventPost | null>(null)

  if (loading) return <Loading />
  if (!data?.length) return <p style={{ color: 'var(--ink-soft)' }}>Du hast noch keine Events eingereicht.</p>
  return (
    <>
      <div className="grid">
        {data.map((e) => (
          <article
            key={e.id}
            className="card card-clickable"
            role="button"
            tabIndex={0}
            aria-label={`${e.title} – Details ansehen`}
            onClick={() => setSelected(e)}
            onKeyDown={(ev) => {
              if (ev.key === 'Enter' || ev.key === ' ') {
                ev.preventDefault()
                setSelected(e)
              }
            }}
          >
            <span className={`badge ${e.status.toLowerCase()}`}>{STATUS_LABELS[e.status]}</span>
            <h3>{e.title}</h3>
            {e.status === 'Rejected' && e.rejectionReason && (
              <div className="notice error" style={{ marginTop: '0.5rem' }}>Grund: {e.rejectionReason}</div>
            )}
          </article>
        ))}
      </div>
      {selected && (
        <EventDetail
          item={selected}
          onClose={() => setSelected(null)}
          onEdit={() => {
            setEditing(selected)
            setSelected(null)
          }}
          onDeleted={reload}
        />
      )}
      {editing && (
        <EventFormModal
          existing={editing}
          onClose={() => setEditing(null)}
          onSaved={reload}
        />
      )}
    </>
  )
}

const MAX_NAME = 60

/**
 * Profile card. The display name is the one identity field members control — it is
 * what appears next to their posts. The email comes from the identity provider and
 * is how the account is matched, so it is shown but not editable.
 */
function ProfilePanel() {
  const { me, refresh } = useAuth()
  const [name, setName] = useState(me?.displayName ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  if (!me) return null
  const trimmed = name.trim()
  const dirty = trimmed !== (me.displayName ?? '')

  async function save(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    setSaved(false)
    try {
      await api.patch('/account/me', { displayName: trimmed })
      await refresh()
      setSaved(true)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="panel" style={{ marginBottom: '1.5rem' }} onSubmit={save}>
      {error && <ErrorNotice message={error} />}
      {saved && !dirty && <div className="notice info">Anzeigename gespeichert.</div>}

      <div className="field">
        <label htmlFor="displayName">Anzeigename</label>
        <input
          id="displayName"
          value={name}
          maxLength={MAX_NAME}
          required
          onChange={(e) => {
            setName(e.target.value)
            setSaved(false)
          }}
        />
        <small>So erscheinst du neben deinen Beiträgen. {MAX_NAME - trimmed.length} Zeichen übrig.</small>
      </div>

      <div className="field">
        <label htmlFor="email">E-Mail</label>
        <input id="email" value={me.email ?? '—'} readOnly disabled />
        <small>Kommt von deinem Microsoft- oder Google-Konto und kann hier nicht geändert werden.</small>
      </div>

      <div className="field">
        <label>Rollen</label>
        <div>
          {me.roles.map((r) => <span key={r} className="badge role" style={{ marginRight: 4 }}>{r}</span>)}
        </div>
      </div>

      <button className="btn" disabled={busy || !dirty || !trimmed}>
        {busy ? 'Speichert…' : 'Namen speichern'}
      </button>
    </form>
  )
}

export default function AccountPage() {
  const { me, loading, isApproved } = useAuth()
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)

  if (loading) return <Loading />
  if (!me) return <LoginGate />

  async function exportData() {
    const res = await fetch(`${api.base}/account/export`)
    const blob = await res.blob()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'my-dorfplatz-data.json'
    a.click()
    URL.revokeObjectURL(url)
  }

  async function deleteAccount() {
    setBusy(true)
    try {
      await api.post('/account/delete')
      window.location.href = '/.auth/logout?post_logout_redirect_uri=/'
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <div className="page-head"><h1>Mein Konto</h1></div>

      <ProfilePanel />

      {/* /events/mine needs an approved account, so a waiting member would
          only get an error here. They have no events yet either way. */}
      {isApproved && (
        <>
          <h2>Meine Events</h2>
          <MyEvents />
        </>
      )}

      <ManagementLinks />

      <div className="panel" style={{ marginTop: '2rem' }}>
        <h2 style={{ marginTop: 0 }}>Datenschutz (DSGVO)</h2>
        <p style={{ color: 'var(--ink-soft)' }}>
          Du kannst jederzeit alle über dich gespeicherten Daten exportieren oder dein Konto löschen.
          Details in der <Link to="/privacy">Datenschutzerklärung</Link>.
        </p>
        <div className="row" style={{ maxWidth: 460 }}>
          <button className="btn secondary" onClick={exportData}>Meine Daten exportieren</button>
          <button className="btn danger" onClick={() => setConfirming(true)}>Konto löschen</button>
        </div>
      </div>

      {confirming && (
        <Sheet title="Konto wirklich löschen?" onClose={() => setConfirming(false)}>
          <div>
            <h2 style={{ marginTop: 0 }}>Konto wirklich löschen?</h2>
            <p>
              Deine persönlichen Daten werden anonymisiert, deine Anzeigen entfernt und deine
              Nachrichten unkenntlich gemacht. Dieser Schritt kann nicht rückgängig gemacht werden.
            </p>
            <div className="row" style={{ justifyContent: 'flex-end' }}>
              <button className="btn secondary" onClick={() => setConfirming(false)}>Abbrechen</button>
              <button className="btn danger" disabled={busy} onClick={deleteAccount}>Endgültig löschen</button>
            </div>
          </div>
        </Sheet>
      )}
    </>
  )
}
