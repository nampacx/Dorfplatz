import { useState, type ReactNode } from 'react'
import { api } from '../../api/client'
import DetailModal from '../../components/DetailModal'
import type { EventPost } from '../../types'
import { downloadEventIcs } from './ics'
import { STATUS_LABELS } from './labels'
import { formatSchedule } from './schedule'

export { STATUS_LABELS }

function fmtDay(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/**
 * Full event. Shared by the public board, the author's own list and the
 * moderation queue — the queue passes its approve/reject buttons as `actions`,
 * so a moderator can read the whole submission before deciding.
 */
export default function EventDetail({
  item,
  onClose,
  actions,
  onEdit,
  onDeleted,
}: {
  item: EventPost
  onClose: () => void
  actions?: ReactNode
  onEdit?: () => void
  onDeleted?: () => void
}) {
  const when = formatSchedule(item.eventDate, item.endDate, { withWeekday: true })
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const canManage = item.canEdit && (onEdit || onDeleted)

  async function remove() {
    setBusy(true)
    setError(null)
    try {
      await api.del(`/events/${item.id}`)
      onDeleted?.()
      onClose()
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <DetailModal title={item.title} onClose={onClose}>
      {item.status !== 'Published' && (
        <span className={`badge ${item.status.toLowerCase()}`}>{STATUS_LABELS[item.status]}</span>
      )}

      <h2 className="detail-title">{item.title}</h2>

      <dl className="detail-facts">
        <dt>Kategorie</dt><dd>{item.category}</dd>
        {when && (<><dt>Termin</dt><dd>{when}</dd></>)}
        {item.location && (<><dt>Ort</dt><dd>{item.location}</dd></>)}
        <dt>Eingereicht von</dt><dd>{item.authorName}</dd>
        <dt>Eingereicht am</dt><dd>{fmtDay(item.createdAt)}</dd>
      </dl>

      {item.status === 'Rejected' && item.rejectionReason && (
        <div className="notice error">
          <strong>Grund der Ablehnung:</strong> {item.rejectionReason}
          {item.canEdit && onEdit && (
            <div style={{ marginTop: '0.4rem' }}>
              Du kannst das Event überarbeiten und erneut zur Prüfung einreichen.
            </div>
          )}
        </div>
      )}

      <p className="detail-desc">{item.description}</p>

      {error && <div className="notice error">{error}</div>}

      {confirming ? (
        <div className="confirm">
          <p>Dieses Event wirklich löschen? Das lässt sich nicht rückgängig machen.</p>
          <div className="detail-actions">
            <button className="btn danger" disabled={busy} onClick={remove}>
              {busy ? 'Löscht…' : 'Endgültig löschen'}
            </button>
            <button className="btn secondary" disabled={busy} onClick={() => setConfirming(false)}>
              Abbrechen
            </button>
          </div>
        </div>
      ) : (
        <div className="detail-actions">
          {actions}
          <button className="btn secondary" onClick={() => downloadEventIcs(item)}>
            Zum Kalender hinzufügen
          </button>
          {canManage && onEdit && (
            <button className="btn" onClick={onEdit}>
              {item.status === 'Rejected' ? 'Überarbeiten und erneut einreichen' : 'Bearbeiten'}
            </button>
          )}
          {canManage && onDeleted && (
            <button className="btn danger" onClick={() => setConfirming(true)}>Löschen</button>
          )}
          <button className="btn secondary" onClick={onClose}>Schließen</button>
        </div>
      )}
    </DetailModal>
  )
}
