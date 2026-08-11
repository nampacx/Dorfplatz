export type Role = 'user' | 'moderator' | 'admin'

export interface Me {
  id: string
  email: string | null
  displayName: string | null
  roles: Role[]
  hasConsented: boolean
}

/** Fixed list, mirrored by the EventCategory enum in the API. */
export const EVENT_CATEGORIES = ['Verwaltung', 'Vereine', 'Kirchen', 'Stadt', 'Sonstiges'] as const
export type EventCategory = (typeof EVENT_CATEGORIES)[number]

export interface EventPost {
  id: string
  title: string
  description: string
  eventDate: string | null
  location: string | null
  category: EventCategory
  status: 'Pending' | 'Published' | 'Rejected'
  rejectionReason: string | null
  authorName: string
  createdAt: string
  /** True when the signed-in viewer may edit, resubmit or delete this event. */
  canEdit: boolean
}

export interface ServicePost {
  id: string
  type: 'Seeking' | 'Offering'
  title: string
  description: string
  category: string | null
  status: 'Active' | 'Closed'
  authorName: string
  authorId: string
  createdAt: string
  /** True when the signed-in viewer may edit or delete this post. */
  canEdit: boolean
}

export interface GoodsImage {
  id: string
  url: string
}

export interface GoodsPost {
  id: string
  title: string
  description: string
  price: number
  currency: string
  condition: string
  category: string | null
  location: string | null
  status: string
  authorName: string
  authorId: string
  images: GoodsImage[]
  createdAt: string
  /** True when the signed-in viewer may edit or delete this listing. */
  canEdit: boolean
}

export interface UploadSlot {
  blobName: string
  uploadUrl: string
}

export interface Conversation {
  id: string
  listingType: string | null
  listingId: string | null
  otherUserId: string
  otherUserName: string
  lastMessageAt: string
  /** Messages from the other person that this viewer has not read yet. */
  unreadCount: number
}

export interface Message {
  id: string
  conversationId: string
  senderId: string
  senderName: string
  body: string
  sentAt: string
  isMine: boolean
}

export interface AdminUser {
  id: string
  email: string | null
  displayName: string | null
  roles: Role[]
  createdAt: string
}
