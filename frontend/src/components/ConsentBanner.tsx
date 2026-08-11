import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/useAuth'

const LOCAL_KEY = 'bb-consent-dismissed'

/**
 * GDPR consent banner. For signed-in users the acceptance is persisted server-side
 * (AppUser.ConsentedAt); anonymous visitors get a local dismissal only.
 */
export default function ConsentBanner() {
  const { me, refresh } = useAuth()
  const [dismissed, setDismissed] = useState(() => localStorage.getItem(LOCAL_KEY) === '1')

  if (dismissed || me?.hasConsented) return null

  async function accept() {
    localStorage.setItem(LOCAL_KEY, '1')
    setDismissed(true)
    if (me) {
      try {
        await api.post('/account/consent')
        await refresh()
      } catch {
        /* best-effort */
      }
    }
  }

  return (
    <div className="consent" role="dialog" aria-label="Datenschutz-Hinweis">
      <p>
        Wir verwenden nur technisch notwendige Cookies für die Anmeldung. Mit der Nutzung
        stimmst du unserer <Link to="/privacy" style={{ color: 'var(--pin)' }}>Datenschutzerklärung</Link> zu.
      </p>
      <button className="btn" onClick={accept}>Verstanden</button>
    </div>
  )
}
