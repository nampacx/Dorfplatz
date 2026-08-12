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
      {/* Kept to two lines at 375px: the banner is fixed over the content, so
          every extra line is screen the member cannot use until they accept.
          The link colour comes from `.consent a`; the inline style it used to
          carry pointed at var(--pin), which is not defined anywhere, so the
          declaration was dropped and the link did not read as a link at all. */}
      <p>
        Nur technisch notwendige Cookies. Mehr in der{' '}
        <Link to="/privacy">Datenschutzerklärung</Link>.
      </p>
      <button className="btn" onClick={accept}>Verstanden</button>
    </div>
  )
}
