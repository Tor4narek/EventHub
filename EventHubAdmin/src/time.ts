const zone = 'Europe/Moscow'
const formatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
})

export function toMoscowInput(iso: string | null): string {
  if (!iso) return ''
  const parts = Object.fromEntries(formatter.formatToParts(new Date(iso)).map(p => [p.type, p.value]))
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`
}

export function fromMoscowInput(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value)
  if (!match) throw new Error('Укажите дату и время.')
  const [, year, month, day, hour, minute] = match.map(Number)
  const wall = Date.UTC(year, month - 1, day, hour, minute)
  let instant = wall
  // Find the UTC offset from the IANA timezone, including any future rule changes.
  for (let i = 0; i < 2; i++) {
    const parts = Object.fromEntries(formatter.formatToParts(new Date(instant)).map(p => [p.type, Number(p.value)]))
    const observedWall = Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute)
    instant += wall - observedWall
  }
  return new Date(instant).toISOString()
}

export function displayDate(iso: string | null): string {
  if (!iso) return '—'
  return new Intl.DateTimeFormat('ru-RU', { timeZone: zone, day: '2-digit', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(iso)) + ' МСК'
}
