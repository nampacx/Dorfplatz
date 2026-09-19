export type Role = 'user' | 'moderator' | 'admin'

/**
 * Whether an admin has let this account onto the board. 'Rejected' was never
 * let in; 'Suspended' had access and an admin took it away again.
 */
export type AccessStatus = 'Pending' | 'Approved' | 'Rejected' | 'Suspended'

export interface Me {
  id: string
  email: string | null
  displayName: string | null
  roles: Role[]
  hasConsented: boolean
  status: AccessStatus
  /** Why access was suspended, when it was. Shown to the member. */
  statusReason: string | null
}

/** Fixed list, mirrored by the EventCategory enum in the API. */
export const EVENT_CATEGORIES = ['Verwaltung', 'Vereine', 'Kirchen', 'Stadt', 'Sonstiges'] as const
export type EventCategory = (typeof EVENT_CATEGORIES)[number]

export interface EventPost {
  id: string
  title: string
  description: string
  eventDate: string | null
  endDate: string | null
  location: string | null
  category: EventCategory
  status: 'Pending' | 'Published' | 'Rejected'
  rejectionReason: string | null
  authorName?: string
  createdAt?: string
  imageUrl?: string | null
  imageBlobName?: string | null
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

/** Unread messages across every conversation, for the nav badge. */
export interface UnreadCount {
  total: number
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
  status: AccessStatus
  createdAt: string
  decidedAt: string | null
  statusReason: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export type AdminUserPage = PagedResult<AdminUser>

/** What an admin can do to a member's access. Mirrors AccessDecisions in the API. */
export type AccessDecision = 'approve' | 'reject' | 'suspend'
