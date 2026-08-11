import { NavLink, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/useAuth'
import ConsentBanner from './components/ConsentBanner'
import EventsPage from './features/events/EventsPage'
import ModerationPage from './features/events/ModerationPage'
import ServicesPage from './features/services/ServicesPage'
import GoodsPage from './features/goods/GoodsPage'
import ChatPage from './features/chat/ChatPage'
import AdminPage from './features/admin/AdminPage'
import AccountPage from './features/account/AccountPage'
import PrivacyPage from './features/account/PrivacyPage'
import LoginPage from './features/account/LoginPage'

function TopBar() {
  const { me, isModerator, isAdmin } = useAuth()
  return (
    <header className="topbar">
      <span className="brand">
        <span className="dot">●</span> Schwarzes&nbsp;Brett
      </span>
      <nav className="nav">
        <NavLink to="/" end>Events</NavLink>
        <NavLink to="/services">Dienstleistungen</NavLink>
        <NavLink to="/goods">Marktplatz</NavLink>
        {me && <NavLink to="/chat">Nachrichten</NavLink>}
        {isModerator && <NavLink to="/moderation">Moderation</NavLink>}
        {isAdmin && <NavLink to="/admin">Admin</NavLink>}
      </nav>
      <span className="spacer" />
      <div className="userbox">
        {me ? (
          <>
            <NavLink to="/account">{me.displayName ?? me.email ?? 'Konto'}</NavLink>
            <a className="btn secondary small" href="/.auth/logout?post_logout_redirect_uri=/">Abmelden</a>
          </>
        ) : (
          <NavLink className="btn small" to="/login">Anmelden</NavLink>
        )}
      </div>
    </header>
  )
}

export default function App() {
  return (
    <div className="app">
      <TopBar />
      <main className="container">
        <Routes>
          <Route path="/" element={<EventsPage />} />
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
      <ConsentBanner />
    </div>
  )
}
