import { Check } from 'lucide-react'
import { Icon } from './ui'
import type { Tag } from './api'

export function TagSelector({ tags, selected, onChange }: { tags: Tag[]; selected: string[]; onChange: (ids: string[]) => void }) {
  return <div className="tag-selector">
    {tags.length ? tags.map(tag => <label className={`tag-choice ${selected.includes(tag.id) ? 'selected' : ''}`} key={tag.id}>
      <input type="checkbox" checked={selected.includes(tag.id)} onChange={e => onChange(e.target.checked ? [...selected, tag.id] : selected.filter(id => id !== tag.id))} /><Icon icon={Check} size={14} className="tag-check" />{tag.name}
    </label>) : <span className="muted">Тегов пока нет. Создайте их в разделе «Теги».</span>}
  </div>
}
