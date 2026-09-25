export type EventStatus = number | 'Draft' | 'Published'
export interface EventItem {
  id: string
  title: string
  description: string
  eventDateTime: string
  location: string
  source: string
  mainImg: string | null
  deadline: string | null
  eventStatus: EventStatus
  tagsConfirmed: boolean
  tagIds: string[]
  createdAt: string
  updatedAt: string
}
export type EventPayload = Pick<EventItem, 'title' | 'description' | 'eventDateTime' | 'location' | 'source' | 'deadline' | 'tagIds' | 'mainImg'>
export interface Tag { id: string; name: string; description: string; examples: string[] }
export type TagPayload = Omit<Tag, 'id'>
export interface Page<T> { items: T[]; page: number; pageSize: number; totalCount: number; hasNextPage: boolean }
export interface EventFilters {
  page: number; pageSize: number; search: string; tags: string[]; from: string; to: string;
  format: string; status: string; tagsConfirmed: string
}

const TOKEN_KEY = 'eventhub.admin.token'
const EXPIRY_KEY = 'eventhub.admin.expiresAt'
export const session = {
  get token() {
    const expires = sessionStorage.getItem(EXPIRY_KEY)
    if (expires && Date.parse(expires) <= Date.now()) this.clear()
    return sessionStorage.getItem(TOKEN_KEY)
  },
  set(token: string, expiresAt: string) {
    sessionStorage.setItem(TOKEN_KEY, token)
    sessionStorage.setItem(EXPIRY_KEY, expiresAt)
  },
  clear() {
    sessionStorage.removeItem(TOKEN_KEY)
    sessionStorage.removeItem(EXPIRY_KEY)
  },
}

export class ApiError extends Error {
  constructor(message: string, public status: number) { super(message) }
}

let onUnauthorized: (() => void) | undefined
export function setUnauthorizedHandler(handler: () => void) { onUnauthorized = handler }

async function request<T>(path: string, options: RequestInit = {}, auth = true): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (auth && session.token) headers.set('Authorization', `Bearer ${session.token}`)
  let response: Response
  try {
    response = await fetch(path, { ...options, headers })
  } catch {
    throw new ApiError('Не удалось связаться с сервером. Проверьте соединение и повторите попытку.', 0)
  }
  if (response.status === 401 && auth) {
    session.clear()
    onUnauthorized?.()
    throw new ApiError('Сессия завершилась. Войдите снова.', 401)
  }
  const raw = await response.text()
  let data: unknown
  try { data = raw ? JSON.parse(raw) : undefined } catch { data = raw }
  if (!response.ok) {
    const problem = data && typeof data === 'object' ? data as { detail?: string; title?: string } : undefined
    throw new ApiError(problem?.detail || problem?.title || (typeof data === 'string' && data) || `Ошибка сервера (${response.status})`, response.status)
  }
  return data as T
}
const json = (value: unknown) => JSON.stringify(value)
const idPath = (id: string) => encodeURIComponent(id)

export const api = {
  login: (username: string, password: string) => request<{ accessToken: string; expiresAt: string }>('/api/admin/auth/login', { method: 'POST', body: json({ username, password }) }, false),
  events: (filters: EventFilters) => {
    const query = new URLSearchParams({ Page: String(filters.page), PageSize: String(filters.pageSize) })
    if (filters.search.trim()) query.set('Search', filters.search.trim())
    for (const tag of filters.tags) query.append('Tags', tag)
    if (filters.from) query.set('From', filters.from)
    if (filters.to) query.set('To', filters.to)
    if (filters.format) query.set('Format', filters.format)
    if (filters.status) query.set('Status', filters.status)
    if (filters.tagsConfirmed) query.set('TagsConfirmed', filters.tagsConfirmed)
    return request<Page<EventItem>>(`/api/admin/events?${query}`)
  },
  event: (id: string) => request<EventItem>(`/api/admin/events/${idPath(id)}`),
  createEvent: (body: EventPayload) => request<EventItem>('/api/admin/events', { method: 'POST', body: json(body) }),
  updateEvent: (id: string, body: EventPayload) => request<EventItem>(`/api/admin/events/${idPath(id)}`, { method: 'PUT', body: json(body) }),
  confirmTags: (id: string, tagIds: string[]) => request<EventItem>(`/api/admin/events/${idPath(id)}/tags`, { method: 'PUT', body: json({ tagIds }) }),
  publish: (id: string) => request<EventItem>(`/api/admin/events/${idPath(id)}/publish`, { method: 'POST' }),
  unpublish: (id: string) => request<void>(`/api/admin/events/${idPath(id)}/unpublish`, { method: 'POST' }),
  tags: () => request<Tag[]>('/api/tags'),
  createTag: (body: TagPayload) => request<Tag>('/api/admin/tags', { method: 'POST', body: json(body) }),
  updateTag: (id: string, body: TagPayload) => request<Tag>(`/api/admin/tags/${idPath(id)}`, { method: 'PUT', body: json(body) }),
  deleteTag: (id: string) => request<void>(`/api/admin/tags/${idPath(id)}`, { method: 'DELETE' }),
  uploadImage: (file: File) => {
    const body = new FormData()
    body.append('file', file)
    return request<{ objectKey: string; url: string }>('/api/admin/images', { method: 'POST', body })
  },
}
