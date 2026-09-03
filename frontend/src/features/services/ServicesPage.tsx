import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, RequireMember, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import ContactButton from '../../components/ContactButton'
import DetailModal from '../../components/DetailModal'
import type { ServicePost } from '../../types'

type Filter = 'all' | 'seeking' | 'offering'

function typeLabel(type: ServicePost['type']) {
  return type === 'Seeking' ? 'Ich suche' : 'Ich biete'
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/** Full service post with the complete description and the contact action. */
function ServiceDetail({
  item,
  onClose,
  onEdit,
  onDeleted,
}: {
  item: ServicePost
  onClose: () => void
  onEdit: () => void
  onDeleted: () => void
}) {
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function remove() {
    setBusy(true)
    setError(null)
    try {
      await api.del(`/services/${item.id}`)
      onDeleted()
      onClose()
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <DetailModal title={item.title} onClose={onClose}>
      <span className={`badge ${item.type.toLowerCase()}`}>{typeLabel(item.type)}</span>
      <h2 className="detail-title">{item.title}</h2>

      <dl className="detail-facts">
        {item.category && (<><dt>Kategorie</dt><dd>{item.category}</dd></>)}
        <dt>Von</dt><dd>{item.authorName}</dd>
        <dt>Eingestellt</dt><dd>{fmtDate(item.createdAt)}</dd>
      </dl>

      <p className="detail-desc">{item.description}</p>

      {error && <ErrorNotice message={error} />}

      {confirming ? (
        <div className="confirm">
          <p>Diesen Eintrag wirklich löschen? Das lässt sich nicht rückgängig machen.</p>
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
          <ContactButton
            recipientId={item.authorId}
            listingType="service"
            listingId={item.id}
            label={item.type === 'Seeking' ? 'Hilfe anbieten' : 'Anfragen'}
            className="btn"
          />
          {item.canEdit && (
            <>
              <button className="btn secondary" onClick={onEdit}>Bearbeiten</button>
              <button className="btn danger" onClick={() => setConfirming(true)}>Löschen</button>
            </>
          )}
          <button className="btn secondary" onClick={onClose}>Schließen</button>
        </div>
      )}
    </DetailModal>
  )
}

/** Create when `existing` is omitted, otherwise edit that post. */
function ServiceFormModal({
  existing,
  onClose,
  onSaved,
}: {
  existing?: ServicePost
  onClose: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState({
    type: existing?.type ?? 'Offering',
    title: existing?.title ?? '',
    description: existing?.description ?? '',
    category: existing?.category ?? '',
  })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const payload = { ...form, category: form.category || null }
      if (existing) await api.patch(`/services/${existing.id}`, payload)
      else await api.post('/services', payload)
      onSaved()
      onClose()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Sheet title={existing ? 'Eintrag bearbeiten' : 'Dienstleistung einstellen'} onClose={onClose}>
      <form onSubmit={submit}>
        <h2 style={{ marginTop: 0 }}>
          {existing ? 'Eintrag bearbeiten' : 'Dienstleistung einstellen'}
        </h2>
        {error && <ErrorNotice message={error} />}
        <div className="field">
          <label>Art</label>
          <select
            value={form.type}
            onChange={(e) => setForm({ ...form, type: e.target.value as ServicePost['type'] })}
          >
            <option value="Offering">Ich biete</option>
            <option value="Seeking">Ich suche</option>
          </select>
        </div>
        <div className="field">
          <label>Titel</label>
          <input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
        </div>
        <div className="field">
          <label>Beschreibung</label>
          <textarea required value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </div>
        <div className="field">
          <label>Kategorie (optional)</label>
          <input value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} />
        </div>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button type="button" className="btn secondary" onClick={onClose}>Abbrechen</button>
          <button className="btn" disabled={busy}>
            {busy ? 'Speichert…' : existing ? 'Speichern' : 'Einstellen'}
          </button>
        </div>
      </form>
    </Sheet>
  )
}

/** Members only — see the access note in ServicesFunctions. */
export default function ServicesPage() {
  return (
    <RequireMember message="Melde dich an, um Dienstleistungen zu sehen.">
      <ServicesBoard />
    </RequireMember>
  )
}

function ServicesBoard() {
  const { me, isModerator } = useAuth()
  const [filter, setFilter] = useState<Filter>('all')
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<ServicePost | null>(null)
  const [selected, setSelected] = useState<ServicePost | null>(null)
  const { data, loading, error, reload } = useAsync(
    () => api.get<ServicePost[]>(`/services${filter === 'all' ? '' : `?type=${filter}`}`),
    [filter],
  )

  return (
    <>
      <div className="page-head" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1>Dienstleistungen</h1>
          <p>Nachbarschaftshilfe: „ich suche“ und „ich biete“.</p>
        </div>
        {me && <button className="btn" onClick={() => setShowForm(true)}>+ Einstellen</button>}
      </div>

      <div className="toolbar">
        {(['all', 'seeking', 'offering'] as Filter[]).map((f) => (
          <button key={f} className={`chip ${filter === f ? 'active' : ''}`} onClick={() => setFilter(f)}>
            {f === 'all' ? 'Alle' : f === 'seeking' ? 'Ich suche' : 'Ich biete'}
          </button>
        ))}
      </div>

      {error && <ErrorNotice message={error} />}
      {loading ? (
        <Loading />
      ) : !data?.length ? (
        <EmptyState>Keine Einträge in dieser Kategorie.</EmptyState>
      ) : (
        <div className="grid">
          {data.map((s) => (
            <article
              key={s.id}
              className="card card-clickable"
              role="button"
              tabIndex={0}
              aria-label={`${s.title} – Details ansehen`}
              onClick={() => setSelected(s)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  setSelected(s)
                }
              }}
            >
              <span className={`badge ${s.type.toLowerCase()}`}>{typeLabel(s.type)}</span>
              <h3>{s.title}</h3>
              <div className="desc desc-clamp">{s.description}</div>
              <div className="meta">
                <span>{s.category}</span>
                {isModerator && <span>{s.authorName}</span>}
              </div>
            </article>
          ))}
        </div>
      )}

      {selected && (
        <ServiceDetail
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
        <ServiceFormModal
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
