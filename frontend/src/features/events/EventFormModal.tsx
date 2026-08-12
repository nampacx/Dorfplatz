import { useState } from 'react'
import { api } from '../../api/client'
import { ErrorNotice } from '../../components/Common'
import Sheet from '../../components/Sheet'
import { EVENT_CATEGORIES, type EventCategory, type EventPost } from '../../types'

/** Date input wants yyyy-MM-dd; the API returns an ISO timestamp. */
function toDateInput(iso: string | null) {
  return iso ? new Date(iso).toISOString().slice(0, 10) : ''
}

/**
 * Create when `existing` is omitted, otherwise edit and resubmit that event.
 * Editing always sends the event back to moderation, so the form says so.
 */
export default function EventFormModal({
  existing,
  onClose,
  onSaved,
}: {
  existing?: EventPost
  onClose: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState({
    title: existing?.title ?? '',
    description: existing?.description ?? '',
    eventDate: toDateInput(existing?.eventDate ?? null),
    location: existing?.location ?? '',
    category: (existing?.category ?? 'Sonstiges') as EventCategory,
  })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const payload = {
        title: form.title,
        description: form.description,
        eventDate: form.eventDate ? new Date(form.eventDate).toISOString() : null,
        location: form.location || null,
        category: form.category,
      }
      if (existing) await api.patch(`/events/${existing.id}`, payload)
      else await api.post('/events', payload)
      onSaved()
      onClose()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Sheet title={existing ? 'Event bearbeiten' : 'Event einreichen'} onClose={onClose}>
      <form onSubmit={submit}>
        <h2 style={{ marginTop: 0 }}>
          {existing ? 'Event bearbeiten' : 'Event einreichen'}
        </h2>
        <p style={{ color: 'var(--ink-soft)', marginTop: 0 }}>
          {existing
            ? 'Nach dem Speichern wird dein Event erneut von einem Moderator geprüft.'
            : 'Deine Einreichung wird von einem Moderator geprüft, bevor sie öffentlich erscheint.'}
        </p>

        {existing?.status === 'Rejected' && existing.rejectionReason && (
          <div className="notice error">Zuvor abgelehnt: {existing.rejectionReason}</div>
        )}
        {error && <ErrorNotice message={error} />}

        <div className="field">
          <label>Titel</label>
          <input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
        </div>
        <div className="field">
          <label>Kategorie</label>
          <select
            value={form.category}
            onChange={(e) => setForm({ ...form, category: e.target.value as EventCategory })}
          >
            {EVENT_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
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
          <button className="btn" disabled={busy}>
            {busy ? 'Sendet…' : existing ? 'Speichern und erneut einreichen' : 'Einreichen'}
          </button>
        </div>
      </form>
    </Sheet>
  )
}
