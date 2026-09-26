import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Alert, Badge, Button, Chip, ConfirmDialog, EmptyState, FormField, Icon, IconButton, Input, LoadingState, Modal, ModalCancel, Pagination, Panel, Select, Textarea } from './ui'
import { CalendarDays, ChevronDown, Clock3, ExternalLink, ListChecks, MapPin, Plus, Search, Tags, Trash2 } from 'lucide-react'
import { api, session, setUnauthorizedHandler, type EventFilters, type EventItem, type EventPayload, type Page, type Tag, type TagPayload } from './api'
import { displayDate, fromMoscowInput, toMoscowInput } from './time'
import { TagSelector } from './TagSelector'
import { ImportsPage } from './ImportsPage'
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

const Notice = Alert
const Loading = LoadingState

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
        <FormField label="Логин"><Input value={username} onChange={e => setUsername(e.target.value)} placeholder="Имя администратора" required autoComplete="username" /></FormField>
        <FormField label="Пароль"><Input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Пароль" required autoComplete="current-password" /></FormField>
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

  const dirty = JSON.stringify([title, description, date, deadline, locationText, source, [...tagIds].sort(), mainImg]) !== JSON.stringify([event?.title || '', event?.description || '', toMoscowInput(event?.eventDateTime || null), toMoscowInput(event?.deadline || null), event?.location || '', event?.source || '', [...(event?.tagIds || [])].sort(), event?.mainImg || null])
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

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
    e.preventDefault(); setError(''); setFieldErrors({})
    let payload: EventPayload
    try {
      const eventDateTime = fromMoscowInput(date)
      const deadlineUtc = deadline ? fromMoscowInput(deadline) : null
      if (Date.parse(eventDateTime) <= Date.now()) { setFieldErrors({ date: 'Дата мероприятия должна быть в будущем.' }); e.currentTarget.querySelector<HTMLInputElement>('[name=eventDate]')?.focus(); return }
      if (deadlineUtc && Date.parse(deadlineUtc) > Date.parse(eventDateTime)) { setFieldErrors({ deadline: 'Дедлайн не может быть позже начала мероприятия.' }); e.currentTarget.querySelector<HTMLInputElement>('[name=deadline]')?.focus(); return }
      if (!/^https?:\/\//i.test(source.trim())) { setFieldErrors({ source: 'Укажите ссылку с http:// или https://.' }); e.currentTarget.querySelector<HTMLInputElement>('[name=source]')?.focus(); return }
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
  return <Modal drawer label={event ? 'Редактирование мероприятия' : 'Новое мероприятие'} dirty={dirty} busy={busy || uploading} onClose={onClose}>
      <div className="drawer-head"><div><div className="eyebrow">МЕРОПРИЯТИЕ</div><h2>{event ? 'Редактировать' : 'Новое мероприятие'}</h2></div></div>
      <form onSubmit={submit} className="drawer-body"><fieldset disabled={busy || uploading} className="form-stack">
		<FormField label="Название"><Input value={title} onChange={e => setTitle(e.target.value)} placeholder="Например, лекция о городском искусстве" required /></FormField>
		<FormField label="Описание"><Textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="О чём мероприятие и кому оно будет интересно" required rows={5} /></FormField>
		<div className="form-grid"><FormField label="Дата и время · МСК" error={fieldErrors.date}><Input name="eventDate" type="datetime-local" value={date} onChange={e => { setDate(e.target.value); setFieldErrors({}) }} required /></FormField><FormField label="Дедлайн · МСК" error={fieldErrors.deadline}><Input name="deadline" type="datetime-local" value={deadline} onChange={e => { setDeadline(e.target.value); setFieldErrors({}) }} /></FormField></div>
		<FormField label="Место"><Input value={locationText} onChange={e => setLocationText(e.target.value)} placeholder="Адрес, площадка или Онлайн" required /></FormField>
		<FormField label="Первоисточник / регистрация" error={fieldErrors.source}><Input name="source" type="url" value={source} onChange={e => setSource(e.target.value)} placeholder="https://..." required /></FormField>
        <div className="form-group"><div className="field-title">Теги</div><TagSelector tags={tags} selected={tagIds} onChange={setTagIds} /><p className="hint">Подтверждение тегов выполняется отдельно в очереди проверки.</p></div>
        <div className="form-group"><div className="field-title">Изображение</div>
          {mainImg && <div className="image-preview"><img src={imageSrc(mainImg)} alt="Предпросмотр мероприятия" /><button type="button" onClick={() => setMainImg(null)}>Убрать из мероприятия</button></div>}
          <label className="upload-zone"><input type="file" accept="image/jpeg,image/png,image/webp,image/gif" onChange={e => { void upload(e.target.files?.[0]); e.target.value = '' }} disabled={uploading || busy} /><span>{uploading ? 'Загружаем изображение…' : 'Выбрать изображение'}</span><small>JPEG, PNG, WebP или GIF · до 10 МБ</small></label>
        </div>
        {error && <Notice text={error} onClose={() => setError('')} />}
        <div className="drawer-actions"><ModalCancel disabled={busy || uploading} /><Button type="submit" variant="primary" loading={busy} disabled={uploading}>{event ? 'Сохранить' : 'Создать черновик'}</Button></div>
      </fieldset></form>
  </Modal>
}

function EventCard({ event, tags, onEdit, onReview, onAction, busy, disabled }: { event: EventItem; tags: Tag[]; onEdit: () => void; onReview: () => void; onAction: (kind: 'publish' | 'unpublish') => void; busy: boolean; disabled: boolean }) {
  return <Panel mode="secondary" className="event-card">
    {event.mainImg ? <img className="event-image" src={imageSrc(event.mainImg)} alt="" /> : <div className="event-mark" aria-hidden="true">{event.title.trim().slice(0, 2).toLocaleUpperCase() || 'EH'}</div>}
    <div className="event-content"><div className="event-top"><Badge tone={published(event) ? 'live' : 'draft'}>{statusText(event.eventStatus)}</Badge><Badge tone={event.tagsConfirmed ? 'confirmed' : 'pending'}>{event.tagsConfirmed ? 'Теги проверены' : 'Проверить теги'}</Badge></div>
      <h3>{event.title}</h3><p className="event-description">{event.description}</p>
      <div className="event-meta"><span><Icon icon={Clock3} size={16} />{displayDate(event.eventDateTime)}</span><span><Icon icon={MapPin} size={16} />{event.location}</span></div>
      <div className="card-tags">{event.tagIds.length ? event.tagIds.map(id => <span className="mini-tag" key={id}>{tags.find(t => t.id === id)?.name || id.slice(0, 8)}</span>) : <span className="muted">Без тегов</span>}</div>
      <div className="card-actions"><Button size="small" variant="secondary" disabled={disabled} onClick={onEdit}>Редактировать</Button>{!event.tagsConfirmed && <Button size="small" variant="secondary" disabled={disabled} onClick={onReview}>Проверить теги</Button>}{published(event) ? <Button size="small" variant="destructive" disabled={disabled} loading={busy} onClick={() => onAction('unpublish')}>Снять с публикации</Button> : <Button size="small" variant="primary" disabled={disabled} loading={busy} onClick={() => event.tagsConfirmed ? onAction('publish') : onReview()}>Опубликовать</Button>}</div>
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
  const [fetchFailed, setFetchFailed] = useState(false)
  const [pendingAction, setPendingAction] = useState<{ event: EventItem; kind: 'publish' | 'unpublish' } | null>(null)
  const actionLock = useRef(false)
  const invalidRange = Boolean(filters.from && filters.to && filters.from > filters.to)
  const reload = () => setRevision(v => v + 1)
  const updateFilters = (patch: Partial<EventFilters>) => setFilters(old => ({ ...old, ...patch, page: patch.page ?? 1 }))

  useEffect(() => { const timer = setTimeout(() => updateFilters({ search: searchText }), 350); return () => clearTimeout(timer) }, [searchText])
  useEffect(() => {
    let current = true
    if (invalidRange) { setLoading(false); return }
    setLoading(true); setError(''); setFetchFailed(false)
    api.events(filters).then(result => { if (current) setPage(result) }).catch(err => { if (current) { setError(errorMessage(err)); setFetchFailed(true) } }).finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [filters, revision, invalidRange])

  async function action(event: EventItem, kind: 'publish' | 'unpublish') {
    if (kind === 'publish' && !event.tagsConfirmed) { setReviewing(event); return }
    if (actionLock.current) return
    actionLock.current = true
    setBusyId(event.id); setError(''); setMessage('')
    try { if (kind === 'publish') await api.publish(event.id); else await api.unpublish(event.id); setMessage(kind === 'publish' ? 'Мероприятие опубликовано.' : 'Мероприятие переведено в черновик.'); reload() }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusyId(''); actionLock.current = false; setPendingAction(null) }
  }

  return <>
    <div className="page-head"><div><h1>{reviewOnly ? 'Проверка тегов' : 'Мероприятия'}</h1><p className="muted">{reviewOnly ? 'События с неподтверждёнными тегами. Проверьте подборку перед публикацией.' : page ? `Найдено мероприятий: ${page.totalCount}.` : 'Создавайте, редактируйте и публикуйте события EventHub.'}</p></div>{!reviewOnly && <Button icon={Plus} variant="primary" onClick={() => setEditing('new')}>Новое мероприятие</Button>}</div>
    {!reviewOnly && <Panel mode="secondary" className="filters">
      <div className="filter-toolbar">
        <label className="filter-search"><Icon icon={Search} className="search-glyph" size={18} /><span className="sr-only">Поиск мероприятий</span><Input type="search" value={searchText} onChange={e => setSearchText(e.target.value)} placeholder="Поиск мероприятий" className="search-input" /></label>
        <div className="filter-chips" aria-label="Статус мероприятия">{([['', 'Все'], ['1', 'Черновики'], ['0', 'Опубликованные']] as const).map(([value, label]) => <Chip key={label} active={filters.status === value} onClick={() => updateFilters({ status: value })}>{label}</Chip>)}</div>
      </div>
      <div className="filter-grid">
        <div className="filter-tags"><span>Теги</span><details><summary>{filters.tags.length ? `Выбрано: ${filters.tags.length}` : 'Все теги'}<Icon icon={ChevronDown} size={16} /></summary><div className="filter-tags-options"><TagSelector tags={tags} selected={filters.tags} onChange={ids => updateFilters({ tags: ids })} /></div></details></div>
        <div className="filter-field"><span>Проверка тегов</span><Select label="Проверка тегов" value={filters.tagsConfirmed} onValueChange={value => updateFilters({ tagsConfirmed: value })}><option value="">Все</option><option value="false">Ожидают проверки</option><option value="true">Проверены</option></Select></div>
        <div className="filter-field"><span>Формат</span><Select label="Формат" value={filters.format} onValueChange={value => updateFilters({ format: value })}><option value="">Любой</option><option value="online">Онлайн</option><option value="offline">Офлайн</option></Select></div>
        <label>С даты<Input type="date" value={filters.from} onChange={e => updateFilters({ from: e.target.value })} /></label>
        <label>По дату<Input type="date" value={filters.to} onChange={e => updateFilters({ to: e.target.value })} /></label>
      </div>
      {invalidRange && <Alert text="Начало периода не может быть позже его окончания." />}
      <Button variant="ghost" size="small" onClick={() => { setSearchText(''); setFilters({ ...initialFilters }) }}>Сбросить фильтры</Button>
    </Panel>}
    {reviewOnly && <Panel mode="secondary" className="review-intro"><span className="intro-icon"><Icon icon={ListChecks} /></span><div><strong>Как работает проверка</strong><p>Выберите подходящие теги, подтвердите их отдельным действием, затем опубликуйте мероприятие.</p></div></Panel>}
    {message && <Alert text={message} success onClose={() => setMessage('')} />}
    {error && <Notice text={error} onRetry={reload} />}
    {fetchFailed && page && <p className="hint">Показан предыдущий результат. Повторите загрузку, чтобы получить актуальный список.</p>}
    <div className="list-heading"><h2>{reviewOnly ? 'Ожидают проверки' : 'Все мероприятия'}</h2><span>{page ? `${page.totalCount} всего` : ''}</span></div>
    {loading && page && <Loading compact />}
    {loading && !page ? <Loading /> : page?.items.length ? <div className={`event-list ${loading || invalidRange || fetchFailed ? 'is-refreshing' : ''}`} aria-busy={loading}>{page.items.map(event => <EventCard key={event.id} event={event} tags={tags} busy={busyId === event.id} disabled={Boolean(busyId) || loading || invalidRange || fetchFailed} onEdit={() => setEditing(event)} onReview={() => setReviewing(event)} onAction={kind => setPendingAction({ event, kind })} />)}</div> : !error && !invalidRange && <EmptyState title={reviewOnly ? 'Очередь пуста' : 'Мероприятий не найдено'} text={reviewOnly ? 'Сейчас нет событий, которым нужна проверка тегов.' : 'Измените фильтры или создайте новое мероприятие.'} icon={reviewOnly ? ListChecks : CalendarDays} />}
    {page && page.totalCount > page.pageSize && <Pagination page={page.page} totalPages={Math.ceil(page.totalCount / page.pageSize)} hasNext={page.hasNextPage} disabled={loading || invalidRange || fetchFailed} onChange={value => updateFilters({ page: value })} />}
    {pendingAction && <ConfirmDialog title={pendingAction.kind === 'publish' ? 'Опубликовать мероприятие?' : 'Снять с публикации?'} text={`«${pendingAction.event.title}». ${pendingAction.kind === 'publish' ? 'Мероприятие станет доступно пользователям.' : 'Мероприятие останется в черновиках, связи и сохранения сохранятся.'}`} confirmLabel={pendingAction.kind === 'publish' ? 'Опубликовать' : 'Снять с публикации'} destructive={pendingAction.kind === 'unpublish'} busy={Boolean(busyId)} onClose={() => setPendingAction(null)} onConfirm={() => void action(pendingAction.event, pendingAction.kind)} />}
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
  return <Modal label="Проверка тегов" onClose={onClose} busy={busy} dirty={JSON.stringify([...ids].sort()) !== JSON.stringify([...event.tagIds].sort())}><div className="eyebrow">ПРОВЕРКА ТЕГОВ</div><h2>{event.title}</h2><p className="muted">Проверьте предложенные теги. После подтверждения мероприятие можно опубликовать.</p><div className="review-context"><p>{event.description}</p><div className="event-meta"><span><Icon icon={Clock3} size={16} />{displayDate(event.eventDateTime)}</span><span><Icon icon={MapPin} size={16} />{event.location}</span></div><a href={event.source} target="_blank" rel="noopener noreferrer">Первоисточник<Icon icon={ExternalLink} size={16} /></a></div><fieldset disabled={busy}><legend className="field-title">Теги мероприятия</legend><TagSelector tags={tags} selected={ids} onChange={setIds} /></fieldset>{error && <Notice text={error} />}<div className="dialog-actions"><ModalCancel disabled={busy} /><Button variant="primary" loading={busy} onClick={() => void confirm()}>Подтвердить теги</Button></div></Modal>
}

function TagEditor({ tag, onClose, onSaved }: { tag: Tag | null; onClose: () => void; onSaved: (message: string) => void }) {
  const [name, setName] = useState(tag?.name || '')
  const [description, setDescription] = useState(tag?.description || '')
  const [examples, setExamples] = useState(() => (tag?.examples.length ? tag.examples : ['']).map(value => ({ key: crypto.randomUUID(), value })))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const changeExample = (index: number, value: string) => setExamples(old => old.map((item, i) => i === index ? { ...item, value } : item))
  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('')
    const payload: TagPayload = { name: name.trim(), description: description.trim(), examples: examples.map(x => x.value.trim()).filter(Boolean) }
    try { if (tag) await api.updateTag(tag.id, payload); else await api.createTag(payload); onSaved(tag ? 'Тег сохранён.' : 'Тег создан.') }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusy(false) }
  }
  return <Modal drawer label={tag ? 'Редактирование тега' : 'Новый тег'} busy={busy} dirty={name !== (tag?.name || '') || description !== (tag?.description || '') || JSON.stringify(examples.map(x => x.value)) !== JSON.stringify(tag?.examples.length ? tag.examples : [''])} onClose={onClose}><div className="drawer-head"><div><div className="eyebrow">ТАКСОНОМИЯ</div><h2>{tag ? 'Редактировать тег' : 'Новый тег'}</h2></div></div><form className="drawer-body" onSubmit={submit}><fieldset disabled={busy} className="form-stack"><FormField label="Название"><Input value={name} onChange={e => setName(e.target.value)} required placeholder="Например, Искусство" /></FormField><FormField label="Описание"><Textarea value={description} onChange={e => setDescription(e.target.value)} required rows={4} placeholder="Опишите, какие события относятся к тегу" /></FormField><div className="form-group"><div className="field-title">Примеры для модели</div><p className="hint">Добавьте несколько содержательных примеров, по которым модель сможет распознать тему.</p>{examples.map((example, index) => <div className="example-row" key={example.key}><Textarea value={example.value} aria-label={`Пример ${index + 1}`} onChange={e => changeExample(index, e.target.value)} rows={2} placeholder={`Пример ${index + 1}`} /><IconButton icon={Trash2} label={`Удалить пример ${index + 1}`} variant="destructive" onClick={() => setExamples(old => old.filter((_, i) => i !== index))} /></div>)}<Button icon={Plus} variant="secondary" onClick={() => setExamples(old => [...old, { key: crypto.randomUUID(), value: '' }])}>Добавить пример</Button></div>{error && <Notice text={error} />}<div className="drawer-actions"><ModalCancel disabled={busy} /><Button type="submit" variant="primary" loading={busy}>Сохранить тег</Button></div></fieldset></form></Modal>
}

function TagsPage({ tags, loading, error, reload }: { tags: Tag[]; loading: boolean; error: string; reload: () => void }) {
  const [editing, setEditing] = useState<Tag | 'new' | null>(null)
  const [message, setMessage] = useState('')
  const [actionError, setActionError] = useState('')
  const [busyId, setBusyId] = useState('')
  const [removing, setRemoving] = useState<Tag | null>(null)
  const actionLock = useRef(false)
  async function remove(tag: Tag) {
    if (actionLock.current) return
    actionLock.current = true
    setBusyId(tag.id); setActionError(''); setMessage('')
    try { await api.deleteTag(tag.id); setMessage('Тег удалён.'); reload() }
    catch (err) { setActionError(errorMessage(err)) }
    finally { setBusyId(''); actionLock.current = false; setRemoving(null) }
  }
  return <><div className="page-head"><div><h1>Теги</h1><p className="muted">Темы, по которым EventHub подбирает и группирует мероприятия.</p></div><Button icon={Plus} variant="primary" onClick={() => setEditing('new')}>Новый тег</Button></div>{message && <Alert text={message} success onClose={() => setMessage('')} />}{(error || actionError) && <Notice text={error || actionError} onRetry={error ? reload : undefined} onClose={error ? undefined : () => setActionError('')} />}{loading && tags.length > 0 && <Loading compact />}{loading && !tags.length ? <Loading /> : tags.length ? <div className="tag-grid">{tags.map(tag => <Panel mode="secondary" className="tag-card" key={tag.id}><div className="tag-card-icon"><Icon icon={Tags} /></div><h3>{tag.name}</h3><p>{tag.description}</p><div className="tag-count">{tag.examples.length} {tag.examples.length % 10 === 1 && tag.examples.length % 100 !== 11 ? 'пример' : [2, 3, 4].includes(tag.examples.length % 10) && ![12, 13, 14].includes(tag.examples.length % 100) ? 'примера' : 'примеров'}</div><div className="tag-actions"><Button size="small" variant="secondary" disabled={Boolean(busyId) || loading} onClick={() => setEditing(tag)}>Редактировать</Button><Button size="small" variant="destructive" disabled={Boolean(busyId) || loading} onClick={() => setRemoving(tag)}>Удалить</Button></div></Panel>)}</div> : !error && <EmptyState icon={Tags} title="Тегов пока нет" text="Создайте первый тег для классификации мероприятий." />}{removing && <ConfirmDialog title="Удалить тег?" text={`«${removing.name}» будет удалён из списка тегов. Это действие нельзя отменить.`} confirmLabel="Удалить тег" destructive busy={Boolean(busyId)} onClose={() => setRemoving(null)} onConfirm={() => void remove(removing)} />}{editing && <TagEditor key={editing === 'new' ? 'new' : editing.id} tag={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={text => { setEditing(null); setMessage(text); reload() }} />}</>
}

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
    {tagsError && section !== 'tags' && <Alert text={`Не удалось загрузить теги: ${tagsError}`} onRetry={reloadTags} />}
    {section === 'events' && <EventList key="events" tags={tags} reviewOnly={false} refreshTags={reloadTags} />}
    {section === 'review' && <EventList key="review" tags={tags} reviewOnly refreshTags={reloadTags} />}
    {section === 'tags' && <TagsPage tags={tags} loading={tagsLoading} error={tagsError} reload={reloadTags} />}
    {section === 'imports' && <ImportsPage tags={tags} />}
  </AppShell>
}

