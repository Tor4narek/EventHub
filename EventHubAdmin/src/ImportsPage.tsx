import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Download, ExternalLink, ListChecks, RefreshCw, Check, Image } from 'lucide-react'
import { api, type ImportItem, type ImportRun, type ImportEdit, type Tag } from './api'
import { Alert, Badge, Button, EmptyState, FormField, Icon, Input, LoadingState, Modal, ModalCancel, Panel, Textarea } from './ui'
import { displayDate, fromMoscowInput, toMoscowInput } from './time'
import { TagSelector } from './TagSelector'

const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'Не удалось выполнить действие.'
const labels: Record<ImportItem['status'], string> = { Pending: 'В очереди', Processing: 'Загружается', Ready: 'Проверить', Failed: 'Ошибка', Confirmed: 'Черновик создан', Duplicate: 'Уже импортировано' }
const processing = (run: ImportRun) => run.items.some(item => item.status === 'Pending' || item.status === 'Processing')

function ImportEditor({ item, tags, onClose, onSaved }: { item: ImportItem; tags: Tag[]; onClose: () => void; onSaved: (text: string) => void }) {
  const [title, setTitle] = useState(item.title || '')
  const [description, setDescription] = useState(item.description || '')
  const [date, setDate] = useState(toMoscowInput(item.eventDateTime))
  const [deadline, setDeadline] = useState(toMoscowInput(item.deadline))
  const [location, setLocation] = useState(item.location || '')
  const [tagIds, setTagIds] = useState(item.tagIds)
  const [image, setImage] = useState(item.mainImg || '')
  const [busy, setBusy] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState('')
  const lock = useRef(false)
  const initial = JSON.stringify([item.title || '', item.description || '', toMoscowInput(item.eventDateTime), toMoscowInput(item.deadline), item.location || '', [...item.tagIds].sort(), item.mainImg || ''])
  const dirty = JSON.stringify([title, description, date, deadline, location, [...tagIds].sort(), image]) !== initial
  async function upload(file?: File) {
    if (!file) return
    if (!['image/jpeg', 'image/png', 'image/webp', 'image/gif'].includes(file.type) || file.size > 10 * 1024 * 1024) { setError('Выберите изображение JPEG, PNG, WebP или GIF размером до 10 МБ.'); return }
    setUploading(true); setError('')
    try { setImage((await api.uploadImage(file)).url) } catch (err) { setError(errorMessage(err)) } finally { setUploading(false) }
  }
  async function save(confirm: boolean) {
    if (lock.current) return
    setError('')
    let payload: ImportEdit
    try {
      const eventDateTime = date ? fromMoscowInput(date) : null
      const deadlineUtc = deadline ? fromMoscowInput(deadline) : null
      if (confirm && (!title.trim() || !description.trim() || !location.trim() || !eventDateTime)) throw new Error('Заполните название, описание, место и дату мероприятия.')
      if (confirm && !tagIds.length) throw new Error('Выберите хотя бы один тег.')
      if (confirm && eventDateTime && Date.parse(eventDateTime) <= Date.now()) throw new Error('Дата мероприятия должна быть в будущем.')
      if (deadlineUtc && eventDateTime && Date.parse(deadlineUtc) > Date.parse(eventDateTime)) throw new Error('Дедлайн не может быть позже начала мероприятия.')
      payload = { title: title.trim(), description: description.trim(), location: location.trim(), eventDateTime, deadline: deadlineUtc, tagIds, mainImg: image.trim() || null }
    } catch (err) { setError(errorMessage(err)); return }
    lock.current = true; setBusy(true)
    try {
      await api.updateImportItem(item.importRunId, item.id, payload)
      if (confirm) await api.confirmImportItem(item.importRunId, item.id)
      onSaved(confirm ? 'Проверка завершена. Черновик доступен в разделе «Мероприятия».' : 'Изменения сохранены в очереди импорта.')
    } catch (err) { setError(errorMessage(err)) } finally { lock.current = false; setBusy(false) }
  }
  return <Modal drawer label="Проверка импорта" busy={busy || uploading} dirty={dirty} onClose={onClose}>
    <div className="drawer-head"><div className="eyebrow">ИМПОРТ ITMO EVENTS</div><h2>Проверить мероприятие</h2></div>
    <form className="drawer-body" onSubmit={(e: FormEvent) => { e.preventDefault(); void save(true) }}><fieldset disabled={busy || uploading} className="form-stack">
      <a className="source-link" href={item.source} target="_blank" rel="noopener noreferrer">Открыть первоисточник<Icon icon={ExternalLink} size={16} /></a>
      {item.warnings.length > 0 && <div className="import-warnings"><strong>Проверьте перед сохранением</strong><ul>{item.warnings.map((warning, index) => <li key={index}>{warning}</li>)}</ul></div>}
      {item.error && <Alert text={item.error} />}
      <FormField label="Название"><Input value={title} maxLength={300} onChange={e => setTitle(e.target.value)} /></FormField>
      <FormField label="Описание"><Textarea value={description} maxLength={100000} rows={6} onChange={e => setDescription(e.target.value)} /></FormField>
      {item.originalDescription && <details className="import-original"><summary>Исходное описание</summary><p>{item.originalDescription}</p></details>}
      {item.suggestedDescription && <Panel className="import-suggestion"><p>{item.suggestedDescription}</p><Button size="small" onClick={() => setDescription(item.suggestedDescription!)}>Использовать описание модели</Button></Panel>}
      <div className="form-grid"><FormField label="Дата и время · МСК"><Input type="datetime-local" value={date} onChange={e => setDate(e.target.value)} /></FormField><FormField label="Дедлайн · МСК"><Input type="datetime-local" value={deadline} onChange={e => setDeadline(e.target.value)} /></FormField></div>
      <FormField label="Место проведения"><Input value={location} maxLength={300} placeholder="Адрес или Онлайн" onChange={e => setLocation(e.target.value)} /></FormField>
      <div className="form-group"><div className="field-title">Теги мероприятия</div><TagSelector tags={tags} selected={tagIds} onChange={setTagIds} />{item.suggestedTagIds.length > 0 && <Button size="small" onClick={() => setTagIds(item.suggestedTagIds.filter(id => tags.some(tag => tag.id === id)))}>Использовать предложенные теги</Button>}<p className="hint">Для завершения проверки выберите хотя бы один тег.</p></div>
      <div className="form-group"><div className="field-title">Изображение · необязательно</div>{image && <div className="image-preview"><img src={image} alt="Изображение мероприятия" /><Button size="small" variant="ghost" onClick={() => setImage('')}>Убрать изображение</Button></div>}<label className="upload-zone"><input type="file" accept="image/jpeg,image/png,image/webp,image/gif" onChange={e => { void upload(e.target.files?.[0]); e.target.value = '' }} /><Icon icon={Image} size={20} /><span>{uploading ? 'Загрузка…' : 'Выбрать изображение'}</span><small>JPEG, PNG, WebP, GIF · до 10 МБ</small></label></div>
      {error && <Alert text={error} />}
      <div className="drawer-actions import-editor-actions"><ModalCancel disabled={busy || uploading} /><Button loading={busy} disabled={uploading} onClick={() => void save(false)}>Сохранить проверку</Button><Button type="submit" variant="primary" icon={Check} loading={busy} disabled={uploading}>Создать черновик</Button></div>
    </fieldset></form>
  </Modal>
}

export function ImportsPage({ tags }: { tags: Tag[] }) {
  const [urls, setUrls] = useState('')
  const [runs, setRuns] = useState<ImportRun[]>([])
  const [activeId, setActiveId] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [retryId, setRetryId] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [editing, setEditing] = useState<ImportItem | null>(null)
  const requestLock = useRef(false)
  const alive = useRef(true)
  const revision = useRef(0)
  const active = runs.find(run => run.id === activeId) || runs[0]
  const load = useCallback(async () => {
    const request = ++revision.current
    try { const result = await api.imports(); if (alive.current && request === revision.current) { setRuns(result); setError('') } }
    catch (err) { if (alive.current && request === revision.current) setError(errorMessage(err)) }
    finally { if (alive.current && request === revision.current) setLoading(false) }
  }, [])
  useEffect(() => { alive.current = true; void load(); return () => { alive.current = false; revision.current++ } }, [load])
  const hasPending = runs.some(processing)
  useEffect(() => {
    if (!hasPending || editing) return
    let cancelled = false
    let timer: ReturnType<typeof setTimeout>
    async function poll() {
      if (!document.hidden) await load()
      if (!cancelled) timer = setTimeout(() => void poll(), 2500)
    }
    timer = setTimeout(() => void poll(), 2500)
    return () => { cancelled = true; clearTimeout(timer) }
  }, [hasPending, editing, load])
  async function start(e: FormEvent) {
    e.preventDefault(); if (requestLock.current) return
    const links = [...new Set(urls.split(/\r?\n/).map(url => url.trim()).filter(Boolean))]
    if (links.length < 1 || links.length > 50) { setError('Введите от 1 до 50 ссылок, каждую с новой строки.'); return }
    if (links.some(link => { try { const url = new URL(link); return url.protocol !== 'https:' || url.hostname !== 'itmo.events' || !/^\/events\/[1-9]\d*\/?$/.test(url.pathname) || Boolean(url.username || url.password || url.port) } catch { return true } })) { setError('Допустимы только ссылки вида https://itmo.events/events/123456.'); return }
    requestLock.current = true; setBusy(true); setError(''); setMessage('')
    try { const result = await api.startImport(links); setActiveId(result.importId); setUrls(''); setMessage('Ссылки добавлены в очередь. Дождитесь загрузки и проверьте результаты.'); await load() }
    catch (err) { setError(errorMessage(err)) } finally { requestLock.current = false; setBusy(false) }
  }
  async function retry(item: ImportItem) {
    if (retryId) return
    setRetryId(item.id); setError('')
    try { await api.retryImportItem(item.importRunId, item.id); await load() } catch (err) { setError(errorMessage(err)) } finally { setRetryId('') }
  }
  return <>
    <div className="page-head"><div><h1>Импорт мероприятий</h1><p className="muted">Добавьте ссылки ITMO Events, проверьте описание, даты и теги. Публикация выполняется отдельно.</p></div><Button icon={RefreshCw} onClick={() => void load()} disabled={loading}>Обновить</Button></div>
    <Panel className="import-start"><form className="form-stack" onSubmit={start}><FormField label="Ссылки на мероприятия" hint="Каждая ссылка с новой строки · до 50 ссылок за запуск"><Textarea rows={4} value={urls} disabled={busy} onChange={e => setUrls(e.target.value)} placeholder="https://itmo.events/events/123456" /></FormField><div><Button type="submit" variant="primary" icon={Download} loading={busy}>Импортировать</Button></div></form></Panel>
    {message && <Alert text={message} success onClose={() => setMessage('')} />}{error && <Alert text={error} onRetry={() => void load()} />}
    {loading ? <LoadingState /> : !runs.length ? <EmptyState icon={Download} title="Импортов пока нет" text="Добавьте первую ссылку или сразу несколько мероприятий." /> : <>
      <div className="import-runs" aria-label="Последние запуски">{runs.map(run => <Button key={run.id} size="small" variant={active?.id === run.id ? 'primary' : 'secondary'} onClick={() => setActiveId(run.id)}>{displayDate(run.createdAt)} · {run.items.length}</Button>)}</div>
      {active && <><div className="list-heading"><h2>Результаты импорта</h2><span>{active.items.filter(item => !['Pending', 'Processing'].includes(item.status)).length} из {active.items.length} обработано</span></div><div className="import-items">{active.items.map(item => <Panel className="import-item" key={item.id}><div className="import-item-heading"><h3>{item.title || `Мероприятие ${item.source.split('/').at(-1)}`}</h3><Badge tone={item.status === 'Failed' ? 'pending' : item.status === 'Confirmed' ? 'live' : 'draft'}>{labels[item.status]}</Badge></div><a className="source-link" href={item.source} target="_blank" rel="noopener noreferrer">{item.source}<Icon icon={ExternalLink} size={14} /></a>{item.error && <p className="field-error">{item.error}</p>}<div className="import-item-actions">{['Ready', 'Failed'].includes(item.status) && <Button icon={ListChecks} size="small" onClick={() => setEditing(item)}>Проверить</Button>}{item.status === 'Failed' && <Button icon={RefreshCw} size="small" loading={retryId === item.id} disabled={Boolean(retryId)} onClick={() => void retry(item)}>Повторить загрузку</Button>}{item.eventId && <span className="hint">Мероприятие доступно в разделе «Мероприятия».</span>}</div></Panel>)}</div></>}
    </>}
    {editing && <ImportEditor key={editing.id} item={editing} tags={tags} onClose={() => setEditing(null)} onSaved={text => { setEditing(null); setMessage(text); void load() }} />}
  </>
}
