import { useState } from 'react'
import { ApiError, api } from '../../api/client'
import { EmptyState, ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import EventDetail from './EventDetail'
import type { EventPost } from '../../types'

type ExportDayOption = '7' | '14' | '21' | 'custom'
const MAX_EXPORT_DAYS = 90

function buildExportDays(option: ExportDayOption, customDays: string) {
  if (option !== 'custom') return Number(option)
  if (!/^\d+$/.test(customDays)) return null
  const days = Number(customDays)
  return Number.isInteger(days) && days >= 0 && days <= MAX_EXPORT_DAYS ? days : null
}

function ExportPanel() {
  const [option, setOption] = useState<ExportDayOption>('7')
  const [customDays, setCustomDays] = useState('7')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const days = buildExportDays(option, customDays)

  async function exportPdf() {
    if (days === null) return

    setBusy(true)
    setError(null)
    try {
      const res = await fetch(`${api.base}/moderation/events/export?days=${days}`)
      if (res.status === 401) throw new ApiError('Bitte anmelden.', 401)
      if (res.status === 403) throw new ApiError('Keine Berechtigung.', 403)
      if (!res.ok) {
        const text = await res.text().catch(() => '')
        throw new ApiError(text || `Fehler ${res.status}`, res.status)
      }

      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `events-${days}d.pdf`
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="panel" style={{ marginBottom: '1.5rem' }}>
      <h2 style={{ marginTop: 0 }}>Event-PDF exportieren</h2>
      <p style={{ color: 'var(--ink-soft)' }}>
        Exportiert veröffentlichte Events ab heute für 7, 14, 21 oder individuell 0 bis 90 Tage.
      </p>
      {error && <ErrorNotice message={error} />}
      <div className="row" style={{ alignItems: 'flex-end', flexWrap: 'wrap' }}>
        <div className="field" style={{ minWidth: 180, marginBottom: 0 }}>
          <label htmlFor="event-export-range">Zeitraum</label>
          <select
            id="event-export-range"
            value={option}
            onChange={(e) => {
              const next = e.target.value as ExportDayOption
              setOption(next)
              setError(null)
            }}
          >
            <option value="7">7 Tage</option>
            <option value="14">14 Tage</option>
            <option value="21">21 Tage</option>
            <option value="custom">Benutzerdefiniert</option>
          </select>
        </div>
        <div className="field" style={{ minWidth: 180, marginBottom: 0 }}>
          <label htmlFor="event-export-custom-days">Tage (0–90)</label>
          <input
            id="event-export-custom-days"
            type="number"
            min={0}
            max={MAX_EXPORT_DAYS}
            step={1}
            disabled={option !== 'custom'}
            value={customDays}
            onChange={(e) => {
              setCustomDays(e.target.value)
              setError(null)
            }}
          />
        </div>
        <button className="btn" disabled={busy || days === null} onClick={exportPdf}>
          {busy ? 'Erstellt…' : 'Export erstellen'}
        </button>
      </div>
      {option === 'custom' && days === null && (
        <small style={{ color: 'var(--danger)' }}>Bitte eine ganze Zahl zwischen 0 und 90 eingeben.</small>
      )}
    </div>
  )
}

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
            className={`card card-clickable event-card${e.imageUrl ? ' event-card-image' : ''}`}
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
            {e.imageUrl ? (
              <>
                <img
                  className="event-card-media"
                  src={e.imageUrl}
                  alt=""
                  loading="lazy"
                  decoding="async"
                  width={800}
                  height={1000}
                />
                <div className="event-card-footer">
                  <div className="event-card-badges">
                    <span className="badge pending">Prüfung</span>
                  </div>
                  <h3>{e.title}</h3>
                  <div className="meta"><span>{e.location}</span><span>{e.authorName}</span></div>
                  {/* Stop propagation so acting on a card does not also open its detail view. */}
                  <div className="row" style={{ marginTop: '0.9rem' }} onClick={(ev) => ev.stopPropagation()}>
                    <button className="btn small" disabled={busyId === e.id} onClick={() => approve(e)}>Freigeben</button>
                    <button className="btn small danger" disabled={busyId === e.id} onClick={() => setRejecting(e)}>Ablehnen</button>
                  </div>
                </div>
              </>
            ) : (
              <>
                <span className="badge pending">Prüfung</span>
                <h3>{e.title}</h3>
                <div className="desc desc-clamp">{e.description}</div>
                <div className="meta"><span>{e.location}</span><span>{e.authorName}</span></div>
                {/* Stop propagation so acting on a card does not also open its detail view. */}
                <div className="row" style={{ marginTop: '0.9rem' }} onClick={(ev) => ev.stopPropagation()}>
                  <button className="btn small" disabled={busyId === e.id} onClick={() => approve(e)}>Freigeben</button>
                  <button className="btn small danger" disabled={busyId === e.id} onClick={() => setRejecting(e)}>Ablehnen</button>
                </div>
              </>
            )}
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
      <ExportPanel />
      <ModerationQueue />
    </RequireRole>
  )
}
