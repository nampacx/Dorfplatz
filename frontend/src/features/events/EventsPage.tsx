import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, useAsync } from '../../components/Common'
import EventDetail from './EventDetail'
import EventFormModal from './EventFormModal'
import { EVENT_CATEGORIES, type EventCategory, type EventPost } from '../../types'

function fmtDate(iso: string | null) {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'short', year: 'numeric' })
}

export default function EventsPage() {
  const { me, isApproved, isModerator } = useAuth()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<EventPost | null>(null)
  const [selected, setSelected] = useState<EventPost | null>(null)
  const [category, setCategory] = useState<EventCategory | 'all'>('all')
  const { data, loading, error, reload } = useAsync(
    () => api.get<EventPost[]>(`/events${category === 'all' ? '' : `?category=${category}`}`),
    [category],
  )

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
      </div>

      {error && <ErrorNotice message={error} />}
      {loading ? (
        <Loading />
      ) : !data?.length ? (
        <EmptyState>
          {category === 'all'
            ? 'Noch keine veröffentlichten Events. Sei die/der Erste!'
            : `Keine Events in der Kategorie ${category}.`}
        </EmptyState>
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
              <h3>{e.title}</h3>
              <div className="desc desc-clamp">{e.description}</div>
              <div className="meta">
                <span>{[fmtDate(e.eventDate), e.location].filter(Boolean).join(' · ')}</span>
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
