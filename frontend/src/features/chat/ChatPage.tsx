import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { api } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { LoginGate, Loading } from '../../components/Common'
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
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [activeId, setActiveId] = useState<string | null>(params.get('c'))
  const [messages, setMessages] = useState<Message[]>([])
  const [text, setText] = useState('')
  const endRef = useRef<HTMLDivElement>(null)

  // Load conversation list.
  async function loadConversations() {
    const list = await api.get<Conversation[]>('/conversations')
    setConversations(list)
    if (!activeId && list.length) setActiveId(list[0].id)
  }
  useEffect(() => {
    void loadConversations()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Load messages for the active conversation.
  useEffect(() => {
    if (!activeId) return
    setParams({ c: activeId }, { replace: true })
    void api.get<Message[]>(`/conversations/${activeId}/messages`).then(setMessages)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeId])

  // Real-time delivery via Azure SignalR (serverless).
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(api.base) // client POSTs to {base}/negotiate
      .withAutomaticReconnect()
      .build()

    connection.on('newMessage', (msg: Message) => {
      setMessages((prev) => (msg.conversationId === activeIdRef.current ? [...prev, { ...msg, isMine: false }] : prev))
      void loadConversations()
    })

    connection.start().catch(() => {/* offline fallback: messages still load on open */})
    return () => void connection.stop()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Keep a ref of activeId for the SignalR handler closure.
  const activeIdRef = useRef(activeId)
  useEffect(() => { activeIdRef.current = activeId }, [activeId])

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
      <div className="page-head"><h1>Nachrichten</h1></div>
      {conversations.length === 0 ? (
        <div className="empty">Noch keine Unterhaltungen. Schreibe jemandem über ein Angebot oder eine Anzeige.</div>
      ) : (
        <div className="chat">
          <div className="convo-list">
            {conversations.map((c) => (
              <div key={c.id} className={`convo ${c.id === activeId ? 'active' : ''}`} onClick={() => setActiveId(c.id)}>
                <div className="name">{c.otherUserName}</div>
                <div className="time">{c.listingType ? `↳ ${c.listingType}` : ''} · {fmtTime(c.lastMessageAt)}</div>
              </div>
            ))}
          </div>
          <div className="thread">
            <div className="messages">
              {active && <div className="time" style={{ textAlign: 'center', color: 'var(--ink-soft)' }}>Unterhaltung mit {active.otherUserName}</div>}
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
