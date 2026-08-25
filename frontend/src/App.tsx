import { NavLink, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/useAuth'
import { useChatConnection } from './chat/ChatConnection'
import ConsentBanner from './components/ConsentBanner'
import TabBar from './components/TabBar'
import HomePage from './features/home/HomePage'
import EventsPage from './features/events/EventsPage'
import ModerationPage from './features/events/ModerationPage'
import ServicesPage from './features/services/ServicesPage'
import GoodsPage from './features/goods/GoodsPage'
import ChatPage from './features/chat/ChatPage'
import AdminPage from './features/admin/AdminPage'
import AccountPage from './features/account/AccountPage'
import PrivacyPage from './features/account/PrivacyPage'
import LoginPage from './features/account/LoginPage'

/**
 * Slim header. Below 900px it carries only the brand and a link to the
 * account page — navigation lives in the bottom TabBar, where a thumb can
 * reach it. From 900px up the full nav and user box reappear here and the
 * TabBar is hidden, so desktop keeps the layout it always had.
 */
function AppHeader() {
  const { me, isModerator, isAdmin } = useAuth()
  const { unreadTotal } = useChatConnection()
  return (
    <header className="topbar">
      <span className="brand">
        <span className="dot">●</span> Schwarzes&nbsp;Brett
      </span>
      <nav className="nav wide-only">
        <NavLink to="/" end>Home</NavLink>
        <NavLink to="/events">Events</NavLink>
        <NavLink to="/services">Dienstleistungen</NavLink>
        <NavLink to="/goods">Marktplatz</NavLink>
        {me && (
          <NavLink to="/chat">
            Chat
            {unreadTotal > 0 && (
              <span className="nav-badge" aria-label={`${unreadTotal} ungelesene Nachrichten`}>
                {unreadTotal > 99 ? '99+' : unreadTotal}
              </span>
            )}
          </NavLink>
        )}
        {isModerator && <NavLink to="/moderation">Moderation</NavLink>}
        {isAdmin && <NavLink to="/admin">Admin</NavLink>}
      </nav>
      <span className="spacer" />
      <div className="userbox">
        {me ? (
          <>
            <NavLink className="account-link" to="/account">{me.displayName ?? me.email ?? 'Konto'}</NavLink>
            {/* Signing out is a rare, deliberate act: on phones it sits on the
                account page with the other account-level controls. */}
            <a className="btn secondary small wide-only" href="/.auth/logout?post_logout_redirect_uri=/">Abmelden</a>
          </>
        ) : (
          <NavLink className="btn small wide-only" to="/login">Anmelden</NavLink>
        )}
      </div>
    </header>
  )
}

export default function App() {
  return (
    <div className="app">
      <AppHeader />
      <main className="container">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/events" element={<EventsPage />} />
          <Route path="/services" element={<ServicesPage />} />
          <Route path="/goods" element={<GoodsPage />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/moderation" element={<ModerationPage />} />
          <Route path="/admin" element={<AdminPage />} />
          <Route path="/account" element={<AccountPage />} />
          <Route path="/privacy" element={<PrivacyPage />} />
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </main>
      <TabBar />
      <ConsentBanner />
    </div>
  )
}
