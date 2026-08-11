import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, useAsync } from '../../components/Common'
import ContactButton from '../../components/ContactButton'
import type { GoodsPost, UploadSlot } from '../../types'

const CONDITIONS = ['New', 'LikeNew', 'Good', 'Used', 'Defective']
const CONDITION_LABELS: Record<string, string> = {
  New: 'Neu', LikeNew: 'Wie neu', Good: 'Gut', Used: 'Gebraucht', Defective: 'Defekt',
}

function money(v: number, c: string) {
  return new Intl.NumberFormat('de-DE', { style: 'currency', currency: c || 'EUR' }).format(v)
}

/** Uploads the selected files to Blob via SAS, returns the stored blob names. */
async function uploadImages(files: File[]): Promise<string[]> {
  if (files.length === 0) return []
  const slots = await api.post<UploadSlot[]>('/goods/uploads', {
    files: files.map((f) => ({
      contentType: f.type,
      sizeBytes: f.size,
      extension: f.name.slice(f.name.lastIndexOf('.')) || '.jpg',
    })),
  })

  await Promise.all(
    slots.map((slot, i) =>
      fetch(slot.uploadUrl, {
        method: 'PUT',
        headers: { 'x-ms-blob-type': 'BlockBlob', 'Content-Type': files[i].type },
        body: files[i],
      }).then((r) => {
        if (!r.ok) throw new Error('Bild-Upload fehlgeschlagen.')
      }),
    ),
  )
  return slots.map((s) => s.blobName)
}

function SubmitGoodsModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [form, setForm] = useState({
    title: '', description: '', price: '', currency: 'EUR', condition: 'Used', category: '', location: '',
  })
  const [files, setFiles] = useState<File[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const blobNames = await uploadImages(files.slice(0, 8))
      await api.post('/goods', {
        title: form.title,
        description: form.description,
        price: Number(form.price) || 0,
        currency: form.currency,
        condition: form.condition,
        category: form.category || null,
        location: form.location || null,
        blobNames,
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
        <h2 style={{ marginTop: 0 }}>Artikel anbieten</h2>
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
            <label>Preis</label>
            <input type="number" min="0" step="0.01" required value={form.price} onChange={(e) => setForm({ ...form, price: e.target.value })} />
          </div>
          <div className="field">
            <label>Zustand</label>
            <select value={form.condition} onChange={(e) => setForm({ ...form, condition: e.target.value })}>
              {CONDITIONS.map((c) => <option key={c} value={c}>{CONDITION_LABELS[c]}</option>)}
            </select>
          </div>
        </div>
        <div className="row">
          <div className="field">
            <label>Kategorie</label>
            <input value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} />
          </div>
          <div className="field">
            <label>Ort</label>
            <input value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} />
          </div>
        </div>
        <div className="field">
          <label>Bilder (bis zu 8)</label>
          <input type="file" accept="image/*" multiple onChange={(e) => setFiles(Array.from(e.target.files ?? []))} />
          {files.length > 0 && <small>{files.length} Datei(en) ausgewählt</small>}
        </div>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button type="button" className="btn secondary" onClick={onClose}>Abbrechen</button>
          <button className="btn" disabled={busy}>{busy ? 'Lade hoch…' : 'Anbieten'}</button>
        </div>
      </form>
    </div>
  )
}

export default function GoodsPage() {
  const { me } = useAuth()
  const [showForm, setShowForm] = useState(false)
  const { data, loading, error, reload } = useAsync(() => api.get<GoodsPost[]>('/goods'), [])

  return (
    <>
      <div className="page-head" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1>Marktplatz</h1>
          <p>Gebrauchtes verkaufen und finden — wie am schwarzen Brett.</p>
        </div>
        {me && <button className="btn" onClick={() => setShowForm(true)}>+ Artikel anbieten</button>}
      </div>

      {error && <ErrorNotice message={error} />}
      {loading ? (
        <Loading />
      ) : !data?.length ? (
        <EmptyState>Noch keine Angebote.</EmptyState>
      ) : (
        <div className="grid">
          {data.map((g) => (
            <article key={g.id} className="card">
              {g.imageUrls[0] && <img className="thumb" src={g.imageUrls[0]} alt={g.title} loading="lazy" />}
              <h3>{g.title}</h3>
              <div className="price">{money(g.price, g.currency)}</div>
              <div className="desc">{g.description}</div>
              <div className="meta">
                <span className="badge role">{CONDITION_LABELS[g.condition] ?? g.condition}</span>
                <span>{[g.location, g.authorName].filter(Boolean).join(' · ')}</span>
              </div>
              <div style={{ marginTop: '0.7rem' }}>
                <ContactButton recipientId={g.authorId} listingType="goods" listingId={g.id} label="Anfragen" />
              </div>
            </article>
          ))}
        </div>
      )}

      {showForm && <SubmitGoodsModal onClose={() => setShowForm(false)} onCreated={reload} />}
    </>
  )
}
