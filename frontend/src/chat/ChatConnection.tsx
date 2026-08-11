import {
  createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode,
} from 'react'
import * as signalR from '@microsoft/signalr'
import { api } from '../api/client'
import { useAuth } from '../auth/useAuth'
import type { Message } from '../types'

type Handler = (message: Message) => void

interface ChatConnection {
  /** Unread messages across every conversation, for the nav badge. */
  unreadTotal: number
  refreshUnread: () => Promise<void>
  markConversationRead: (conversationId: string) => Promise<void>
  /** Tell the provider which thread is on screen so it is not counted as unread. */
  setActiveConversation: (conversationId: string | null) => void
  /** Listen for incoming messages. Returns an unsubscribe function. */
  subscribe: (handler: Handler) => () => void
}

const ChatConnectionContext = createContext<ChatConnection | undefined>(undefined)

/**
 * Owns the single SignalR connection for the whole app.
 *
 * It lives here rather than in the chat page because the badge has to update
 * while the member is reading the board — a connection that only exists on the
 * chat page could only notify someone already looking at their messages.
 */
export function ChatConnectionProvider({ children }: { children: ReactNode }) {
  const { me } = useAuth()
  const userId = me?.id ?? null

  const [unreadTotal, setUnreadTotal] = useState(0)
  const handlers = useRef(new Set<Handler>())
  const activeConversation = useRef<string | null>(null)

  const refreshUnread = useCallback(async () => {
    if (!userId) {
      setUnreadTotal(0)
      return
    }
    try {
      const { total } = await api.get<{ total: number }>('/conversations/unread-count')
      setUnreadTotal(total)
    } catch {
      // A failed count must not break the page; the next event or reload retries.
    }
  }, [userId])

  const markConversationRead = useCallback(
    async (conversationId: string) => {
      try {
        await api.post(`/conversations/${conversationId}/read`)
        await refreshUnread()
      } catch {
        /* ignore */
      }
    },
    [refreshUnread],
  )

  const setActiveConversation = useCallback((conversationId: string | null) => {
    activeConversation.current = conversationId
  }, [])

  const subscribe = useCallback((handler: Handler) => {
    handlers.current.add(handler)
    return () => {
      handlers.current.delete(handler)
    }
  }, [])

  useEffect(() => {
    if (!userId) {
      setUnreadTotal(0)
      return
    }

    void refreshUnread()

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(api.base) // the client POSTs to {base}/negotiate
      .withAutomaticReconnect()
      .build()

    connection.on('newMessage', (message: Message) => {
      // A message in the thread already on screen is read on arrival, so it
      // never briefly flashes into the badge.
      if (message.conversationId !== activeConversation.current) {
        setUnreadTotal((n) => n + 1)
      }
      handlers.current.forEach((handler) => handler(message))
    })

    // Offline is survivable: counts still load over HTTP, just not live.
    connection.start().catch(() => {})

    return () => {
      connection.off('newMessage')
      void connection.stop()
    }
  }, [userId, refreshUnread])

  return (
    <ChatConnectionContext.Provider
      value={{ unreadTotal, refreshUnread, markConversationRead, setActiveConversation, subscribe }}
    >
      {children}
    </ChatConnectionContext.Provider>
  )
}

export function useChatConnection() {
  const ctx = useContext(ChatConnectionContext)
  if (!ctx) throw new Error('useChatConnection must be used within ChatConnectionProvider')
  return ctx
}
