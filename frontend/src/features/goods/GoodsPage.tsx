import { useState } from 'react'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { EmptyState, ErrorNotice, Loading, RequireMember, useAsync } from '../../components/Common'
import Sheet from '../../components/Sheet'
import Gallery from './Gallery'
import ContactButton from '../../components/ContactButton'
import DetailModal from '../../components/DetailModal'
import type { GoodsImage, GoodsPost, UploadSlot } from '../../types'

const CONDITIONS = ['New', 'LikeNew', 'Good', 'Used', 'Defective']
const CONDITION_LABELS: Record<string, string> = {
  New: 'Neu', LikeNew: 'Wie neu', Good: 'Gut', Used: 'Gebraucht', Defective: 'Defekt',
}

function money(v: number, c: string) {
  return new Intl.NumberFormat('de-DE', { style: 'currency', currency: c || 'EUR' }).format(v)
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('de-DE', { day: '2-digit', month: 'long', year: 'numeric' })
}

/** Full listing: all images, complete description and the contact action. */
function GoodsDetail({
  item,
  onClose,
  onEdit,
  onDeleted,
}: {
  item: GoodsPost
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
      await api.del(`/goods/${item.id}`)
      onDeleted()
      onClose()
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <DetailModal title={item.title} onClose={onClose}>
      <Gallery images={item.images} title={item.title} />

      <h2 className="detail-title">{item.title}</h2>
      <div className="price">{money(item.price, item.currency)}</div>

      <dl className="detail-facts">
        <dt>Zustand</dt><dd>{CONDITION_LABELS[item.condition] ?? item.condition}</dd>
        {item.category && (<><dt>Kategorie</dt><dd>{item.category}</dd></>)}
        {item.location && (<><dt>Ort</dt><dd>{item.location}</dd></>)}
        <dt>Anbieter</dt><dd>{item.authorName}</dd>
        <dt>Eingestellt</dt><dd>{fmtDate(item.createdAt)}</dd>
      </dl>

      <p className="detail-desc">{item.description}</p>

      {error && <ErrorNotice message={error} />}

      {confirming ? (
        <div className="confirm">
          <p>Diese Anzeige wirklich löschen? Bilder werden mit entfernt.</p>
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
            listingType="goods"
            listingId={item.id}
            label="Verkäufer kontaktieren"
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

/** Create when `existing` is omitted, otherwise edit that listing. */
function GoodsFormModal({
  existing,
  onClose,
  onSaved,
}: {
  existing?: GoodsPost
  onClose: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState({
    title: existing?.title ?? '',
    description: existing?.description ?? '',
    price: existing ? String(existing.price) : '',
    currency: existing?.currency ?? 'EUR',
    condition: existing?.condition ?? 'Used',
    category: existing?.category ?? '',
    location: existing?.location ?? '',
  })
  // Existing images start kept; unchecking one removes it on save.
  const [keptImages, setKeptImages] = useState<GoodsImage[]>(existing?.images ?? [])
  const [files, setFiles] = useState<File[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const slotsLeft = 8 - keptImages.length

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const blobNames = await uploadImages(files.slice(0, Math.max(slotsLeft, 0)))

      if (existing) {
        await api.patch(`/goods/${existing.id}`, {
          title: form.title,
          description: form.description,
          price: Number(form.price) || 0,
          currency: form.currency,
          condition: form.condition,
          category: form.category || null,
          location: form.location || null,
          keepImageIds: keptImages.map((i) => i.id),
          newBlobNames: blobNames,
        })
        onSaved()
        onClose()
        return
      }

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
      onSaved()
      onClose()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Sheet title={existing ? 'Anzeige bearbeiten' : 'Artikel anbieten'} onClose={onClose}>
      <form onSubmit={submit}>
        <h2 style={{ marginTop: 0 }}>{existing ? 'Anzeige bearbeiten' : 'Artikel anbieten'}</h2>
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
        {keptImages.length > 0 && (
          <div className="field">
            <label>Vorhandene Bilder</label>
            <div className="image-editor">
              {keptImages.map((img) => (
                <div key={img.id} className="image-editor-item">
                  <img src={img.url} alt="" />
                  <button
                    type="button"
                    className="image-remove"
                    aria-label="Bild entfernen"
                    onClick={() => setKeptImages(keptImages.filter((k) => k.id !== img.id))}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}
        <div className="field">
          <label>{existing ? 'Bilder hinzufügen' : 'Bilder (bis zu 8)'}</label>
          <input
            type="file"
            accept="image/*"
            multiple
            disabled={slotsLeft <= 0}
            onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
          />
          {slotsLeft <= 0
            ? <small>Maximal 8 Bilder. Entferne eines, um Platz zu schaffen.</small>
            : files.length > 0 && <small>{files.length} Datei(en) ausgewählt · noch {slotsLeft} frei</small>}
        </div>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button type="button" className="btn secondary" onClick={onClose}>Abbrechen</button>
          <button className="btn" disabled={busy}>
            {busy ? 'Speichert…' : existing ? 'Speichern' : 'Anbieten'}
          </button>
        </div>
      </form>
    </Sheet>
  )
}

/** Members only — see the access note in GoodsFunctions. */
export default function GoodsPage() {
  return (
    <RequireMember message="Melde dich an, um den Marktplatz zu sehen.">
      <GoodsBoard />
    </RequireMember>
  )
}

function GoodsBoard() {
  const { me } = useAuth()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<GoodsPost | null>(null)
  const [selected, setSelected] = useState<GoodsPost | null>(null)
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
            <article
              key={g.id}
              className="card card-clickable"
              role="button"
              tabIndex={0}
              aria-label={`${g.title} – Details ansehen`}
              onClick={() => setSelected(g)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault()
                  setSelected(g)
                }
              }}
            >
              {g.images[0] && (
                <img
                  className="thumb"
                  src={g.images[0].url}
                  alt={g.title}
                  loading="lazy"
                  decoding="async"
                  width={800}
                  height={600}
                />
              )}
              <h3>{g.title}</h3>
              <div className="price">{money(g.price, g.currency)}</div>
              <div className="desc desc-clamp">{g.description}</div>
              <div className="meta">
                <span className="badge role">{CONDITION_LABELS[g.condition] ?? g.condition}</span>
                <span>{[g.location, g.authorName].filter(Boolean).join(' · ')}</span>
              </div>
            </article>
          ))}
        </div>
      )}

      {selected && (
        <GoodsDetail
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
        <GoodsFormModal
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
