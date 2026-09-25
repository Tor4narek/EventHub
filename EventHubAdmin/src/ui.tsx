import type { ButtonHTMLAttributes, HTMLAttributes, InputHTMLAttributes, TextareaHTMLAttributes } from 'react'

const cx = (...parts: Array<string | undefined | false>) => parts.filter(Boolean).join(' ')

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'destructive' | 'ghost'
  size?: 'small' | 'medium'
  stretched?: boolean
  loading?: boolean
}

export function Button({ variant = 'secondary', size = 'medium', stretched, loading, disabled, className, children, ...props }: ButtonProps) {
  const variants = {
    primary: 'border-accent bg-accent text-white hover:border-[#4637e8] hover:bg-[#4637e8]',
    secondary: 'border-line bg-white text-ink hover:bg-soft',
    destructive: 'border-pink/25 bg-white text-pink hover:bg-pink/5',
    ghost: 'border-transparent bg-transparent text-muted hover:bg-soft',
  }
  return <button className={cx('inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl border font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50', size === 'small' ? 'min-h-8 px-3 text-xs' : 'min-h-11 px-4 text-sm', variants[variant], stretched && 'w-full', className)} disabled={disabled || loading} {...props}>
    {loading && <Spinner size={14} />}<span>{children}</span>
  </button>
}

export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cx('min-h-11 w-full min-w-0 rounded-xl border border-line bg-soft px-3 text-sm text-ink outline-none placeholder:text-muted focus:border-accent focus:ring-2 focus:ring-accent/10 disabled:opacity-60', className)} {...props} />
}

export function Textarea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={cx('w-full min-w-0 resize-y rounded-xl border border-line bg-soft px-3 py-2.5 text-sm text-ink outline-none placeholder:text-muted focus:border-accent focus:ring-2 focus:ring-accent/10 disabled:opacity-60', className)} {...props} />
}

export function Panel({ mode: _mode, className, ...props }: HTMLAttributes<HTMLDivElement> & { mode?: 'primary' | 'secondary' }) {
  return <div className={cx('rounded-2xl border border-line bg-white', className)} {...props} />
}

export function Spinner({ size = 20 }: { size?: number }) {
  return <span role="progressbar" aria-label="Загрузка" className="inline-block animate-spin rounded-full border-2 border-current border-r-transparent" style={{ width: size, height: size }} />
}
