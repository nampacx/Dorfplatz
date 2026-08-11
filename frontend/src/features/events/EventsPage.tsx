import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, useAsync } from '../../components/Common'
import type { EventPost } from '../../types'

function fmtDate(iso: string | null) {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'short', year: 'numeric' })
}

function SubmitEventModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [form, setForm] = useState({ title: '', description: '', eventDate: '', location: '' })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.post('/events', {
        title: form.title,
        description: form.description,
        eventDate: form.eventDate ? new Date(form.eventDate).toISOString() : null,
        location: form.location || null,
      })
      onCreated()
      onClose()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <h2 style={{ marginTop: 0 }}>Event einreichen</h2>
        <p style={{ color: 'var(--ink-soft)', marginTop: 0 }}>
          Deine Einreichung wird von einem Moderator geprüft, bevor sie öffentlich erscheint.
        </p>
        {error && <ErrorNotice message={error} />}
        <div className="field">
          <label>Titel</label>
          <input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
        </div>
        <div className="field">
          <label>Beschreibung</label>
          <textarea required value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </div>
        <div className="row">
          <div className="field">
            <label>Datum</label>
            <input type="date" value={form.eventDate} onChange={(e) => setForm({ ...form, eventDate: e.target.value })} />
          </div>
          <div className="field">
            <label>Ort</label>
            <input value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} />
          </div>
        </div>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button type="button" className="btn secondary" onClick={onClose}>Abbrechen</button>
          <button className="btn" disabled={busy}>{busy ? 'Sende…' : 'Einreichen'}</button>
        </div>
      </form>
    </div>
  )
}

export default function EventsPage() {
  const { me } = useAuth()
  const [showForm, setShowForm] = useState(false)
  const { data, loading, error, reload } = useAsync(() => api.get<EventPost[]>('/events'), [])

  return (
    <>
      <div className="page-head" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1>Events & Neuigkeiten</h1>
          <p>Vereinsfeste, Partys und Ankündigungen aus der Nachbarschaft.</p>
        </div>
        {me && <button className="btn" onClick={() => setShowForm(true)}>+ Event einreichen</button>}
      </div>

      {error && <ErrorNotice message={error} />}
      {loading ? (
        <Loading />
      ) : !data?.length ? (
        <EmptyState>Noch keine veröffentlichten Events. Sei die/der Erste!</EmptyState>
      ) : (
        <div className="grid">
          {data.map((e) => (
            <article key={e.id} className="card">
              <h3>{e.title}</h3>
              <div className="desc">{e.description}</div>
              <div className="meta">
                <span>{[fmtDate(e.eventDate), e.location].filter(Boolean).join(' · ')}</span>
                <span>{e.authorName}</span>
              </div>
            </article>
          ))}
        </div>
      )}

      {showForm && <SubmitEventModal onClose={() => setShowForm(false)} onCreated={reload} />}
    </>
  )
}
