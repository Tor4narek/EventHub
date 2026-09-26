import type { ReactNode } from 'react'
import { Sidebar, type Section } from './Sidebar'

const titles: Record<Section, string> = {
  events: 'Мероприятия', review: 'Проверка тегов', tags: 'Теги', imports: 'Импорт',
}

export function AppShell({ active, navigate, logout, children }: { active: Section; navigate: (section: Section) => void; logout: () => void; children: ReactNode }) {
  return <div className="flex min-h-screen bg-canvas font-sans text-ink">
    <Sidebar active={active} navigate={navigate} logout={logout} />
    <div className="min-w-0 flex-1">
      <header className="flex h-16 items-center justify-between border-b border-line bg-white px-5 md:px-8 xl:px-10">
        <nav aria-label="Навигационная цепочка" className="truncate text-xs text-muted">Панель управления <span className="mx-2 text-line">/</span> <strong className="font-semibold text-ink">{titles[active]}</strong></nav>
        <div className="hidden items-center gap-2 text-xs text-muted sm:flex">Панель администратора</div>
      </header>
      <main className="mx-auto w-full max-w-[1400px] p-5 md:p-8 xl:p-10">{children}</main>
    </div>
  </div>
}
