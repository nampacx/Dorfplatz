import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, useAsync } from '../../components/Common'
import ContactButton from '../../components/ContactButton'
import type { ServicePost } from '../../types'

type Filter = 'all' | 'seeking' | 'offering'

function SubmitServiceModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [form, setForm] = useState({ type: 'Offering', title: '', description: '', category: '' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.post('/services', { ...form, category: form.category || null })
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
        <h2 style={{ marginTop: 0 }}>Dienstleistung einstellen</h2>
        {error && <ErrorNotice message={error} />}
        <div className="field">
          <label>Art</label>
          <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
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
          <button className="btn" disabled={busy}>{busy ? 'Sende…' : 'Einstellen'}</button>
        </div>
      </form>
    </div>
  )
}

export default function ServicesPage() {
  const { me } = useAuth()
  const [filter, setFilter] = useState<Filter>('all')
  const [showForm, setShowForm] = useState(false)
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
            <article key={s.id} className="card">
              <span className={`badge ${s.type.toLowerCase()}`}>{s.type === 'Seeking' ? 'Ich suche' : 'Ich biete'}</span>
              <h3>{s.title}</h3>
              <div className="desc">{s.description}</div>
              <div className="meta">
                <span>{s.category}</span>
                <span>{s.authorName}</span>
              </div>
              <div style={{ marginTop: '0.7rem' }}>
                <ContactButton recipientId={s.authorId} listingType="service" listingId={s.id} />
              </div>
            </article>
          ))}
        </div>
      )}

      {showForm && <SubmitServiceModal onClose={() => setShowForm(false)} onCreated={reload} />}
    </>
  )
}
