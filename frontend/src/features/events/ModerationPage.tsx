import { useState } from 'react'
import { api } from '../../api/client'
import { EmptyState, ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import type { EventPost } from '../../types'

function ModerationQueue() {
  const { data, loading, error, reload } = useAsync(() => api.get<EventPost[]>('/moderation/events'), [])
  const [busyId, setBusyId] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState<EventPost | null>(null)
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
          <article key={e.id} className="card">
            <span className="badge pending">Prüfung</span>
            <h3>{e.title}</h3>
            <div className="desc">{e.description}</div>
            <div className="meta"><span>{e.location}</span><span>{e.authorName}</span></div>
            <div className="row" style={{ marginTop: '0.9rem' }}>
              <button className="btn small" disabled={busyId === e.id} onClick={() => approve(e)}>Freigeben</button>
              <button className="btn small danger" disabled={busyId === e.id} onClick={() => setRejecting(e)}>Ablehnen</button>
            </div>
          </article>
        ))}
      </div>

      {rejecting && (
        <div className="modal-backdrop" onClick={() => setRejecting(null)}>
          <div className="modal" onClick={(ev) => ev.stopPropagation()}>
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
        </div>
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
