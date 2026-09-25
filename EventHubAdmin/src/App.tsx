import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Button, Input, Panel, Spinner, Textarea } from './ui'
import { api, session, setUnauthorizedHandler, type EventFilters, type EventItem, type EventPayload, type Page, type Tag, type TagPayload } from './api'
import { displayDate, fromMoscowInput, toMoscowInput } from './time'
import { AppShell } from './AppShell'
import { routeFor, sectionFromPath, type Section } from './Sidebar'

const initialFilters: EventFilters = { page: 1, pageSize: 10, search: '', tags: [], from: '', to: '', format: '', status: '', tagsConfirmed: '' }
const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'Произошла ошибка.'
const statusText = (status: EventItem['eventStatus']) => status === 1 || status === 'Draft' ? 'Черновик' : status === 0 || status === 'Published' ? 'Опубликовано' : `Статус ${status}`
const published = (event: EventItem) => event.eventStatus === 0 || event.eventStatus === 'Published'
const imageSrc = (value: string) => {
  try {
    const url = new URL(value, window.location.origin)
    return url.pathname.startsWith('/api/media/') ? `${url.pathname}${url.search}` : value
  } catch { return value }
}

function Notice({ text, onClose }: { text: string; onClose?: () => void }) {
  return <div className="notice" role="alert"><span>{text}</span>{onClose && <button type="button" onClick={onClose} aria-label="Закрыть">×</button>}</div>
}

function Loading() { return <div className="loading"><Spinner size={28} /><span>Загружаем данные…</span></div> }

function TagSelector({ tags, selected, onChange }: { tags: Tag[]; selected: string[]; onChange: (ids: string[]) => void }) {
  return <div className="tag-selector">
    {tags.length ? tags.map(tag => <label className={`tag-choice ${selected.includes(tag.id) ? 'selected' : ''}`} key={tag.id}>
      <input type="checkbox" checked={selected.includes(tag.id)} onChange={e => onChange(e.target.checked ? [...selected, tag.id] : selected.filter(id => id !== tag.id))} />{tag.name}
    </label>) : <span className="muted">Тегов пока нет. Создайте их в разделе «Теги».</span>}
  </div>
}

function Login({ onLogin }: { onLogin: () => void }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('')
    try { const result = await api.login(username, password); session.set(result.accessToken, result.expiresAt); onLogin() }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusy(false) }
  }
  return <div className="login-page">
    <div className="login-brand"><div className="brand-mark">E</div><span>EventHub <small>АДМИН</small></span></div>
    <Panel mode="secondary" className="login-panel">
      <div className="eyebrow">ПАНЕЛЬ УПРАВЛЕНИЯ</div>
      <h1>С возвращением</h1>
      <p className="muted">Войдите, чтобы управлять мероприятиями и тегами.</p>
      <form onSubmit={submit} className="form-stack">
        <label>Логин<Input value={username} onChange={e => setUsername(e.target.value)} placeholder="Имя администратора" required autoComplete="username" /></label>
        <label>Пароль<Input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Пароль" required autoComplete="current-password" /></label>
        {error && <Notice text={error} />}
        <Button type="submit" variant="primary" stretched loading={busy}>Войти</Button>
      </form>
    </Panel>
    <div className="login-footer">Панель администратора EventHub</div>
  </div>
}

function EventEditor({ event, tags, onClose, onSaved }: { event: EventItem | null; tags: Tag[]; onClose: () => void; onSaved: (message: string) => void }) {
  const [title, setTitle] = useState(event?.title || '')
  const [description, setDescription] = useState(event?.description || '')
  const [date, setDate] = useState(toMoscowInput(event?.eventDateTime || null))
  const [deadline, setDeadline] = useState(toMoscowInput(event?.deadline || null))
  const [locationText, setLocationText] = useState(event?.location || '')
  const [source, setSource] = useState(event?.source || '')
  const [tagIds, setTagIds] = useState(event?.tagIds || [])
  const [mainImg, setMainImg] = useState<string | null>(event?.mainImg || null)
  const [busy, setBusy] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')

  async function upload(file?: File) {
    if (!file) return
    if (!['image/jpeg', 'image/png', 'image/webp', 'image/gif'].includes(file.type)) { setError('Допустимы JPEG, PNG, WebP и GIF.'); return }
    if (file.size > 10 * 1024 * 1024) { setError('Размер изображения не должен превышать 10 МБ.'); return }
    setUploading(true); setError('')
    try { const image = await api.uploadImage(file); setMainImg(image.url) }
    catch (err) { setError(errorMessage(err)) }
    finally { setUploading(false) }
  }
  async function submit(e: FormEvent) {
    e.preventDefault(); setError('')
    let payload: EventPayload
    try {
      const eventDateTime = fromMoscowInput(date)
      const deadlineUtc = deadline ? fromMoscowInput(deadline) : null
      if (Date.parse(eventDateTime) <= Date.now()) throw new Error('Дата мероприятия должна быть в будущем.')
      if (deadlineUtc && Date.parse(deadlineUtc) > Date.parse(eventDateTime)) throw new Error('Дедлайн не может быть позже начала мероприятия.')
      if (!/^https?:\/\//i.test(source)) throw new Error('Укажите полную ссылку на первоисточник с http:// или https://.')
      payload = { title: title.trim(), description: description.trim(), eventDateTime, location: locationText.trim(), source: source.trim(), deadline: deadlineUtc, tagIds, mainImg }
      if (!payload.title || !payload.description || !payload.location) throw new Error('Заполните название, описание и место.')
    } catch (err) { setError(errorMessage(err)); return }
    setBusy(true)
    try {
      if (event) await api.updateEvent(event.id, payload)
      else await api.createEvent(payload)
      onSaved(event ? 'Мероприятие сохранено.' : 'Мероприятие создано как черновик.')
    } catch (err) { setError(errorMessage(err)) }
    finally { setBusy(false) }
  }
  return <div className="overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}>
    <div className="drawer" role="dialog" aria-modal="true" aria-label={event ? 'Редактирование мероприятия' : 'Новое мероприятие'}>
      <div className="drawer-head"><div><div className="eyebrow">МЕРОПРИЯТИЕ</div><h2>{event ? 'Редактировать' : 'Новое мероприятие'}</h2></div><button className="close" type="button" onClick={onClose} aria-label="Закрыть">×</button></div>
      <form onSubmit={submit} className="drawer-body form-stack">
		<label><span>Название <span className="required">*</span></span><Input value={title} onChange={e => setTitle(e.target.value)} placeholder="Например, лекция о городском искусстве" required /></label>
		<label><span>Описание <span className="required">*</span></span><Textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="О чём мероприятие и кому оно будет интересно" required rows={5} /></label>
		<div className="form-grid"><label><span>Дата и время · МСК <span className="required">*</span></span><input className="native-input" type="datetime-local" value={date} onChange={e => setDate(e.target.value)} required /></label><label><span>Дедлайн · МСК</span><input className="native-input" type="datetime-local" value={deadline} onChange={e => setDeadline(e.target.value)} /></label></div>
		<label><span>Место <span className="required">*</span></span><Input value={locationText} onChange={e => setLocationText(e.target.value)} placeholder="Адрес, площадка или Онлайн" required /></label>
		<label><span>Первоисточник / регистрация <span className="required">*</span></span><Input type="url" value={source} onChange={e => setSource(e.target.value)} placeholder="https://..." required /></label>
        <div className="form-group"><div className="field-title">Теги</div><TagSelector tags={tags} selected={tagIds} onChange={setTagIds} /><p className="hint">Подтверждение тегов выполняется отдельно в очереди проверки.</p></div>
        <div className="form-group"><div className="field-title">Изображение</div>
          {mainImg && <div className="image-preview"><img src={imageSrc(mainImg)} alt="Предпросмотр мероприятия" /><button type="button" onClick={() => setMainImg(null)}>Убрать из мероприятия</button></div>}
          <label className="upload-zone"><input type="file" accept="image/jpeg,image/png,image/webp,image/gif" onChange={e => { void upload(e.target.files?.[0]); e.target.value = '' }} disabled={uploading || busy} /><span>{uploading ? 'Загружаем изображение…' : 'Выбрать изображение'}</span><small>JPEG, PNG, WebP или GIF · до 10 МБ</small></label>
        </div>
        {error && <Notice text={error} onClose={() => setError('')} />}
        <div className="drawer-actions"><Button type="button" variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" variant="primary" loading={busy} disabled={uploading}>{event ? 'Сохранить' : 'Создать черновик'}</Button></div>
      </form>
    </div>
  </div>
}

function EventCard({ event, tags, onEdit, onReview, onAction, busy }: { event: EventItem; tags: Tag[]; onEdit: () => void; onReview: () => void; onAction: (kind: 'publish' | 'unpublish') => void; busy: boolean }) {
  return <Panel mode="secondary" className="event-card">
    {event.mainImg ? <img className="event-image" src={imageSrc(event.mainImg)} alt="" /> : <div className="event-mark" aria-hidden="true">{event.title.trim().slice(0, 2).toLocaleUpperCase() || 'EH'}</div>}
    <div className="event-content"><div className="event-top"><span className={`badge ${published(event) ? 'live' : 'draft'}`}>{statusText(event.eventStatus)}</span><span className={`badge ${event.tagsConfirmed ? 'confirmed' : 'pending'}`}>{event.tagsConfirmed ? 'Теги проверены' : 'Проверить теги'}</span></div>
      <h3>{event.title}</h3><p className="event-description">{event.description}</p>
      <div className="event-meta"><span>◷ &nbsp;{displayDate(event.eventDateTime)}</span><span>⌖ &nbsp;{event.location}</span></div>
      <div className="card-tags">{event.tagIds.length ? event.tagIds.map(id => <span className="mini-tag" key={id}>{tags.find(t => t.id === id)?.name || id.slice(0, 8)}</span>) : <span className="muted">Без тегов</span>}</div>
      <div className="card-actions"><Button size="small" variant="secondary" onClick={onEdit}>Редактировать</Button>{!event.tagsConfirmed && <Button size="small" variant="secondary" onClick={onReview}>Проверить теги</Button>}{published(event) ? <Button size="small" variant="destructive" disabled={busy} onClick={() => onAction('unpublish')}>Снять с публикации</Button> : <Button size="small" variant="primary" disabled={busy} onClick={() => event.tagsConfirmed ? onAction('publish') : onReview()}>Опубликовать</Button>}</div>
    </div>
  </Panel>
}

function EventList({ tags, reviewOnly, refreshTags }: { tags: Tag[]; reviewOnly: boolean; refreshTags: () => void }) {
  const [filters, setFilters] = useState<EventFilters>({ ...initialFilters, tagsConfirmed: reviewOnly ? 'false' : '' })
  const [searchText, setSearchText] = useState('')
  const [page, setPage] = useState<Page<EventItem> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [editing, setEditing] = useState<EventItem | 'new' | null>(null)
  const [reviewing, setReviewing] = useState<EventItem | null>(null)
  const [busyId, setBusyId] = useState('')
  const [revision, setRevision] = useState(0)
  const reload = () => setRevision(v => v + 1)
  const updateFilters = (patch: Partial<EventFilters>) => setFilters(old => ({ ...old, ...patch, page: patch.page ?? 1 }))

  useEffect(() => { const timer = setTimeout(() => updateFilters({ search: searchText }), 350); return () => clearTimeout(timer) }, [searchText])
  useEffect(() => {
    let current = true
    setLoading(true); setError(''); setPage(null)
    api.events(filters).then(result => { if (current) setPage(result) }).catch(err => { if (current) setError(errorMessage(err)) }).finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [filters, revision])

  async function action(event: EventItem, kind: 'publish' | 'unpublish') {
    if (kind === 'publish' && !event.tagsConfirmed) { setReviewing(event); return }
    const question = kind === 'publish' ? `Опубликовать «${event.title}»?` : `Снять «${event.title}» с публикации? Мероприятие останется в черновиках.`
    if (!window.confirm(question)) return
    setBusyId(event.id); setError(''); setMessage('')
    try { if (kind === 'publish') await api.publish(event.id); else await api.unpublish(event.id); setMessage(kind === 'publish' ? 'Мероприятие опубликовано.' : 'Мероприятие переведено в черновик.'); reload() }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusyId('') }
  }

  return <>
    <div className="page-head"><div><h1>{reviewOnly ? 'Проверка тегов' : 'Мероприятия'}</h1><p className="muted">{reviewOnly ? 'События с неподтверждёнными тегами. Проверьте подборку перед публикацией.' : page ? `Мероприятий в EventHub: ${page.totalCount}.` : 'Создавайте, редактируйте и публикуйте события EventHub.'}</p></div>{!reviewOnly && <Button variant="primary" onClick={() => setEditing('new')}>+ Новое мероприятие</Button>}</div>
    {!reviewOnly && <Panel mode="secondary" className="filters">
      <div className="filter-toolbar">
        <label className="filter-search"><span aria-hidden="true" className="search-glyph">⌕</span><span className="sr-only">Поиск мероприятий</span><Input type="search" value={searchText} onChange={e => setSearchText(e.target.value)} placeholder="Поиск мероприятий" className="pl-9" /></label>
        <div className="filter-chips" aria-label="Статус мероприятия">{([['', 'Все'], ['1', 'Черновики'], ['0', 'Опубликованные']] as const).map(([value, label]) => <button key={label} type="button" aria-pressed={filters.status === value} onClick={() => updateFilters({ status: value })} className={`filter-chip ${filters.status === value ? 'active' : ''}`}>{label}</button>)}</div>
      </div>
      <div className="filter-grid">
        <div className="filter-tags"><span>Теги</span><details><summary>{filters.tags.length ? `Выбрано: ${filters.tags.length}` : 'Все теги'}</summary><div className="filter-tags-options"><TagSelector tags={tags} selected={filters.tags} onChange={ids => updateFilters({ tags: ids })} /></div></details></div>
        <label>Проверка тегов<select value={filters.tagsConfirmed} onChange={e => updateFilters({ tagsConfirmed: e.target.value })}><option value="">Все</option><option value="false">Ожидают проверки</option><option value="true">Проверены</option></select></label>
        <label>Формат<select value={filters.format} onChange={e => updateFilters({ format: e.target.value })}><option value="">Любой</option><option value="online">Онлайн</option><option value="offline">Офлайн</option></select></label>
        <label>С даты<input type="date" value={filters.from} onChange={e => updateFilters({ from: e.target.value })} /></label>
        <label>По дату<input type="date" value={filters.to} onChange={e => updateFilters({ to: e.target.value })} /></label>
      </div>
    </Panel>}
    {reviewOnly && <Panel mode="secondary" className="review-intro"><span className="intro-icon">✓</span><div><strong>Как работает проверка</strong><p>Выберите подходящие теги, подтвердите их отдельным действием, затем опубликуйте мероприятие.</p></div></Panel>}
    {message && <div className="success" role="status">{message}<button type="button" onClick={() => setMessage('')}>×</button></div>}
    {error && <Notice text={error} onClose={() => setError('')} />}
    <div className="list-heading"><h2>{reviewOnly ? 'Ожидают проверки' : 'Все мероприятия'}</h2><span>{page ? `${page.totalCount} всего` : ''}</span></div>
    {loading ? <Loading /> : page?.items.length ? <div className="event-list">{page.items.map(event => <EventCard key={event.id} event={event} tags={tags} busy={busyId === event.id} onEdit={() => setEditing(event)} onReview={() => setReviewing(event)} onAction={kind => void action(event, kind)} />)}</div> : <div className="empty"><div className="empty-icon">⌁</div><h3>{reviewOnly ? 'Очередь пуста' : 'Мероприятий не найдено'}</h3><p>{reviewOnly ? 'Сейчас нет событий, которым нужна проверка тегов.' : 'Измените фильтры или создайте новое мероприятие.'}</p></div>}
    {page && page.totalCount > page.pageSize && <div className="pagination"><Button variant="secondary" disabled={filters.page <= 1 || loading} onClick={() => updateFilters({ page: filters.page - 1 })}>← Назад</Button><span>Страница {page.page} из {Math.ceil(page.totalCount / page.pageSize)}</span><Button variant="secondary" disabled={!page.hasNextPage || loading} onClick={() => updateFilters({ page: filters.page + 1 })}>Вперёд →</Button></div>}
    {editing && <EventEditor key={editing === 'new' ? 'new' : editing.id} event={editing === 'new' ? null : editing} tags={tags} onClose={() => setEditing(null)} onSaved={text => { setEditing(null); setMessage(text); reload(); refreshTags() }} />}
    {reviewing && <ReviewDialog key={reviewing.id} event={reviewing} tags={tags} onClose={() => setReviewing(null)} onSaved={() => { setReviewing(null); setMessage('Теги подтверждены. Мероприятие можно публиковать.'); reload() }} />}
  </>
}

function ReviewDialog({ event, tags, onClose, onSaved }: { event: EventItem; tags: Tag[]; onClose: () => void; onSaved: () => void }) {
  const [ids, setIds] = useState(event.tagIds)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function confirm() {
    setBusy(true); setError('')
    try { await api.confirmTags(event.id, ids); onSaved() }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusy(false) }
  }
  return <div className="overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}><Panel mode="secondary" className="dialog" role="dialog" aria-modal="true" aria-label="Проверка тегов"><div className="eyebrow">ПРОВЕРКА ТЕГОВ</div><h2>{event.title}</h2><p className="muted">Проверьте предложенные теги. После подтверждения мероприятие можно опубликовать.</p><TagSelector tags={tags} selected={ids} onChange={setIds} />{error && <Notice text={error} />}<div className="dialog-actions"><Button variant="secondary" onClick={onClose}>Отмена</Button><Button variant="primary" loading={busy} onClick={() => void confirm()}>Подтвердить теги</Button></div></Panel></div>
}

function TagEditor({ tag, onClose, onSaved }: { tag: Tag | null; onClose: () => void; onSaved: (message: string) => void }) {
  const [name, setName] = useState(tag?.name || '')
  const [description, setDescription] = useState(tag?.description || '')
  const [examples, setExamples] = useState(tag?.examples.length ? tag.examples : [''])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const changeExample = (index: number, value: string) => setExamples(old => old.map((item, i) => i === index ? value : item))
  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('')
    const payload: TagPayload = { name: name.trim(), description: description.trim(), examples: examples.map(x => x.trim()).filter(Boolean) }
    try { if (tag) await api.updateTag(tag.id, payload); else await api.createTag(payload); onSaved(tag ? 'Тег сохранён.' : 'Тег создан.') }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusy(false) }
  }
  return <div className="overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}><div className="drawer" role="dialog" aria-modal="true" aria-label={tag ? 'Редактирование тега' : 'Новый тег'}><div className="drawer-head"><div><div className="eyebrow">ТАКСОНОМИЯ</div><h2>{tag ? 'Редактировать тег' : 'Новый тег'}</h2></div><button className="close" type="button" onClick={onClose} aria-label="Закрыть">×</button></div><form className="drawer-body form-stack" onSubmit={submit}><label><span>Название <span className="required">*</span></span><Input value={name} onChange={e => setName(e.target.value)} required placeholder="Например, Искусство" /></label><label><span>Описание <span className="required">*</span></span><Textarea value={description} onChange={e => setDescription(e.target.value)} required rows={4} placeholder="Опишите, какие события относятся к тегу" /></label><div className="form-group"><div className="field-title">Примеры для модели</div><p className="hint">Добавьте несколько содержательных примеров, по которым модель сможет распознать тему.</p>{examples.map((example, index) => <div className="example-row" key={index}><Textarea value={example} onChange={e => changeExample(index, e.target.value)} rows={2} placeholder={`Пример ${index + 1}`} /><button type="button" onClick={() => setExamples(old => old.filter((_, i) => i !== index))} aria-label="Удалить пример">×</button></div>)}<Button type="button" variant="secondary" onClick={() => setExamples(old => [...old, ''])}>+ Добавить пример</Button></div>{error && <Notice text={error} />}<div className="drawer-actions"><Button type="button" variant="secondary" onClick={onClose}>Отмена</Button><Button type="submit" variant="primary" loading={busy}>Сохранить тег</Button></div></form></div></div>
}

function TagsPage({ tags, loading, error, reload }: { tags: Tag[]; loading: boolean; error: string; reload: () => void }) {
  const [editing, setEditing] = useState<Tag | 'new' | null>(null)
  const [message, setMessage] = useState('')
  const [actionError, setActionError] = useState('')
  const [busyId, setBusyId] = useState('')
  async function remove(tag: Tag) {
    if (!window.confirm(`Удалить тег «${tag.name}»?`)) return
    setBusyId(tag.id); setActionError(''); setMessage('')
    try { await api.deleteTag(tag.id); setMessage('Тег удалён.'); reload() }
    catch (err) { setActionError(errorMessage(err)) }
    finally { setBusyId('') }
  }
  return <><div className="page-head"><div><h1>Теги</h1><p className="muted">Темы, по которым EventHub подбирает и группирует мероприятия.</p></div><Button variant="primary" onClick={() => setEditing('new')}>+ Новый тег</Button></div>{message && <div className="success" role="status">{message}</div>}{(error || actionError) && <Notice text={error || actionError} onClose={() => setActionError('')} />}{loading ? <Loading /> : tags.length ? <div className="tag-grid">{tags.map(tag => <Panel mode="secondary" className="tag-card" key={tag.id}><div className="tag-card-icon">#</div><h3>{tag.name}</h3><p>{tag.description}</p><div className="tag-count">{tag.examples.length} {tag.examples.length === 1 ? 'пример' : 'примеров'}</div><div className="tag-actions"><Button size="small" variant="secondary" onClick={() => setEditing(tag)}>Редактировать</Button><Button size="small" variant="destructive" disabled={busyId === tag.id} onClick={() => void remove(tag)}>Удалить</Button></div></Panel>)}</div> : <div className="empty"><div className="empty-icon">#</div><h3>Тегов пока нет</h3><p>Создайте первый тег для классификации мероприятий.</p></div>}{editing && <TagEditor key={editing === 'new' ? 'new' : editing.id} tag={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={text => { setEditing(null); setMessage(text); reload() }} />}</>
}

function ImportsPage() { return <><div className="page-head"><div><h1>Импорт</h1><p className="muted">Мероприятия из внешних источников появятся здесь после подключения импортёра.</p></div></div><Panel mode="secondary" className="import-panel"><div className="import-visual">↗</div><span className="badge draft">Скоро</span><h2>Импортёр ещё не подключён</h2><p>Эндпоинт импорта пока отвечает 501. Когда он станет доступен, события с неподтверждёнными тегами будут попадать в очередь проверки.</p><div className="import-flow"><span>Внешний источник</span><b>→</b><span>Импорт</span><b>→</b><span>Проверка тегов</span><b>→</b><span>Публикация</span></div></Panel></> }

export default function App() {
  const [loggedIn, setLoggedIn] = useState(Boolean(session.token))
  const [section, setSection] = useState<Section>(sectionFromPath)
  const [tags, setTags] = useState<Tag[]>([])
  const [tagsLoading, setTagsLoading] = useState(false)
  const [tagsError, setTagsError] = useState('')
  const [tagsRevision, setTagsRevision] = useState(0)
  const reloadTags = useCallback(() => setTagsRevision(v => v + 1), [])
  useEffect(() => setUnauthorizedHandler(() => setLoggedIn(false)), [])
  useEffect(() => { const handler = () => setSection(sectionFromPath()); window.addEventListener('popstate', handler); return () => window.removeEventListener('popstate', handler) }, [])
  useEffect(() => {
    if (!loggedIn) return
    let current = true
    setTagsLoading(true); setTagsError('')
    api.tags().then(result => { if (current) setTags(result) }).catch(err => { if (current) setTagsError(errorMessage(err)) }).finally(() => { if (current) setTagsLoading(false) })
    return () => { current = false }
  }, [loggedIn, tagsRevision])
  function navigate(next: Section) { history.pushState({}, '', routeFor(next)); setSection(next); window.scrollTo(0, 0) }
  function logout() { session.clear(); setLoggedIn(false) }
  if (!loggedIn) return <Login onLogin={() => setLoggedIn(true)} />
  return <AppShell active={section} navigate={navigate} logout={logout}>
    {section === 'events' && <EventList key="events" tags={tags} reviewOnly={false} refreshTags={reloadTags} />}
    {section === 'review' && <EventList key="review" tags={tags} reviewOnly refreshTags={reloadTags} />}
    {section === 'tags' && <TagsPage tags={tags} loading={tagsLoading} error={tagsError} reload={reloadTags} />}
    {section === 'imports' && <ImportsPage />}
  </AppShell>
}

