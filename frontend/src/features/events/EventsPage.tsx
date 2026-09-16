import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, useAsync } from '../../components/Common'
import EventCalendar from './EventCalendar'
import EventDetail from './EventDetail'
import EventFormModal from './EventFormModal'
import { EVENT_CATEGORIES, type EventCategory, type EventPost } from '../../types'
import { formatSchedule } from './schedule'

type ViewMode = 'cards' | 'calendar'

const VIEW_KEY = 'events:view'

/** Remembered per browser, so the chosen view survives a reload. */
function readStoredView(): ViewMode {
  try {
    return localStorage.getItem(VIEW_KEY) === 'calendar' ? 'calendar' : 'cards'
  } catch {
    return 'cards'
  }
}

/** Matches the API cutoff: a Termin counts as upcoming for the whole of its day. */
function isPast(startIso: string | null, endIso: string | null) {
  const iso = endIso ?? startIso
  if (!iso) return false
  const startOfToday = new Date()
  startOfToday.setHours(0, 0, 0, 0)
  return new Date(iso) < startOfToday
}

export default function EventsPage() {
  const { me, isApproved, isModerator } = useAuth()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<EventPost | null>(null)
  const [selected, setSelected] = useState<EventPost | null>(null)
  const [category, setCategory] = useState<EventCategory | 'all'>('all')
  const [view, setView] = useState<ViewMode>(readStoredView)
  const [includePast, setIncludePast] = useState(false)
  // Only moderators may request the archive; the flag must never leak into a
  // member's query, otherwise the API just ignores it and the UI lies.
  const showPast = isModerator && includePast

  function switchView(next: ViewMode) {
    setView(next)
    try {
      localStorage.setItem(VIEW_KEY, next)
    } catch {
      /* Private mode or a full quota: the view still switches for this visit. */
    }
  }

  const { data, loading, error, reload } = useAsync(() => {
    const params = new URLSearchParams()
    if (category !== 'all') params.set('category', category)
    if (showPast) params.set('includePast', 'true')
    const qs = params.toString()
    return api.get<EventPost[]>(`/events${qs ? `?${qs}` : ''}`)
  }, [category, showPast])

  return (
    <>
      <div className="page-head" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1>Events & Neuigkeiten</h1>
          <p>Vereinsfeste, Partys und Ankündigungen aus der Nachbarschaft.</p>
        </div>
        {/* The board stays readable for everyone; submitting needs a member. */}
        {me && isApproved && (
          <button className="btn" onClick={() => setShowForm(true)}>+ Event einreichen</button>
        )}
      </div>

      <div className="toolbar">
        <button className={`chip ${category === 'all' ? 'active' : ''}`} onClick={() => setCategory('all')}>
          Alle
        </button>
        {EVENT_CATEGORIES.map((c) => (
          <button key={c} className={`chip ${category === c ? 'active' : ''}`} onClick={() => setCategory(c)}>
            {c}
          </button>
        ))}
        {isModerator && (
          <label className="switch" title="Auch Events anzeigen, deren Termin bereits vorbei ist">
            <input
              type="checkbox"
              checked={includePast}
              onChange={(ev) => setIncludePast(ev.target.checked)}
            />
            <span className="switch-track" aria-hidden="true" />
            <span>Vergangene Events</span>
          </label>
        )}
        <div className="view-switch" role="group" aria-label="Ansicht wählen">
          <button
            className={`chip ${view === 'cards' ? 'active' : ''}`}
            aria-pressed={view === 'cards'}
            onClick={() => switchView('cards')}
          >
            Karten
          </button>
          <button
            className={`chip ${view === 'calendar' ? 'active' : ''}`}
            aria-pressed={view === 'calendar'}
            onClick={() => switchView('calendar')}
          >
            Kalender
          </button>
        </div>
      </div>

      {error && <ErrorNotice message={error} />}
      {loading ? (
        <Loading />
      ) : !data?.length ? (
        <EmptyState>
          {category === 'all'
            ? showPast
              ? 'Noch keine veröffentlichten Events. Sei die/der Erste!'
              : 'Keine anstehenden Events. Vergangene Termine werden nicht angezeigt.'
            : showPast
              ? `Keine Events in der Kategorie ${category}.`
              : `Keine anstehenden Events in der Kategorie ${category}.`}
        </EmptyState>
      ) : view === 'calendar' ? (
        <EventCalendar events={data} onSelect={setSelected} />
      ) : (
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
              <span className="badge category">{e.category}</span>
              {isPast(e.eventDate, e.endDate) && <span className="badge past">Vorbei</span>}
              <h3>{e.title}</h3>
              <div className="desc desc-clamp">{e.description}</div>
              <div className="meta">
                <span>{[formatSchedule(e.eventDate, e.endDate), e.location].filter(Boolean).join(' · ')}</span>
                {isModerator && <span>{e.authorName}</span>}
              </div>
            </article>
          ))}
        </div>
      )}

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
      {(showForm || editing) && (
        <EventFormModal
          existing={editing ?? undefined}
          onClose={() => {
            setShowForm(false)
            setEditing(null)
          }}
          onSaved={reload}
        />
      )}
    </>
  )
}
