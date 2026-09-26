import { Children, cloneElement, createContext, useContext, isValidElement, useEffect, useId, useRef, useState, type ReactNode, type ReactElement, type ButtonHTMLAttributes, type HTMLAttributes, type InputHTMLAttributes, type TextareaHTMLAttributes, type OptionHTMLAttributes } from 'react'
import { AlertCircle, ArrowLeft, ArrowRight, CheckCircle2, Check, ChevronDown, Inbox, X, type LucideIcon } from 'lucide-react'

const cx = (...parts: Array<string | undefined | false>) => parts.filter(Boolean).join(' ')
export function Icon({ icon: Glyph, size = 20, className }: { icon: LucideIcon; size?: number; className?: string }) {
  return <Glyph size={size} strokeWidth={1.8} aria-hidden="true" className={cx('ui-icon', className)} />
}
type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'destructive' | 'ghost'; size?: 'small' | 'medium'; stretched?: boolean; loading?: boolean; icon?: LucideIcon }
export function Button({ variant = 'secondary', size = 'medium', stretched, loading, disabled, className, children, icon, type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={cx('ui-button', `ui-button--${variant}`, `ui-button--${size}`, stretched && 'ui-button--stretched', className)} disabled={disabled || loading} aria-busy={loading || undefined} {...props}>
    {icon && <span className="button-icon">{loading ? <Spinner size={16} /> : <Icon icon={icon} size={16} />}</span>}
    <span className={cx('button-label', loading && !icon && 'button-label--loading')}>{children}</span>
    {loading && !icon && <span className="button-spinner"><Spinner size={16} /></span>}
  </button>
}
export function IconButton({ icon, label, ...props }: Omit<ButtonProps, 'children'> & { icon: LucideIcon; label: string }) {
  return <Button {...props} className={cx('icon-button', props.className)} aria-label={label} title={label} icon={icon} />
}
export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) { return <input className={cx('ui-input', className)} {...props} /> }
export function Textarea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) { return <textarea className={cx('ui-input ui-textarea', className)} {...props} /> }
export function Select({ value, onValueChange, children, label }: { value: string; onValueChange: (value: string) => void; children: ReactNode; label: string }) {
  const id = useId()
  const ref = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const options = Children.toArray(children).filter(isValidElement).map(child => (child as ReactElement<OptionHTMLAttributes<HTMLOptionElement>>).props)
  const selected = Math.max(0, options.findIndex(option => String(option.value ?? '') === value))
  useEffect(() => {
    if (!open) return
    function outside(e: PointerEvent) { if (!ref.current?.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('pointerdown', outside)
    return () => document.removeEventListener('pointerdown', outside)
  }, [open])
  function choose(index: number) { onValueChange(String(options[index].value ?? '')); setOpen(false); trigger.current?.focus() }
  return <div ref={ref} className="ui-select">
    <button ref={trigger} type="button" role="combobox" aria-label={label} aria-haspopup="listbox" aria-expanded={open} aria-controls={`${id}-options`} aria-activedescendant={open ? `${id}-${active}` : undefined} className="ui-input select-trigger" onClick={() => { setActive(selected); setOpen(old => !old) }} onKeyDown={e => {
      if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(e.key)) {
        e.preventDefault()
        if (!open) { setActive(selected); setOpen(true) }
        else setActive(old => e.key === 'Home' ? 0 : e.key === 'End' ? options.length - 1 : Math.max(0, Math.min(options.length - 1, old + (e.key === 'ArrowDown' ? 1 : -1))))
      } else if (open && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); choose(active) }
      else if (e.key === 'Escape' && open) { e.preventDefault(); e.stopPropagation(); setOpen(false) }
      else if (e.key === 'Tab') setOpen(false)
    }}><span>{options[selected]?.children}</span><Icon icon={ChevronDown} size={16} className={open ? 'select-chevron open' : 'select-chevron'} /></button>
    {open && <div id={`${id}-options`} className="ui-select-options" role="listbox" aria-label={label}>{options.map((option, index) => <div key={String(option.value)} id={`${id}-${index}`} role="option" aria-selected={index === selected} className={`select-option ${index === active ? 'is-active' : ''}`} onPointerMove={() => setActive(index)} onMouseDown={e => e.preventDefault()} onClick={() => choose(index)}><span>{option.children}</span>{index === selected && <Icon icon={Check} size={16} />}</div>)}</div>}
  </div>
}
export function FormField({ label, hint, error, children }: { label: string; hint?: string; error?: string; children: ReactNode }) {
  const id = useId()
  const control = Children.only(children) as ReactElement<InputHTMLAttributes<HTMLInputElement>>
  return <div className="form-field"><label htmlFor={id}>{label}{control.props.required && <span className="required"> *</span>}</label>
    {isValidElement(control) && cloneElement(control, { id, 'aria-invalid': Boolean(error), 'aria-describedby': error || hint ? `${id}-help` : undefined })}
    {(error || hint) && <p id={`${id}-help`} className={error ? 'field-error' : 'hint'}>{error || hint}</p>}
  </div>
}
export function Panel({ mode: _mode, className, ...props }: HTMLAttributes<HTMLDivElement> & { mode?: 'primary' | 'secondary' }) { return <div className={cx('ui-panel', className)} {...props} /> }
export function Spinner({ size = 20 }: { size?: number }) { return <span role="progressbar" aria-label="Загрузка" className="ui-spinner" style={{ width: size, height: size }} /> }
export function Badge({ tone = 'draft', children }: { tone?: 'draft' | 'live' | 'confirmed' | 'pending'; children: ReactNode }) { return <span className={`badge ${tone}`}>{children}</span> }
export function Chip({ active, children, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { active?: boolean }) { return <button type="button" {...props} className="filter-chip" aria-pressed={Boolean(active)}>{children}</button> }
export function Alert({ text, onClose, success = false, onRetry }: { text: string; onClose?: () => void; success?: boolean; onRetry?: () => void }) {
  return <div className={`alert ${success ? 'success' : 'notice'}`} role={success ? 'status' : 'alert'}><Icon icon={success ? CheckCircle2 : AlertCircle} size={18} /><span>{text}</span>{onRetry && <Button size="small" onClick={onRetry}>Повторить</Button>}{onClose && <IconButton icon={X} label="Закрыть уведомление" variant="ghost" size="small" onClick={onClose} />}</div>
}
export function LoadingState({ compact = false }: { compact?: boolean }) { return <div className={`loading ${compact ? 'loading--compact' : ''}`} role="status"><Spinner size={compact ? 16 : 24} /><span>{compact ? 'Обновляем список…' : 'Загружаем данные…'}</span></div> }
export function EmptyState({ title, text, icon = Inbox, children }: { title: string; text: string; icon?: LucideIcon; children?: ReactNode }) { return <Panel className="empty"><div className="empty-icon"><Icon icon={icon} size={28} /></div><h3>{title}</h3><p>{text}</p>{children}</Panel> }
export function Pagination({ page, totalPages, hasNext, disabled, onChange }: { page: number; totalPages: number; hasNext: boolean; disabled?: boolean; onChange: (page: number) => void }) { return <nav className="pagination" aria-label="Страницы списка"><Button icon={ArrowLeft} disabled={disabled || page <= 1} onClick={() => onChange(page - 1)}>Назад</Button><span>Страница {page} из {totalPages}</span><Button icon={ArrowRight} disabled={disabled || !hasNext} onClick={() => onChange(page + 1)}>Вперёд</Button></nav> }

// A stack keeps Escape and focus inside the topmost dialog, including discard confirmation.
const dialogStack: HTMLElement[] = []
let bodyOverflow = ''
const CloseContext = createContext<() => void>(() => {})
export function ModalCancel({ disabled }: { disabled?: boolean }) { const close = useContext(CloseContext); return <Button disabled={disabled} onClick={close}>Отмена</Button> }
export function Modal({ children, label, onClose, drawer = false, dirty = false, busy = false }: { children: ReactNode; label: string; onClose: () => void; drawer?: boolean; dirty?: boolean; busy?: boolean }) {
  const ref = useRef<HTMLDivElement>(null)
  const [discard, setDiscard] = useState(false)
  const closeRef = useRef(() => {})
  closeRef.current = () => { if (!busy) { if (dirty) setDiscard(true); else onClose() } }
  useEffect(() => {
    const element = ref.current!
    const previous = document.activeElement as HTMLElement | null
    if (!dialogStack.length) bodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const underlying = dialogStack.at(-1)
    if (underlying) { underlying.inert = true; underlying.setAttribute('aria-hidden', 'true') }
    dialogStack.push(element)
    const initialFocus = element.querySelector<HTMLElement>('input, textarea, select') || element.querySelector<HTMLElement>('.dialog-actions button, button')
    initialFocus?.focus()
    function keydown(e: KeyboardEvent) {
      if (dialogStack.at(-1) !== element) return
      if (e.key === 'Escape') { e.preventDefault(); closeRef.current() }
      if (e.key === 'Tab') {
        const nodes = Array.from(element.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), select:not(:disabled), a[href], [tabindex="0"]')).filter(node => node.getClientRects().length)
        const first = nodes[0], last = nodes.at(-1)
        if (!first) { e.preventDefault(); element.focus() }
        else if (e.shiftKey && (document.activeElement === first || document.activeElement === element)) { e.preventDefault(); last?.focus() }
        else if (!e.shiftKey && (document.activeElement === last || !element.contains(document.activeElement))) { e.preventDefault(); first.focus() }
      }
    }
    function focusin(e: FocusEvent) { if (dialogStack.at(-1) === element && !element.contains(e.target as Node)) element.focus() }
    document.addEventListener('keydown', keydown)
    document.addEventListener('focusin', focusin)
    return () => {
      dialogStack.splice(dialogStack.indexOf(element), 1)
      const active = dialogStack.at(-1)
      if (active) { active.inert = false; active.removeAttribute('aria-hidden') }
      else document.body.style.overflow = bodyOverflow
      document.removeEventListener('keydown', keydown)
      document.removeEventListener('focusin', focusin)
      if (previous?.isConnected) previous.focus()
    }
  }, [])
  useEffect(() => {
    if (!dirty) return
    function unload(e: BeforeUnloadEvent) { e.preventDefault(); e.returnValue = '' }
    window.addEventListener('beforeunload', unload)
    return () => window.removeEventListener('beforeunload', unload)
  }, [dirty])
  return <><div className={`overlay ${drawer ? 'overlay--drawer' : ''}`} onMouseDown={e => { if (e.target === e.currentTarget) closeRef.current() }}><div ref={ref} tabIndex={-1} className={drawer ? 'drawer' : 'dialog ui-panel'} role="dialog" aria-modal="true" aria-label={label} aria-busy={busy || undefined}>
    <IconButton className="modal-close" icon={X} label="Закрыть" variant="ghost" disabled={busy} onClick={() => closeRef.current()} /><CloseContext.Provider value={() => closeRef.current()}>{children}</CloseContext.Provider>
  </div></div>{discard && <ConfirmDialog title="Закрыть без сохранения?" text="Изменения в форме будут потеряны." confirmLabel="Не сохранять" destructive onClose={() => setDiscard(false)} onConfirm={onClose} />}</>
}
export function ConfirmDialog({ title, text, confirmLabel, onConfirm, onClose, busy, destructive = false }: { title: string; text: string; confirmLabel: string; onConfirm: () => void; onClose: () => void; busy?: boolean; destructive?: boolean }) { return <Modal label={title} onClose={onClose} busy={busy}><h2>{title}</h2><p className="muted">{text}</p><div className="dialog-actions"><Button disabled={busy} onClick={onClose}>Отмена</Button><Button variant={destructive ? 'destructive' : 'primary'} loading={busy} onClick={onConfirm}>{confirmLabel}</Button></div></Modal> }
