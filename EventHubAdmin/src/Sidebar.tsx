import { useEffect, useState } from 'react'
import { CalendarDays, Download, ListChecks, LogOut, PanelLeftClose, PanelLeftOpen, Tags, UserRound } from 'lucide-react'
import { Icon, IconButton } from './ui'

export type Section = 'events' | 'review' | 'tags' | 'imports'
const items = [
  { section: 'events' as const, label: 'Мероприятия', icon: CalendarDays },
  { section: 'tags' as const, label: 'Теги', icon: Tags },
  { section: 'review' as const, label: 'Проверка тегов', icon: ListChecks },
  { section: 'imports' as const, label: 'Импорт', icon: Download },
]
const routes: Record<Section, string> = { events: '/events', review: '/review', tags: '/tags', imports: '/imports' }
export function sectionFromPath(): Section {
  if (location.pathname === '/integrations') history.replaceState({}, '', '/events')
  return (Object.entries(routes).find(([, path]) => location.pathname === path)?.[0] as Section) || 'events'
}
export function routeFor(section: Section): string { return routes[section] }
export function Sidebar({ active, navigate, logout }: { active: Section; navigate: (section: Section) => void; logout: () => void }) {
  const [collapsed, setCollapsed] = useState(() => {
    try { const saved = localStorage.getItem('eventhub.sidebar.collapsed'); return window.innerWidth < 800 || saved === 'true' }
    catch { return window.innerWidth < 800 }
  })
  useEffect(() => { try { localStorage.setItem('eventhub.sidebar.collapsed', String(collapsed)) } catch { /* Storage may be disabled. */ } }, [collapsed])
  useEffect(() => {
    function close(e: KeyboardEvent) { if (e.key === 'Escape' && window.innerWidth < 800) setCollapsed(true) }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [])
  return <>
    {!collapsed && <button className="sidebar-backdrop" aria-label="Свернуть навигацию" onClick={() => setCollapsed(true)} />}
    <aside data-collapsed={collapsed} className={`sidebar-rail sticky top-0 z-20 flex h-screen shrink-0 flex-col border-r border-line bg-white ${collapsed ? 'w-[72px]' : 'w-[260px]'}`}>
      <div className="flex h-19 items-center justify-center gap-3 border-b border-line px-4">
        <div className="grid size-9 shrink-0 place-items-center rounded-xl bg-ink text-lg font-semibold text-white">E</div>
        <div data-label className="flex-1"><div className="text-sm font-semibold">EventHub</div><div className="text-[10px] uppercase tracking-widest text-muted">Админ</div></div>
      </div>
      <div className={`flex p-3 ${collapsed ? 'justify-center' : 'justify-end'}`}><IconButton variant="ghost" icon={collapsed ? PanelLeftOpen : PanelLeftClose} label={collapsed ? 'Развернуть навигацию' : 'Свернуть навигацию'} aria-expanded={!collapsed} aria-controls="admin-navigation" onClick={() => setCollapsed(old => !old)} /></div>
      <nav id="admin-navigation" aria-label="Разделы" className="flex-1 space-y-2 px-3">
        {items.map(item => <a key={item.section} href={routes[item.section]} title={item.label} aria-label={item.label} aria-current={active === item.section ? 'page' : undefined} onClick={e => { e.preventDefault(); navigate(item.section); if (window.innerWidth < 800) setCollapsed(true) }} className={`nav-row flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm transition-colors ${active === item.section ? 'bg-ink font-semibold text-white' : 'text-muted hover:bg-soft hover:text-ink'}`}>
          <Icon icon={item.icon} /><span data-label>{item.label}</span>
        </a>)}
      </nav>
      <div className="flex flex-col gap-3 border-t border-line p-3">
        <div data-label className="flex items-center gap-3 px-2 text-xs text-muted"><Icon icon={UserRound} /><span>Администратор</span></div>
        <div className={collapsed ? 'flex justify-center' : 'flex items-center gap-3 px-2'}><IconButton icon={LogOut} label="Выйти из аккаунта" variant="ghost" onClick={logout} /><span data-label className="text-xs text-muted">Выйти</span></div>
      </div>
    </aside>
  </>
}
