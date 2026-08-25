import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useChatConnection } from '../chat/ChatConnection'

/**
 * Primary navigation on phones: five fixed slots in the thumb zone.
 *
 * It replaces a top bar that wrapped to 4-5 rows and ate 143-210px of a 812px
 * viewport while sticky.
 *
 * Moderation and Admin therefore live on the account page rather than here: a
 * tab bar whose contents shift depending on who you are is disorienting, and
 * both are rare, deliberate destinations. Signed out, the Nachrichten slot
 * becomes Anmelden, so the slot count never changes and the layout never
 * shifts — and the one action we want from a signed-out visitor lands under
 * their thumb.
 *
 * Labels are shortened here only ("Dienste", "Markt"); the page headings keep
 * the full words. No icons: no generic glyph means "Dienstleistungen", and
 * inventing one would cost a dependency the CSP forbids anyway.
 */
export default function TabBar() {
  const { me } = useAuth()
  const { unreadTotal } = useChatConnection()

  return (
    <nav className="tabbar" aria-label="Hauptnavigation">
      <NavLink to="/" end>Home</NavLink>
      <NavLink to="/events">Events</NavLink>
      <NavLink to="/services">Dienste</NavLink>
      <NavLink to="/goods">Markt</NavLink>
      {me ? (
        <NavLink to="/chat">
          Nachrichten
          {unreadTotal > 0 && (
            <span className="tab-badge" aria-label={`${unreadTotal} ungelesene Nachrichten`}>
              {unreadTotal > 99 ? '99+' : unreadTotal}
            </span>
          )}
        </NavLink>
      ) : (
        <NavLink to="/login">Anmelden</NavLink>
      )}
    </nav>
  )
}
