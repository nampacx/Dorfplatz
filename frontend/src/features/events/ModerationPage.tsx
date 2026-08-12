import { useState } from 'react'
import { api } from '../../api/client'
import { EmptyState, ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import EventDetail from './EventDetail'
import type { EventPost } from '../../types'

function ModerationQueue() {
  const { data, loading, error, reload } = useAsync(() => api.get<EventPost[]>('/moderation/events'), [])
  const [busyId, setBusyId] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState<EventPost | null>(null)
  const [selected, setSelected] = useState<EventPost | null>(null)
  const [reason, setReason] = useState('')

  async function approve(e: EventPost) {
    setBusyId(e.id)
    try {
      await api.post(`/moderation/events/${e.id}/approve`)
      reload()
    } finally {
      setBusyId(null)
    }
  }

  async function confirmReject() {
    if (!rejecting || !reason.trim()) return
    setBusyId(rejecting.id)
    try {
      await api.post(`/moderation/events/${rejecting.id}/reject`, { reason })
      setRejecting(null)
      setReason('')
      reload()
    } finally {
      setBusyId(null)
    }
  }

  if (loading) return <Loading />
  if (error) return <ErrorNotice message={error} />
  if (!data?.length) return <EmptyState>Keine offenen Einreichungen. 🎉</EmptyState>

  return (
    <>
      <div className="grid">
        {data.map((e) => (
          <article
            key={e.id}
            className="card card-clickable"
            role="button"
            tabIndex={0}
            aria-label={`${e.title} – vollständige Einreichung ansehen`}
            onClick={() => setSelected(e)}
            onKeyDown={(ev) => {
              if (ev.key === 'Enter' || ev.key === ' ') {
                ev.preventDefault()
                setSelected(e)
              }
            }}
          >
            <span className="badge pending">Prüfung</span>
            <h3>{e.title}</h3>
            <div className="desc desc-clamp">{e.description}</div>
            <div className="meta"><span>{e.location}</span><span>{e.authorName}</span></div>
            {/* Stop propagation so acting on a card does not also open its detail view. */}
            <div className="row" style={{ marginTop: '0.9rem' }} onClick={(ev) => ev.stopPropagation()}>
              <button className="btn small" disabled={busyId === e.id} onClick={() => approve(e)}>Freigeben</button>
              <button className="btn small danger" disabled={busyId === e.id} onClick={() => setRejecting(e)}>Ablehnen</button>
            </div>
          </article>
        ))}
      </div>

      {selected && (
        <EventDetail
          item={selected}
          onClose={() => setSelected(null)}
          actions={
            <>
              <button
                className="btn"
                disabled={busyId === selected.id}
                onClick={async () => {
                  await approve(selected)
                  setSelected(null)
                }}
              >
                Freigeben
              </button>
              <button
                className="btn danger"
                onClick={() => {
                  setRejecting(selected)
                  setSelected(null)
                }}
              >
                Ablehnen
              </button>
            </>
          }
        />
      )}

      {rejecting && (
        <Sheet title="Einreichung ablehnen" onClose={() => setRejecting(null)}>
          <div>
            <h2 style={{ marginTop: 0 }}>Einreichung ablehnen</h2>
            <p style={{ color: 'var(--ink-soft)' }}>Der Grund wird der einreichenden Person angezeigt.</p>
            <div className="field">
              <label>Ablehnungsgrund</label>
              <textarea value={reason} onChange={(e) => setReason(e.target.value)} />
            </div>
            <div className="row" style={{ justifyContent: 'flex-end' }}>
              <button className="btn secondary" onClick={() => setRejecting(null)}>Abbrechen</button>
              <button className="btn danger" disabled={!reason.trim()} onClick={confirmReject}>Ablehnen</button>
            </div>
          </div>
        </Sheet>
      )}
    </>
  )
}

export default function ModerationPage() {
  return (
    <RequireRole role="moderator">
      <div className="page-head">
        <h1>Moderation</h1>
        <p>Prüfe eingereichte Events und gib sie frei oder lehne sie ab.</p>
      </div>
      <ModerationQueue />
    </RequireRole>
  )
}
