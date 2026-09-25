import { useEffect, useRef, useState } from 'react'
import { CalendarDays, ChevronRight, Download, ListChecks, LogOut, PanelLeftClose, PanelLeftOpen, Search, Tags, UserRound, type LucideIcon } from 'lucide-react'

export type Section = 'events' | 'review' | 'tags' | 'imports'

const groups: Array<{ id: string; title: string; items: Array<{ section: Section; label: string; icon: LucideIcon; count?: number }> }> = [
  { id: 'workspace-links', title: 'Управление', items: [
    { section: 'events', label: 'Мероприятия', icon: CalendarDays },
    { section: 'tags', label: 'Теги', icon: Tags },
    { section: 'imports', label: 'Импорт', icon: Download },
  ] },
  { id: 'insights-links', title: 'Проверка', items: [
    { section: 'review', label: 'Проверка тегов', icon: ListChecks },
  ] },
]

const routes: Record<Section, string> = {
  events: '/events', review: '/review', tags: '/tags', imports: '/imports',
}

export function sectionFromPath(): Section {
  if (location.pathname === '/integrations') history.replaceState({}, '', '/events')
  return (Object.entries(routes).find(([, path]) => location.pathname === path)?.[0] as Section) || 'events'
}

export function routeFor(section: Section): string { return routes[section] }

export function Sidebar({ active, navigate, logout }: { active: Section; navigate: (section: Section) => void; logout: () => void }) {
  const [collapsed, setCollapsed] = useState(() => {
    try { const saved = localStorage.getItem('eventhub.sidebar.collapsed'); return saved === null ? window.innerWidth < 800 : saved === 'true' }
    catch { return window.innerWidth < 800 }
  })
  const [expanded, setExpanded] = useState<Record<string, boolean>>({ 'workspace-links': true, 'insights-links': true })
  const [search, setSearch] = useState('')
  const searchRef = useRef<HTMLInputElement>(null)

  useEffect(() => { try { localStorage.setItem('eventhub.sidebar.collapsed', String(collapsed)) } catch { /* storage may be disabled */ } }, [collapsed])
  function toggleRail() { setCollapsed(old => { if (!old) setSearch(''); return !old }) }
  function openSearch() { setCollapsed(false); requestAnimationFrame(() => searchRef.current?.focus()) }

  return <aside data-collapsed={collapsed} className={`sidebar-rail sticky top-0 z-20 flex h-screen shrink-0 flex-col border-r border-line bg-white transition-[width] duration-200 ${collapsed ? 'w-[72px]' : 'w-[260px]'}`}>
    <div className={`flex h-19 items-center border-b border-line ${collapsed ? 'justify-center px-3' : 'justify-between px-4'}`}>
      <div className="flex min-w-0 items-center gap-3">
        <div className="grid size-9 shrink-0 place-items-center rounded-lg bg-ink font-mono text-lg font-semibold text-white">E</div>
        <div data-label className="min-w-0"><div data-label className="truncate text-sm font-semibold tracking-tight text-ink">EventHub</div><div data-label className="text-[10px] font-medium uppercase tracking-[.18em] text-muted">Админ</div></div>
      </div>
      <button type="button" onClick={toggleRail} className={`grid size-8 shrink-0 place-items-center rounded-md border border-line text-muted hover:bg-soft ${collapsed ? 'absolute top-[84px] left-5' : ''}`} aria-label={collapsed ? 'Развернуть панель' : 'Свернуть панель'} title={collapsed ? 'Развернуть панель' : 'Свернуть панель'}>
        {collapsed ? <PanelLeftOpen size={17} strokeWidth={1.9} aria-hidden="true" /> : <PanelLeftClose size={17} strokeWidth={1.9} aria-hidden="true" />}
      </button>
    </div>

    <div className={`px-3 pt-5 ${collapsed ? 'mt-9' : ''}`}>
      <div className="sidebar-search relative">
        <Search size={17} strokeWidth={1.9} aria-hidden="true" className={`pointer-events-none absolute top-1/2 -translate-y-1/2 text-muted ${collapsed ? 'left-1/2 -translate-x-1/2' : 'left-3'}`} />
        <input ref={searchRef} data-label type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Поиск раздела" aria-label="Поиск раздела" className="h-10 w-full rounded-lg border border-line bg-soft pl-9 pr-3 text-xs text-ink outline-none placeholder:text-muted focus:border-accent" />
        {collapsed && <button type="button" onClick={openSearch} className="absolute inset-0" aria-label="Поиск раздела" title="Поиск раздела" />}
      </div>
    </div>

    <nav aria-label="Разделы" className="mt-6 min-h-0 flex-1 space-y-6 overflow-y-auto px-3 pb-5">
      {groups.map(group => {
        const matches = group.items.filter(item => `${item.label} ${group.title}`.toLowerCase().includes(search.toLowerCase().trim()))
        if (!matches.length) return null
        return <section key={group.id}>
          <button type="button" data-group={group.id} aria-controls={group.id} aria-expanded={expanded[group.id]} onClick={() => setExpanded(old => ({ ...old, [group.id]: !old[group.id] }))} className="group-header mb-2 flex w-full items-center justify-between rounded-md px-3 py-1 text-left text-[11px] font-semibold uppercase tracking-[.13em] text-muted hover:text-ink">
            <span data-label>{group.title}</span><ChevronRight data-label size={14} strokeWidth={1.9} aria-hidden="true" className={`transition-transform ${expanded[group.id] ? 'rotate-90' : ''}`} />
          </button>
          <div id={group.id} className={`space-y-1 ${expanded[group.id] || collapsed ? '' : 'hidden'}`}>
            {matches.map(item => <a key={item.section} href={routes[item.section]} aria-current={active === item.section ? 'page' : undefined} title={item.label} onClick={e => { e.preventDefault(); navigate(item.section) }} className={`nav-row flex h-10 items-center gap-3 rounded-lg border-l-[3px] px-3 text-sm transition-colors ${active === item.section ? 'border-ink bg-soft font-semibold text-ink' : 'border-transparent text-muted hover:bg-soft hover:text-ink'}`}>
              <span aria-hidden="true" className="nav-glyph grid w-5 shrink-0 place-items-center"><item.icon size={18} strokeWidth={1.9} /></span><span data-label className="min-w-0 flex-1 truncate">{item.label}</span>{item.count !== undefined && <span data-label className="rounded-md border border-neutral-200 bg-white px-1.5 py-0.5 text-[10px] text-neutral-500">{item.count}</span>}
            </a>)}
          </div>
        </section>
      })}
    </nav>

    <div className="border-t border-line p-3">
      <div className="flex items-center gap-3 rounded-lg p-2 hover:bg-soft">
        <div className="grid size-9 shrink-0 place-items-center rounded-full bg-soft text-ink" aria-hidden="true"><UserRound size={17} strokeWidth={1.9} /></div>
        <div data-label className="min-w-0 flex-1"><div data-label className="truncate text-xs font-semibold text-ink">Администратор</div><div data-label className="truncate text-[11px] text-muted">EventHub</div></div>
        <button type="button" onClick={logout} aria-label="Выйти" title="Выйти" className="logout-button grid size-7 place-items-center rounded-md text-muted hover:bg-soft hover:text-ink"><LogOut size={16} strokeWidth={1.9} aria-hidden="true" /></button>
      </div>
    </div>
  </aside>
}
