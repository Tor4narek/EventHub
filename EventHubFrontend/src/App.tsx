import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { getEvent, getEvents, getMe, getRecommendations, getSavedEvents, getTags, loginWithMax, mediaUrl, removeSavedEvent, saveEvent, saveInterests, saveSettings, type EventResponse, type PagedEvents, type TagResponse } from './api';
import { maxApp, maxInitData, observeMaxBack, openExternal } from './maxBridge';

type Page = 'catalog' | 'search' | 'saved' | 'interests' | 'selection';
type Period = 'all' | 'today' | 'week' | 'month';
type Format = 'all' | 'offline' | 'online' | 'hybrid';

const asset = (name: string) => `/assets/${name}`;
const icons = {
  close: asset('1_2_0.svg'), search: asset('1_2_1.svg'), searchActive: asset('1_79_1.svg'),
  remind: asset('1_2_3.svg'), selection: asset('1_2_4.svg'), catalog: asset('1_2_5.svg'), catalogInactive: asset('1_206_3.svg'),
  saved: asset('1_2_6.svg'), interests: asset('1_2_7.svg'), checked: asset('1_206_1.svg'),
  savedActive: asset('1_206_4.svg'), empty: asset('1_257_0.png'), loading: asset('1_257_3.svg'),
};
const storageNamespace = 'eventhub';
const locale = 'ru-RU';

function dateOnly(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function eventDate(event: EventResponse) {
  const date = new Date(event.eventDateTime);
  if (Number.isNaN(date.getTime())) return event.eventDateTime;
  return `${new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }).format(date)}, ${new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(date)}`;
}

function deadlineText(event: EventResponse) {
  if (!event.deadline) return 'Регистрация открыта';
  const date = new Date(event.deadline);
  if (Number.isNaN(date.getTime())) return 'Регистрация открыта';
  return `До ${new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }).format(date)}`;
}

function eventCountText(count: number) {
  const mod100 = count % 100;
  const mod10 = count % 10;
  const noun = mod100 >= 11 && mod100 <= 14 ? 'мероприятий' : mod10 === 1 ? 'мероприятие' : mod10 >= 2 && mod10 <= 4 ? 'мероприятия' : 'мероприятий';
  return `${count} ${noun}`;
}

function Icon({ src, alt = '' }: { src: string; alt?: string }) {
  return <img className="icon" src={src} alt={alt} />;
}

function Header({ title, subtitle, onClose, back = false }: { title: string; subtitle?: string; onClose: () => void; back?: boolean }) {
  return <header className="header">
    <div className="header-row"><h1>{title}</h1><button className="close-button" aria-label={back ? 'Назад к мероприятиям' : 'Закрыть'} onClick={onClose}>{back ? <svg className="back-arrow" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M19 12H5m0 0 6-6m-6 6 6 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg> : <Icon src={icons.close} />}</button></div>
    {subtitle && <p>{subtitle}</p>}
  </header>;
}

function SearchField({ value, onChange, onFocus, autoFocus = false }: { value: string; onChange: (value: string) => void; onFocus?: () => void; autoFocus?: boolean }) {
  return <label className={`search-field ${onFocus ? '' : 'search-field-active'}`}>
    <Icon src={onFocus ? icons.search : icons.searchActive} />
    <input aria-label="Поиск мероприятий" value={value} onChange={event => onChange(event.target.value)} onFocus={onFocus} placeholder="Поиск мероприятий" autoFocus={autoFocus} />
  </label>;
}

function EventCard({ event, saved, onSave, onOpen, savedPage = false, localOnly = false, saving = false }: { event: EventResponse; saved: boolean; onSave: () => void; onOpen: () => void; savedPage?: boolean; localOnly?: boolean; saving?: boolean }) {
  return <article className={`event-card ${savedPage ? 'event-card-bordered' : ''}`}>
    <button className="card-main" onClick={onOpen} aria-label={`Открыть ${event.title}`}>
      <h3>{event.title}</h3>
      <p className="event-description">{event.description}</p>
      <p className="event-date">{eventDate(event)}</p>
      <p className="event-location">{event.location}</p>
    </button>
    <div className="card-actions">
      <span className="event-deadline">{savedPage ? localOnly ? 'Только на этом устройстве · без напоминания' : 'Сохранено в профиле MAX' : deadlineText(event)}</span>
      <button className={`save-button ${saved ? 'is-saved' : ''}`} onClick={onSave} disabled={saving} aria-label={saved ? `Убрать из сохранённых: ${event.title}` : `Сохранить: ${event.title}`}>
        <Icon src={saved ? icons.checked : icons.remind} />{saved ? 'Сохранено' : 'Сохранить'}
      </button>
    </div>
  </article>;
}

function BottomNav({ page, onNavigate }: { page: Page; onNavigate: (page: Page) => void }) {
  const items: { page: Page; label: string; icon: string }[] = [
    { page: 'selection', label: 'Подборка', icon: icons.selection },
    { page: 'catalog', label: 'Все', icon: page === 'catalog' || page === 'search' ? icons.catalog : icons.catalogInactive },
    { page: 'saved', label: 'Сохранённые', icon: page === 'saved' ? icons.savedActive : icons.saved },
    { page: 'interests', label: 'Интересы', icon: icons.interests },
  ];
  return <nav className="bottom-nav" aria-label="Основная навигация">
    {items.map(item => <button key={item.page} className={page === item.page || (page === 'search' && item.page === 'catalog') ? 'active' : ''} onClick={() => onNavigate(item.page)}>
      <Icon src={item.icon} /><span>{item.label}</span>
    </button>)}
  </nav>;
}

function EmptyState({ onReset, title = 'Ничего не найдено', description = 'Измените запрос или сбросьте фильтры.', actionLabel = 'Сбросить фильтры' }: { onReset: () => void; title?: string; description?: string; actionLabel?: string }) {
  return <div className="empty-state"><img className="empty-art" src={icons.empty} alt="" /><h2>{title}</h2><p>{description}</p><button className="small-dark-button" onClick={onReset}>{actionLabel}</button></div>;
}

function LoadingMore() {
  return <div className="loading-more"><Icon src={icons.loading} /><span>Загружаем ещё мероприятия</span></div>;
}

function FilterSheet({ period, selectedDay, format, setPeriod, setFormat, chooseDate, tags, selectedTags, tagsEnabled, toggleTag, setTagsEnabled, reset, apply, count, close }: { period: Period; selectedDay: string | null; format: Format; setPeriod: (v: Period) => void; setFormat: (v: Format) => void; chooseDate: () => void; tags: TagResponse[]; selectedTags: string[]; tagsEnabled: boolean; toggleTag: (id: string) => void; setTagsEnabled: (enabled: boolean) => void; reset: () => void; apply: () => void; count: number; close: () => void }) {
  const [tagSearch, setTagSearch] = useState('');
  const visibleTags = tags.filter(tag => tag.name.toLocaleLowerCase('ru').includes(tagSearch.toLocaleLowerCase('ru').trim()));
  return <div className="modal-backdrop" onMouseDown={close}>
    <section className="filter-sheet" role="dialog" aria-modal="true" aria-label="Фильтры" onMouseDown={event => event.stopPropagation()}>
      <div className="grabber" /><div className="sheet-title"><h2>Фильтры</h2><button onClick={reset}>Сбросить</button></div>
      <div className="sheet-body">
      <div className="filter-group"><h3>КОГДА</h3><div className="chip-row">
        {([['all', 'Все даты'], ['today', 'Сегодня'], ['week', 'Неделя'], ['month', 'Месяц']] as const).map(([value, label]) => <button key={value} className={`chip ${!selectedDay && period === value ? 'selected' : ''}`} onClick={() => setPeriod(value)}>{label}</button>)}
        <button className={`chip ${selectedDay ? 'selected' : ''}`} onClick={chooseDate}>{selectedDay ? new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }).format(new Date(`${selectedDay}T12:00:00`)) : 'Выбрать дату'}</button>
      </div></div>
      <div className="filter-group"><h3>ФОРМАТ</h3><div className="chip-row">
        {([['offline', 'Офлайн'], ['online', 'Онлайн'], ['hybrid', 'Гибрид']] as const).map(([value, label]) => <button key={value} className={`chip ${format === value ? 'selected' : ''}`} onClick={() => setFormat(value)}>{label}</button>)}
      </div></div>
      <div className="filter-group tag-filter"><div className="tag-filter-heading"><h3>ТЕГИ</h3>{selectedTags.length > 0 && <button onClick={() => setTagsEnabled(!tagsEnabled)}>{tagsEnabled ? 'Снять фильтр' : 'Применить мои теги'}</button>}</div>
        {tags.length > 8 && <input className="tag-search" aria-label="Поиск тегов" value={tagSearch} onChange={event => setTagSearch(event.target.value)} placeholder="Найти тег" />}
        <div className="tag-options"><div className="chip-row">{visibleTags.map(tag => <button key={tag.id} className={`chip ${selectedTags.includes(tag.id) ? 'selected' : ''}`} onClick={() => toggleTag(tag.id)}>{tag.name}</button>)}</div>{!tags.length && <p>Теги пока недоступны</p>}{tags.length > 0 && !visibleTags.length && <p>Теги не найдены</p>}</div>
        {selectedTags.length > 0 && <p className="tag-hint">{tagsEnabled ? `Применены мои теги: ${selectedTags.length}` : 'Фильтр по тегам выключен'}</p>}
      </div>
      </div>
      <button className="primary-button accent" onClick={apply}>Показать {eventCountText(count)}</button>
    </section>
  </div>;
}

function CalendarSheet({ selectedDay, onSelect, onClear, close }: { selectedDay: string | null; onSelect: (day: string) => void; onClear: () => void; close: () => void }) {
  const [month, setMonth] = useState(() => selectedDay ? new Date(`${selectedDay}T12:00:00`) : new Date());
  const year = month.getFullYear();
  const monthIndex = month.getMonth();
  const offset = (new Date(year, monthIndex, 1).getDay() + 6) % 7;
  const daysInMonth = new Date(year, monthIndex + 1, 0).getDate();
  const today = dateOnly(new Date());
  const cells = Array.from({ length: offset + daysInMonth }, (_, index) => index < offset ? null : index - offset + 1);
  const changeMonth = (step: number) => setMonth(new Date(year, monthIndex + step, 1));
  return <div className="modal-backdrop" onMouseDown={close}>
    <section className="calendar-sheet" role="dialog" aria-modal="true" aria-label="Выбрать дату" onMouseDown={event => event.stopPropagation()}>
      <div className="grabber" />
      <div className="calendar-sheet-top"><h2>Выбрать дату</h2><button aria-label="Закрыть календарь" onClick={close}>×</button></div>
      <div className="calendar-month-nav"><button aria-label="Предыдущий месяц" onClick={() => changeMonth(-1)}>‹</button><b>{new Intl.DateTimeFormat(locale, { month: 'long' }).format(month)} {year}</b><button aria-label="Следующий месяц" onClick={() => changeMonth(1)}>›</button></div>
      <div className="month-grid" key={`${year}-${monthIndex}`}>{['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'].map(day => <span className="month-weekday" key={day}>{day}</span>)}{cells.map((day, index) => day === null ? <span key={`empty-${index}`} /> : <button key={day} className={`${selectedDay === dateOnly(new Date(year, monthIndex, day)) ? 'selected' : ''} ${today === dateOnly(new Date(year, monthIndex, day)) ? 'today' : ''}`} aria-label={new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(year, monthIndex, day))} aria-pressed={selectedDay === dateOnly(new Date(year, monthIndex, day))} onClick={() => onSelect(dateOnly(new Date(year, monthIndex, day)))}>{day}</button>)}</div>
      <div className="calendar-sheet-actions"><label>Перейти к дате<input type="date" aria-label="Указать любую дату" value={selectedDay || ''} onChange={event => { if (event.target.value) onSelect(event.target.value); }} /></label><button onClick={onClear}>Все даты</button></div>
    </section>
  </div>;
}

function DetailSheet({ event, close, saved, toggleSave, saving = false }: { event: EventResponse; close: () => void; saved: boolean; toggleSave: () => void; saving?: boolean }) {
  return <div className="modal-backdrop" onMouseDown={close}>
    <section className="detail-sheet" role="dialog" aria-modal="true" aria-label={event.title} onMouseDown={event => event.stopPropagation()}>
      <div className="grabber" />
      {event.mainImg && <img className="detail-image" src={mediaUrl(event.mainImg)} alt="" />}
      <h2>{event.title}</h2><p className="detail-date">{eventDate(event)}</p><p>{event.location}</p><p className="detail-description">{event.description}</p>
      {event.source && /^https?:\/\//i.test(event.source) && <button className="source-link" onClick={() => openExternal(event.source)}>Источник мероприятия ↗</button>}
      <button className={`primary-button ${saved ? '' : 'accent'}`} onClick={toggleSave} disabled={saving}>{saved ? 'Убрать из сохранённых' : 'Сохранить мероприятие'}</button>
    </section>
  </div>;
}

function readSaved(): EventResponse[] {
  try { return JSON.parse(localStorage.getItem(`${storageNamespace}-saved-v1`) || '[]') as EventResponse[]; }
  catch { return []; }
}

function readTagIds(): string[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(`${storageNamespace}-tags-v1`) || '[]');
    return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(item)) : [];
  } catch { return []; }
}

function readTagFilterEnabled() {
  try { return localStorage.getItem(`${storageNamespace}-tags-enabled-v1`) !== 'false'; }
  catch { return true; }
}

export default function App() {
  const [page, setPage] = useState<Page>('catalog');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [period, setPeriod] = useState<Period>('all');
  const [format, setFormat] = useState<Format>('all');
  const [selectedDay, setSelectedDay] = useState<string | null>(null);
  const [stripAnchor, setStripAnchor] = useState(() => dateOnly(new Date()));
  const [visibleDayCount, setVisibleDayCount] = useState(6);
  const daysRef = useRef<HTMLDivElement>(null);
  const [calendarOpen, setCalendarOpen] = useState(false);
  const [filterOpen, setFilterOpen] = useState(false);
  const [detail, setDetail] = useState<EventResponse | null>(null);
  const [saved, setSaved] = useState<EventResponse[]>(() => maxInitData() ? [] : readSaved());
  const [selectedTags, setSelectedTags] = useState<string[]>(() => maxInitData() ? [] : readTagIds());
  const [tagsEnabled, setTagsEnabled] = useState(readTagFilterEnabled);
  const [tags, setTags] = useState<TagResponse[]>([]);
  const [result, setResult] = useState<PagedEvents | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  const [authStatus, setAuthStatus] = useState<'pending' | 'ready' | 'anonymous' | 'error'>(() => maxInitData() ? 'pending' : 'anonymous');
  const [authError, setAuthError] = useState('');
  const [authRetry, setAuthRetry] = useState(0);
  const [token, setToken] = useState<string | null>(null);
  const [interestSaving, setInterestSaving] = useState(false);
  const [savedLoading, setSavedLoading] = useState(false);
  const [syncError, setSyncError] = useState('');
  const [actionError, setActionError] = useState('');
  const [savingIds, setSavingIds] = useState<string[]>([]);
  const savingRef = useRef(new Set<string>());
  const savedVersion = useRef(0);
  const profileVersion = useRef(0);
  const interestSavingRef = useRef(false);
  const [profileReload, setProfileReload] = useState(0);
  const [weeklyDigest, setWeeklyDigest] = useState(false);
  const [settingsSaving, setSettingsSaving] = useState(false);
  const settingsSavingRef = useRef(false);

  useEffect(() => {
    let mounted = true;
    const syncViewport = () => maxApp()?.getViewportSize?.().then(viewport => {
      const height = Number.parseFloat(viewport.height);
      if (mounted && Number.isFinite(height) && height > 300) document.documentElement.style.setProperty('--max-viewport-height', `${height}px`);
    }).catch(() => {});
    syncViewport();
    window.addEventListener('resize', syncViewport);
    window.visualViewport?.addEventListener('resize', syncViewport);
    return () => {
      mounted = false;
      window.removeEventListener('resize', syncViewport);
      window.visualViewport?.removeEventListener('resize', syncViewport);
      document.documentElement.style.removeProperty('--max-viewport-height');
    };
  }, []);

  useEffect(() => { if (authStatus !== 'anonymous') return; try { localStorage.setItem(`${storageNamespace}-saved-v1`, JSON.stringify(saved)); } catch { /* in-memory fallback */ } }, [saved, authStatus]);
  useEffect(() => { if (authStatus !== 'anonymous') return; try { localStorage.setItem(`${storageNamespace}-tags-v1`, JSON.stringify(selectedTags)); } catch { /* in-memory fallback */ } }, [selectedTags, authStatus]);
  useEffect(() => { try { localStorage.setItem(`${storageNamespace}-tags-enabled-v1`, String(tagsEnabled)); } catch { /* in-memory fallback */ } }, [tagsEnabled]);
  useEffect(() => {
    const initData = maxInitData();
    if (!initData) { setAuthStatus('anonymous'); return; }
    const controller = new AbortController();
    setAuthStatus('pending'); setAuthError(''); setToken(null); setSaved([]);
    loginWithMax(initData, controller.signal)
      .then(async session => {
        const [profile, savedEvents] = await Promise.all([getMe(session.accessToken, controller.signal), getSavedEvents(session.accessToken, controller.signal)]);
        const ids = profile.tagIds;
        if (controller.signal.aborted) return;
        setToken(session.accessToken);
        setSelectedTags(ids);
        setSaved(savedEvents);
        setWeeklyDigest(profile.isWeeklyDigestEnabled);
        setTagsEnabled(true);
        setAuthStatus('ready');
      })
      .catch(err => {
        if (controller.signal.aborted) return;
        setAuthError(err instanceof Error ? err.message : 'Не удалось получить интересы');
        setAuthStatus('error');
      });
    return () => controller.abort();
  }, [authRetry]);

  useEffect(() => {
    const refresh = () => { if (document.visibilityState === 'visible') setProfileReload(value => value + 1); };
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', refresh);
    return () => { window.removeEventListener('focus', refresh); document.removeEventListener('visibilitychange', refresh); };
  }, []);

  useEffect(() => {
    if (!token || authStatus !== 'ready') return;
    const controller = new AbortController();
    const savedSnapshot = savedVersion.current;
    const profileSnapshot = profileVersion.current;
    setSavedLoading(true); setSyncError('');
    Promise.all([getSavedEvents(token, controller.signal), getMe(token, controller.signal)])
      .then(([items, profile]) => {
        if (controller.signal.aborted) return;
        if (savedSnapshot === savedVersion.current && savingRef.current.size === 0) setSaved(items);
        if (profileSnapshot === profileVersion.current && !interestSavingRef.current && !settingsSavingRef.current) {
          setSelectedTags(profile.tagIds);
          setWeeklyDigest(profile.isWeeklyDigestEnabled);
        }
      })
      .catch(err => { if (!controller.signal.aborted) setSyncError(err instanceof Error ? err.message : 'Не удалось синхронизировать профиль'); })
      .finally(() => { if (!controller.signal.aborted) setSavedLoading(false); });
    return () => controller.abort();
  }, [token, authStatus, page, profileReload]);
  useEffect(() => { const timer = window.setTimeout(() => setQuery(search.trim()), 280); return () => clearTimeout(timer); }, [search]);
  useEffect(() => {
    if (page !== 'catalog' || !daysRef.current) return;
    const element = daysRef.current;
    const measure = () => setVisibleDayCount(Math.max(1, Math.floor((element.clientWidth + 8) / 46)));
    measure();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', measure);
      return () => window.removeEventListener('resize', measure);
    }
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [page]);
  useEffect(() => {
    const controller = new AbortController();
    getTags(controller.signal).then(items => {
      setTags(items);
    }).catch(() => {});
    return () => controller.abort();
  }, []);

  const referenceDate = new Date();
  const activeDay = selectedDay;
  const appliedTags = tagsEnabled ? selectedTags : [];
  const range = useMemo(() => {
    if (selectedDay && page === 'catalog') return { from: selectedDay, to: selectedDay };
    if (period === 'all') return { allDates: true };
    const start = new Date(referenceDate.getFullYear(), referenceDate.getMonth(), referenceDate.getDate());
    const end = new Date(start);
    if (period === 'week') end.setDate(end.getDate() + 6);
    if (period === 'month') end.setMonth(end.getMonth() + 1);
    return { from: dateOnly(start), to: dateOnly(end) };
  }, [period, selectedDay, page]);

  useEffect(() => {
    if (page === 'saved' || page === 'interests') return;
    if (authStatus === 'pending' || authStatus === 'error') return;
    const controller = new AbortController();
    setLoading(true); setResult(null); setError('');
    const request = page === 'selection'
      ? token ? getRecommendations(token, controller.signal).then(items => ({ items, page: 1, pageSize: 3, totalCount: items.length, hasNextPage: false })) : Promise.reject(new Error('Откройте приложение в MAX, чтобы увидеть персональную подборку'))
      : getEvents(1, { search: page === 'search' ? query : undefined, ...range, format: format === 'all' ? undefined : format, tags: appliedTags.length ? appliedTags : undefined }, controller.signal);
    request.then(setResult).catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : 'Не удалось загрузить мероприятия'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, query, range.from, range.to, range.allDates, format, selectedTags.join(','), tagsEnabled, reload, profileReload, authStatus, token]);

  const navigate = useCallback((next: Page) => { setDetail(null); setFilterOpen(false); setCalendarOpen(false); if (next === 'catalog') setSearch(''); setPage(next); }, []);
  useEffect(() => {
    if (page !== 'search') return;
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') navigate('catalog'); };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [page, navigate]);
  const back = useCallback(() => {
    if (detail) setDetail(null);
    else if (calendarOpen) setCalendarOpen(false);
    else if (filterOpen) setFilterOpen(false);
    else if (page !== 'catalog') navigate('catalog');
    else if (maxApp()?.close) maxApp()!.close!();
    else if (window.history.length > 1) window.history.back();
    else window.close();
  }, [detail, calendarOpen, filterOpen, page, navigate]);
  useEffect(() => observeMaxBack(Boolean(detail || calendarOpen || filterOpen || page !== 'catalog'), back), [detail, calendarOpen, filterOpen, page, back]);

  async function toggleSave(event: EventResponse) {
    if (authStatus === 'pending' || authStatus === 'error' || savingRef.current.has(event.id)) return;
    const removing = saved.some(item => item.id === event.id);
    if (!token) {
      setSaved(current => removing ? current.filter(item => item.id !== event.id) : [...current, event]);
      return;
    }
    savingRef.current.add(event.id); savedVersion.current++;
    setSavingIds([...savingRef.current]); setActionError('');
    try {
      if (removing) await removeSavedEvent(event.id, token);
      else await saveEvent(event.id, token);
      setSaved(current => removing ? current.filter(item => item.id !== event.id) : current.some(item => item.id === event.id) ? current : [...current, event]);
      setReload(value => value + 1);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Не удалось изменить сохранённые');
    } finally {
      savingRef.current.delete(event.id); savedVersion.current++;
      setSavingIds([...savingRef.current]);
      setProfileReload(value => value + 1);
    }
  }

  async function toggleTag(id: string) {
    if (interestSavingRef.current || authStatus === 'pending' || authStatus === 'error') return;
    const next = selectedTags.includes(id) ? selectedTags.filter(item => item !== id) : [...selectedTags, id];
    if (!token) { setSelectedTags(next); setTagsEnabled(true); return; }
    interestSavingRef.current = true; profileVersion.current++;
    setInterestSaving(true);
    try {
      await saveInterests(next, token);
      setSelectedTags(next);
      setTagsEnabled(true);
      setAuthError('');
    } catch (err) {
      setAuthError(err instanceof Error ? err.message : 'Не удалось сохранить интересы');
    } finally { interestSavingRef.current = false; profileVersion.current++; setInterestSaving(false); setProfileReload(value => value + 1); }
  }

  async function toggleWeeklyDigest() {
    if (!token || settingsSavingRef.current) return;
    settingsSavingRef.current = true; profileVersion.current++;
    setSettingsSaving(true); setActionError('');
    try {
      await saveSettings(!weeklyDigest, token);
      setWeeklyDigest(value => !value);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Не удалось сохранить настройки');
    } finally {
      settingsSavingRef.current = false; profileVersion.current++;
      setSettingsSaving(false); setProfileReload(value => value + 1);
    }
  }

  function openEvent(event: EventResponse) {
    setDetail(event);
    getEvent(event.id).then(setDetail).catch(() => {});
  }

  function resetFilters() { setPeriod('all'); setFormat('all'); setSelectedDay(null); setStripAnchor(dateOnly(new Date())); setSearch(''); setTagsEnabled(false); }
  function loadMore() {
    if (!result?.hasNextPage || loadingMore || page === 'selection') return;
    setLoadingMore(true);
    getEvents(Number(result.page) + 1, { search: page === 'search' ? query : undefined, ...range, format: format === 'all' ? undefined : format, tags: appliedTags.length ? appliedTags : undefined })
      .then(next => setResult(current => current ? { ...next, items: [...current.items, ...next.items] } : next))
      .catch(err => setError(err instanceof Error ? err.message : 'Не удалось загрузить ещё мероприятия'))
      .finally(() => setLoadingMore(false));
  }

  const todayKey = dateOnly(referenceDate);
  const stripStart = new Date(`${stripAnchor}T12:00:00`);
  const dayItems = Array.from({ length: visibleDayCount }, (_, index) => {
    const date = new Date(stripStart.getFullYear(), stripStart.getMonth(), stripStart.getDate() + index);
    return { key: dateOnly(date), day: date.getDate(), weekday: new Intl.DateTimeFormat(locale, { weekday: 'short' }).format(date).replace('.', '') };
  });
  const visibleItems = page === 'catalog' ? [...(result?.items || [])].sort((left, right) => Date.parse(left.eventDateTime) - Date.parse(right.eventDateTime)) : result?.items || [];
  const displayCount = result?.totalCount ?? visibleItems.length;
  const title = page === 'saved' ? 'Сохранённые' : page === 'interests' ? 'Интересы' : page === 'selection' ? 'Подборка' : page === 'search' ? 'Поиск' : 'Все мероприятия';
  const subtitle = page === 'saved' ? token ? 'Общий список с ботом · напоминания включены' : 'События, сохранённые на этом устройстве' : page === 'catalog' ? appliedTags.length ? 'Мероприятия по вашим интересам' : 'Все доступные мероприятия' : page === 'selection' ? 'Три мероприятия для вас' : undefined;

  return <div className="app-shell">
    <Header title={title} subtitle={subtitle} onClose={back} back={page === 'search'} />
    <main className="main-content">
      {(syncError || actionError) && <div className="sync-notice" role="alert"><p>{actionError || syncError}</p><button onClick={() => { setActionError(''); setProfileReload(value => value + 1); setAuthRetry(value => value + 1); }}>Повторить синхронизацию</button></div>}
      {page === 'catalog' && <>
        <div className="search-inset"><SearchField value={search} onChange={setSearch} onFocus={() => navigate('search')} /></div>
        <div className="calendar"><div className="calendar-heading"><b>{new Intl.DateTimeFormat(locale, { month: 'long' }).format(stripStart).toUpperCase()} {stripStart.getFullYear()}</b><button onClick={() => setCalendarOpen(true)}>Выбрать дату</button></div>
          <div className="days" ref={daysRef} style={{ gridTemplateColumns: `repeat(${visibleDayCount}, minmax(0, 1fr))` }}>{dayItems.map(day => <button key={day.key} className={activeDay === day.key ? 'day active' : 'day'} onClick={() => { setPeriod('all'); if (selectedDay === day.key) { setSelectedDay(null); setStripAnchor(todayKey); } else setSelectedDay(day.key); }}><b>{day.day}</b><span>{day.weekday}</span></button>)}</div>
        </div>
        <div className="quick-filters"><button className={`chip ${period === 'all' && format === 'all' && !appliedTags.length && !selectedDay ? 'selected' : ''}`} onClick={resetFilters}>Все</button><button className="chip" onClick={() => setFilterOpen(true)}>Формат</button><button className="chip" onClick={() => setFilterOpen(true)}>Даты</button><button className="chip" onClick={() => setFilterOpen(true)}>Теги</button>{selectedTags.length > 0 && <button className={`chip ${tagsEnabled ? 'selected' : ''}`} onClick={() => setTagsEnabled(enabled => !enabled)}>По интересам · {selectedTags.length} {tagsEnabled ? '×' : '+'}</button>}</div>
        <div className="catalog-context"><span>{selectedDay ? `Выбрано: ${new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }).format(new Date(`${selectedDay}T12:00:00`))}` : period === 'all' ? 'Все даты' : period === 'today' ? 'Сегодня' : period === 'week' ? 'Ближайшая неделя' : 'Ближайший месяц'}</span>{selectedDay && <button onClick={() => { setSelectedDay(null); setStripAnchor(todayKey); }}>Сбросить дату</button>}</div>
      </>}
      {page === 'search' && <div className="search-page-top"><SearchField value={search} onChange={setSearch} autoFocus /><div className="result-line"><b>{eventCountText(displayCount)}</b><button onClick={resetFilters}>Сбросить</button></div></div>}
      {page === 'saved' && authStatus !== 'pending' && authStatus !== 'error' && <div className="saved-notice"><span>✓</span>Сохранено событий: {saved.length}</div>}
      {page === 'interests' && authStatus !== 'error' && <div className="interests-panel"><h2>Ваши интересы</h2><p>{token ? 'Интересы синхронизированы с ботом и вашим профилем.' : 'Выберите интересы, чтобы фильтровать каталог на этом устройстве.'}</p>{authError && <p className="interest-error">{authError}</p>}<div className="chip-row">{tags.map(tag => <button key={tag.id} disabled={interestSaving || authStatus === 'pending'} className={`chip ${selectedTags.includes(tag.id) ? 'selected' : ''}`} onClick={() => toggleTag(tag.id)}>{tag.name}</button>)}</div>{tags.filter(tag => selectedTags.includes(tag.id) && tag.description).map(tag => <p key={tag.id}><b>{tag.name}:</b> {tag.description}</p>)}<button className="primary-button accent" disabled={authStatus === 'pending' || interestSaving} onClick={() => { setTagsEnabled(true); navigate(token ? 'selection' : 'catalog'); }}>{token ? 'Показать подборку' : selectedTags.length ? 'Показать события по интересам' : 'Показать все мероприятия'}</button>{token && <div className="weekly-settings"><h2>Еженедельная подборка</h2><p>{weeklyDigest ? 'Бот присылает подборку каждое воскресенье.' : 'Рассылка в боте выключена.'}</p><button className="chip" disabled={settingsSaving} onClick={() => void toggleWeeklyDigest()}>{settingsSaving ? 'Сохраняем…' : weeklyDigest ? 'Выключить рассылку' : 'Включить рассылку'}</button></div>}</div>}
      {page === 'selection' && <div className="selection-top"><p>Персональная подборка из трёх мероприятий</p><button className="chip" onClick={() => navigate('interests')}>Мои интересы</button></div>}
      {authStatus === 'error' ? <div className="event-list"><div className="error-state"><h2>Не удалось подключить профиль MAX</h2><p>{authError}</p><button className="small-dark-button" onClick={() => setAuthRetry(value => value + 1)}>Повторить</button></div></div> : page === 'saved' ? <div className="event-list">{authStatus === 'pending' || savedLoading && !saved.length ? <LoadingMore /> : syncError && !saved.length ? <div className="error-state"><h2>Сохранённые недоступны</h2><p>{syncError}</p></div> : saved.length ? saved.map(event => <EventCard key={event.id} event={event} saved onSave={() => void toggleSave(event)} onOpen={() => openEvent(event)} savedPage localOnly={!token} saving={savingIds.includes(event.id)} />) : <EmptyState title="Пока нет сохранённых" description="Сохраняйте интересные мероприятия из каталога или в боте." actionLabel="Открыть каталог" onReset={() => navigate('catalog')} />}</div>
        : page !== 'interests' && <div className={`event-list ${page === 'catalog' ? 'catalog-list' : ''}`}>
          {loading && !result ? <LoadingMore /> : error && !result ? <div className="error-state"><h2>{page === 'selection' ? 'Подборка недоступна' : 'Не удалось загрузить мероприятия'}</h2><p>{error}</p><button className="small-dark-button" onClick={() => page === 'selection' && !token ? navigate('catalog') : setReload(value => value + 1)}>{page === 'selection' && !token ? 'Открыть каталог' : 'Повторить'}</button></div> : visibleItems.length ? visibleItems.map((event, index) => <div className="event-group-item" key={event.id}>{page === 'catalog' && (index === 0 || dateOnly(new Date(visibleItems[index - 1].eventDateTime)) !== dateOnly(new Date(event.eventDateTime))) && <h2 className="date-group">{new Intl.DateTimeFormat(locale, { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date(event.eventDateTime)).toUpperCase().replace(',', ' ·')}</h2>}<EventCard event={event} saved={saved.some(item => item.id === event.id)} onSave={() => void toggleSave(event)} onOpen={() => openEvent(event)} saving={authStatus === 'pending' || savingIds.includes(event.id)} /></div>) : <EmptyState onReset={() => page === 'selection' ? navigate(saved.length ? 'saved' : 'interests') : resetFilters()} title={page === 'selection' ? 'Новых мероприятий пока нет' : 'Ничего не найдено'} description={page === 'selection' ? saved.length ? 'Посмотрите события, которые вы уже сохранили в боте или приложении.' : 'Добавьте интересы или вернитесь в каталог.' : 'Измените запрос или сбросьте фильтры.'} actionLabel={page === 'selection' ? saved.length ? 'Сохранённые' : 'Мои интересы' : 'Сбросить фильтры'} />}
          {result?.hasNextPage && <button className="more-button" onClick={loadMore} disabled={loadingMore}>{loadingMore ? <LoadingMore /> : 'Показать ещё'}</button>}
        </div>}
    </main>
    <BottomNav page={page} onNavigate={navigate} />
    {calendarOpen && <CalendarSheet selectedDay={selectedDay} onSelect={day => { setPeriod('all'); setSelectedDay(day); setStripAnchor(day); setCalendarOpen(false); }} onClear={() => { setPeriod('all'); setSelectedDay(null); setStripAnchor(todayKey); setCalendarOpen(false); }} close={() => setCalendarOpen(false)} />}
    {filterOpen && <FilterSheet period={period} selectedDay={selectedDay} format={format} setPeriod={value => { setSelectedDay(null); setStripAnchor(todayKey); setPeriod(value); }} setFormat={setFormat} chooseDate={() => { setFilterOpen(false); setCalendarOpen(true); }} tags={tags} selectedTags={selectedTags} tagsEnabled={tagsEnabled} toggleTag={toggleTag} setTagsEnabled={setTagsEnabled} reset={resetFilters} apply={() => setFilterOpen(false)} count={displayCount} close={() => setFilterOpen(false)} />}
    {detail && <DetailSheet event={detail} saved={saved.some(item => item.id === detail.id)} toggleSave={() => void toggleSave(detail)} saving={authStatus === 'pending' || authStatus === 'error' || savingIds.includes(detail.id)} close={() => setDetail(null)} />}
    <span className="sr-only">{maxApp() ? 'Открыто в MAX' : 'Открыто в браузере'}</span>
  </div>;
}
