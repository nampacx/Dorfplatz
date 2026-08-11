import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { LoginGate, Loading, useAsync } from '../../components/Common'
import EventDetail, { STATUS_LABELS } from '../events/EventDetail'
import type { EventPost } from '../../types'

function MyEvents() {
  const { data, loading } = useAsync(() => api.get<EventPost[]>('/events/mine'), [])
  const [selected, setSelected] = useState<EventPost | null>(null)

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
      {selected && <EventDetail item={selected} onClose={() => setSelected(null)} />}
    </>
  )
}

export default function AccountPage() {
  const { me, loading } = useAuth()
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
    a.download = 'my-bulletinboard-data.json'
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

      <div className="panel" style={{ marginBottom: '1.5rem' }}>
        <p><strong>Name:</strong> {me.displayName ?? '—'}</p>
        <p><strong>E-Mail:</strong> {me.email ?? '—'}</p>
        <p><strong>Rollen:</strong> {me.roles.map((r) => <span key={r} className="badge role" style={{ marginRight: 4 }}>{r}</span>)}</p>
      </div>

      <h2 style={{ fontFamily: 'var(--serif)' }}>Meine Events</h2>
      <MyEvents />

      <div className="panel" style={{ marginTop: '2rem' }}>
        <h2 style={{ marginTop: 0, fontFamily: 'var(--serif)' }}>Datenschutz (DSGVO)</h2>
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
        <div className="modal-backdrop" onClick={() => setConfirming(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
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
        </div>
      )}
    </>
  )
}
