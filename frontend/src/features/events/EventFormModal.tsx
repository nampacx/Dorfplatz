import { useState } from 'react'
import { api } from '../../api/client'
import { ErrorNotice } from '../../components/Common'
import Sheet from '../../components/Sheet'
import { EVENT_CATEGORIES, type EventCategory, type EventPost } from '../../types'
import { combineDateAndTime, toDateInputValue, toTimeInputValue } from './schedule'

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
    eventDate: toDateInputValue(existing?.eventDate ?? null),
    startTime: toTimeInputValue(existing?.eventDate ?? null, 'start'),
    endDate: toDateInputValue(existing?.endDate ?? null),
    endTime: toTimeInputValue(existing?.endDate ?? null, 'end'),
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
        eventDate: combineDateAndTime(form.eventDate, form.startTime),
        endDate: combineDateAndTime(form.endDate, form.endTime, { blankTimeMeansEndOfDay: true }),
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
            <label>Startdatum</label>
            <input
              type="date"
              value={form.eventDate}
              onChange={(e) => setForm((current) => {
                const nextDate = e.target.value
                return {
                  ...current,
                  eventDate: nextDate,
                  startTime: nextDate ? current.startTime : '',
                  endDate: nextDate ? current.endDate : '',
                  endTime: nextDate ? current.endTime : '',
                }
              })}
            />
          </div>
          <div className="field">
            <label>Startzeit</label>
            <input
              type="time"
              value={form.startTime}
              disabled={!form.eventDate}
              onChange={(e) => setForm({ ...form, startTime: e.target.value })}
            />
          </div>
        </div>
        <div className="row">
          <div className="field">
            <label>Enddatum</label>
            <input
              type="date"
              value={form.endDate}
              disabled={!form.eventDate}
              min={form.eventDate || undefined}
              onChange={(e) => setForm((current) => ({
                ...current,
                endDate: e.target.value,
                endTime: e.target.value ? current.endTime : '',
              }))}
            />
          </div>
          <div className="field">
            <label>Endzeit</label>
            <input
              type="time"
              value={form.endTime}
              disabled={!form.endDate}
              onChange={(e) => setForm((current) => ({
                ...current,
                endDate: current.endDate || current.eventDate,
                endTime: e.target.value,
              }))}
            />
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
