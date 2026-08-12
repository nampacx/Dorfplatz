import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { useChatConnection } from '../../chat/ChatConnection'
import { LoginGate, Loading } from '../../components/Common'
import useIsWide from '../../hooks/useIsWide'
import type { Conversation, Message } from '../../types'

export default function ChatPage() {
  const { me, loading } = useAuth()
  if (loading) return <Loading />
  if (!me) return <LoginGate message="Melde dich an, um Nachrichten zu senden." />
  return <Chat />
}

function fmtTime(iso: string) {
  return new Date(iso).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

function Chat() {
  const [params, setParams] = useSearchParams()
  const isWide = useIsWide()
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [activeId, setActiveId] = useState<string | null>(params.get('c'))
  const [messages, setMessages] = useState<Message[]>([])
  const [text, setText] = useState('')
  const endRef = useRef<HTMLDivElement>(null)
  const { subscribe, markConversationRead, setActiveConversation } = useChatConnection()

  /** On a phone the thread is a screen of its own, not a second pane. */
  const threadOnPhone = !isWide && !!activeId

  // Load conversation list.
  async function loadConversations() {
    const list = await api.get<Conversation[]>('/conversations')
    setConversations(list)
    // Opening the first thread automatically is right for the two-pane desktop
    // layout and wrong on a phone, where it would skip the list entirely and
    // drop the member into someone's conversation. A /chat?c=… deep link still
    // lands straight in the thread, because activeId is seeded from the query.
    if (isWide && !activeId && list.length) setActiveId(list[0].id)
  }
  useEffect(() => {
    void loadConversations()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Back gesture: the URL is what changes, so follow it back to the list.
  const paramC = params.get('c')
  useEffect(() => {
    if (!isWide) setActiveId(paramC)
  }, [paramC, isWide])

  // Hides the tab bar and reclaims its space; a nav bar sitting under a text
  // input is the standard mistake, and the composer needs those 56px.
  useEffect(() => {
    document.body.dataset.chatThread = threadOnPhone ? 'open' : ''
    return () => {
      document.body.dataset.chatThread = ''
    }
  }, [threadOnPhone])

  // Load messages for the active conversation, and clear its unread state.
  useEffect(() => {
    if (!activeId) return
    // Push rather than replace on a phone: the thread is its own screen there,
    // so the hardware Back button should return to the list, not leave chat.
    setParams({ c: activeId }, { replace: isWide })
    setActiveConversation(activeId)
    void api.get<Message[]>(`/conversations/${activeId}/messages`).then(setMessages)
    void markConversationRead(activeId).then(loadConversations)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeId])

  // Release the "on screen" marker when leaving the page, so later messages count.
  useEffect(() => () => setActiveConversation(null), [setActiveConversation])

  // Live delivery rides on the app-wide connection rather than a second one.
  // Re-subscribing when the thread changes costs a Set swap, so the handler closes
  // over activeId directly instead of mirroring it into a ref.
  useEffect(() => {
    return subscribe((msg: Message) => {
      if (msg.conversationId === activeId) {
        setMessages((prev) => [...prev, { ...msg, isMine: false }])
        // Already on screen, so it should not linger as unread.
        void markConversationRead(msg.conversationId)
      }
      void loadConversations()
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [subscribe, markConversationRead, activeId])

  useEffect(() => { endRef.current?.scrollIntoView({ behavior: 'smooth' }) }, [messages])

  async function send() {
    if (!text.trim() || !activeId) return
    const body = text
    setText('')
    const msg = await api.post<Message>('/messages', { conversationId: activeId, body })
    setMessages((prev) => [...prev, { ...msg, isMine: true }])
    void loadConversations()
  }

  const active = conversations.find((c) => c.id === activeId)

  return (
    <>
      {/* The thread wants the whole screen on a phone; the page title would
          cost a fifth of it to say something the thread header already says. */}
      {!threadOnPhone && <div className="page-head"><h1>Nachrichten</h1></div>}
      {conversations.length === 0 ? (
        <div className="empty">Noch keine Unterhaltungen. Schreibe jemandem über ein Angebot oder eine Anzeige.</div>
      ) : (
        <div className={threadOnPhone ? 'chat thread-open' : 'chat'}>
          <div className="convo-list">
            {conversations.map((c) => (
              <div
                key={c.id}
                className={`convo ${c.id === activeId ? 'active' : ''} ${c.unreadCount > 0 ? 'unread' : ''}`}
                onClick={() => setActiveId(c.id)}
              >
                <div className="name">
                  {c.otherUserName}
                  {c.unreadCount > 0 && <span className="convo-badge">{c.unreadCount}</span>}
                </div>
                <div className="time">{c.listingType ? `↳ ${c.listingType}` : ''} · {fmtTime(c.lastMessageAt)}</div>
              </div>
            ))}
          </div>
          <div className="thread">
            {threadOnPhone && (
              <div className="thread-head">
                {/* Back rather than clearing state directly, so this and the
                    hardware Back gesture are the same single code path. */}
                <button
                  className="thread-back"
                  onClick={() => window.history.back()}
                  aria-label="Zurück zur Übersicht"
                >
                  ‹ Zurück
                </button>
                <span className="thread-name">{active?.otherUserName}</span>
              </div>
            )}
            <div className="messages">
              {active && !threadOnPhone && <div className="time" style={{ textAlign: 'center', color: 'var(--ink-soft)' }}>Unterhaltung mit {active.otherUserName}</div>}
              {messages.map((m) => (
                <div key={m.id} className={`bubble ${m.isMine ? 'mine' : 'theirs'}`}>
                  {m.body}
                  <div style={{ fontSize: '0.7rem', opacity: 0.7, marginTop: 2 }}>{fmtTime(m.sentAt)}</div>
                </div>
              ))}
              <div ref={endRef} />
            </div>
            <div className="composer">
              <input
                value={text}
                placeholder="Nachricht schreiben…"
                onChange={(e) => setText(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && send()}
              />
              <button className="btn" onClick={send} disabled={!text.trim()}>Senden</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
