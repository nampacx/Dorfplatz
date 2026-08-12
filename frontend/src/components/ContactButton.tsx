import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/useAuth'

/** Starts (or reuses) a conversation with a listing's author and jumps to chat. */
export default function ContactButton({
  recipientId,
  listingType,
  listingId,
  label = 'Nachricht senden',
  className = 'btn small secondary',
}: {
  recipientId: string
  listingType: 'goods' | 'service'
  listingId: string
  label?: string
  className?: string
}) {
  const { me } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Say why there is no reply button, rather than rendering nothing.
  if (!me) {
    return (
      <a className={className} href={`/.auth/login/aad?post_login_redirect_uri=${encodeURIComponent(location.pathname)}`}>
        Anmelden, um zu antworten
      </a>
    )
  }
  if (me.id === recipientId) {
    return <p className="contact-note">Das ist dein eigener Eintrag.</p>
  }

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

  // Expands in place rather than opening a second overlay on top of the
  // detail sheet. There is room on a full-screen sheet, the listing stays
  // visible while you write about it, and it removes a third stacked layer.
  if (open) {
    return (
      <div className="contact-form">
        {error && <div className="notice error">{error}</div>}
        <div className="field">
          <label>Nachricht schreiben</label>
          <textarea
            autoFocus
            placeholder="Deine Nachricht…"
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
        </div>
        <div className="row">
          <button className="btn secondary" onClick={() => setOpen(false)}>Abbrechen</button>
          <button className="btn" disabled={busy || !text.trim()} onClick={send}>Senden</button>
        </div>
      </div>
    )
  }

  return <button className={className} onClick={() => setOpen(true)}>{label}</button>
}
