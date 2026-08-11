import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/useAuth'

/** Starts (or reuses) a conversation with a listing's author and jumps to chat. */
export default function ContactButton({
  recipientId,
  listingType,
  listingId,
  label = 'Nachricht senden',
}: {
  recipientId: string
  listingType: 'goods' | 'service'
  listingId: string
  label?: string
}) {
  const { me } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (!me || me.id === recipientId) return null

  async function send() {
    if (!text.trim()) return
    setBusy(true)
    setError(null)
    try {
      const res = await api.post<{ conversationId: string }>('/conversations', {
        recipientId,
        listingType,
        listingId,
        firstMessage: text,
      })
      navigate(`/chat?c=${res.conversationId}`)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <button className="btn small secondary" onClick={() => setOpen(true)}>{label}</button>
      {open && (
        <div className="modal-backdrop" onClick={() => setOpen(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2 style={{ marginTop: 0 }}>Nachricht schreiben</h2>
            {error && <div className="notice error">{error}</div>}
            <div className="field">
              <textarea placeholder="Deine Nachricht…" value={text} onChange={(e) => setText(e.target.value)} />
            </div>
            <div className="row" style={{ justifyContent: 'flex-end' }}>
              <button className="btn secondary" onClick={() => setOpen(false)}>Abbrechen</button>
              <button className="btn" disabled={busy || !text.trim()} onClick={send}>Senden</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
