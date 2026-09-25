export interface EventResponse {
  id: string;
  title: string;
  description: string;
  eventDateTime: string;
  location: string;
  source: string;
  mainImg: string | null;
  deadline: string | null;
  eventStatus: number | string;
  tagsConfirmed: boolean;
  tagIds: string[];
  createdAt: string;
  updatedAt: string;
}

export interface PagedEvents {
  items: EventResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
}

export interface TagResponse {
  id: string;
  name: string;
  description: string;
  examples: string[];
}

export interface EventFilters {
  search?: string;
  from?: string;
  to?: string;
  format?: string;
  tags?: string[];
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL || '').replace(/\/$/, '');

async function requestJson<T>(path: string, signal?: AbortSignal, options?: { method?: string; body?: unknown; token?: string }): Promise<T> {
  const request = new AbortController();
  const onAbort = () => request.abort();
  signal?.addEventListener('abort', onAbort, { once: true });
  if (signal?.aborted) request.abort();
  const timeout = window.setTimeout(() => request.abort(), 10000);
  try {
    const response = await fetch(`${baseUrl}${path}`, {
      signal: request.signal,
      method: options?.method || 'GET',
      body: options?.body === undefined ? undefined : JSON.stringify(options.body),
      headers: {
        Accept: 'application/json',
        ...(options?.body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...(options?.token ? { Authorization: `Bearer ${options.token}` } : {}),
      },
    });
    if (!response.ok) throw new Error(`API вернул ${response.status}`);
    if (response.status === 204) return undefined as T;
    const body = await response.text();
    return body ? JSON.parse(body) as T : undefined as T;
  } catch (error) {
    if (request.signal.aborted && !signal?.aborted) throw new Error('Сервер не отвечает');
    throw error;
  } finally {
    window.clearTimeout(timeout);
    signal?.removeEventListener('abort', onAbort);
  }
}

function getJson<T>(path: string, signal?: AbortSignal) {
  return requestJson<T>(path, signal);
}

export interface TokenResponse { accessToken: string; expiresAt: string }

export function loginWithMax(initData: string, signal?: AbortSignal) {
  return requestJson<TokenResponse>('/api/auth/max', signal, { method: 'POST', body: { initData } });
}

export function getMe(token: string, signal?: AbortSignal) {
  return requestJson<unknown>('/api/me', signal, { token });
}

export function saveInterests(tagIds: string[], token: string, signal?: AbortSignal) {
  return requestJson<unknown>('/api/me/interests', signal, { method: 'PUT', body: { tagIds }, token });
}

export function getRecommendations(token: string, signal?: AbortSignal) {
  return requestJson<EventResponse[]>('/api/me/recommendations?limit=3', signal, { token });
}

export function getEvents(page: number, filters: EventFilters, signal?: AbortSignal) {
  const query = new URLSearchParams({ Page: String(page), PageSize: '20' });
  if (filters.search) query.set('Search', filters.search);
  if (filters.from) query.set('From', filters.from);
  if (filters.to) query.set('To', filters.to);
  if (filters.format) query.set('Format', filters.format);
  filters.tags?.forEach(tag => query.append('Tags', tag));
  return getJson<PagedEvents>(`/api/events?${query}`, signal);
}

export function getEvent(id: string, signal?: AbortSignal) {
  return getJson<EventResponse>(`/api/events/${encodeURIComponent(id)}`, signal);
}

export function getTags(signal?: AbortSignal) {
  return getJson<TagResponse[]>('/api/tags', signal);
}

export function getTag(id: string, signal?: AbortSignal) {
  return getJson<TagResponse>(`/api/tags/${encodeURIComponent(id)}`, signal);
}

export function mediaUrl(objectKey: string) {
  if (/^https?:\/\//i.test(objectKey)) {
    const url = new URL(objectKey);
    if (url.pathname.startsWith('/api/media/')) return `${baseUrl}${url.pathname}${url.search}`;
    return objectKey;
  }
  return `${baseUrl}/api/media/${objectKey.split('/').map(encodeURIComponent).join('/')}`;
}
