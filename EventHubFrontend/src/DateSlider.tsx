import { useEffect, useRef, useState } from 'react';

const step = 60;
const weekday = new Intl.DateTimeFormat('ru-RU', { weekday: 'short' });
const label = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' });
const dateKey = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
const dateAt = (today: string, offset: number) => {
  const date = new Date(`${today}T12:00:00`);
  date.setDate(date.getDate() + offset);
  return date;
};

export function DateSlider({ today, anchor, value, onSelect }: { today: string; anchor: string; value: string | null; onSelect: (day: string) => void }) {
  const track = useRef<HTMLDivElement>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const userScroll = useRef(false);
  const touching = useRef(false);
  const select = useRef(onSelect);
  select.current = onSelect;
  const [count, setCount] = useState(60);
  const [center, setCenter] = useState(0);
  const target = Math.max(0, Math.round((new Date(`${anchor}T12:00:00`).getTime() - new Date(`${today}T12:00:00`).getTime()) / 86400000));
  const renderedCount = Math.max(count, target + 31);

  function settle() {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    if (touching.current) return;
    const element = track.current;
    if (!element) return;
    const index = Math.max(0, Math.min(renderedCount - 1, Math.round(element.scrollLeft / step)));
    setCenter(index);
    if (userScroll.current) {
      userScroll.current = false;
      select.current(dateKey(dateAt(today, index)));
    }
  }

  useEffect(() => {
    const element = track.current;
    if (!element) return;
    userScroll.current = false;
    const left = target * step;
    if (Math.abs(element.scrollLeft - left) > 1) {
      const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
      element.scrollTo({ left, behavior: reduce ? 'instant' : 'smooth' });
    }
    setCenter(target);
  }, [target, today]);

  useEffect(() => {
    const element = track.current;
    if (!element) return;
    // scrollend is supplemented with a debounce for older iOS WebViews.
    element.addEventListener('scrollend', settle);
    return () => {
      element.removeEventListener('scrollend', settle);
    };
  }, [today, renderedCount]);
  useEffect(() => () => { if (timer.current) clearTimeout(timer.current); }, []);

  function scroll() {
    const element = track.current;
    if (!element) return;
    const index = Math.max(0, Math.min(renderedCount - 1, Math.round(element.scrollLeft / step)));
    setCenter(index);
    if (index > renderedCount - 20) setCount(renderedCount + 60);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(settle, 180);
  }

  return <div className="date-slider-window">
    <div className="date-slider" ref={track} role="group" tabIndex={0} aria-label="Выбор даты. Прокрутите дни; дата в центре выбирается после остановки."
      onTouchStart={() => { touching.current = true; userScroll.current = true; }}
      onTouchEnd={() => { touching.current = false; if (timer.current) clearTimeout(timer.current); timer.current = setTimeout(settle, 180); }}
      onTouchCancel={() => { touching.current = false; if (timer.current) clearTimeout(timer.current); timer.current = setTimeout(settle, 180); }}
      onScroll={scroll} onPointerDown={() => { userScroll.current = true; }} onWheel={() => { userScroll.current = true; }}
      onKeyDown={event => {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight' && event.key !== 'Home') return;
        event.preventDefault();
        const index = event.key === 'Home' ? 0 : Math.max(0, Math.min(renderedCount - 1, Math.round(event.currentTarget.scrollLeft / step) + (event.key === 'ArrowRight' ? 1 : -1)));
        userScroll.current = true;
        const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
        if (Math.abs(event.currentTarget.scrollLeft - index * step) < 1) settle();
        else event.currentTarget.scrollTo({ left: index * step, behavior: reduce ? 'instant' : 'smooth' });
      }}>
      {Array.from({ length: renderedCount }, (_, index) => {
        const date = dateAt(today, index);
        const day = dateKey(date);
        return <button key={day} className={`day${center === index ? ' centered' : ''}${value === day ? ' active' : ''}`}
          aria-label={label.format(date)} aria-pressed={value === day}
          onClick={() => { userScroll.current = false; if (timer.current) clearTimeout(timer.current); const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches; track.current?.scrollTo({ left: index * step, behavior: reduce ? 'instant' : 'smooth' }); setCenter(index); select.current(day); }}>
          <b>{date.getDate()}</b><span>{weekday.format(date).replace('.', '')}</span>
        </button>;
      })}
    </div>
    <p className="date-slider-hint">Прокрутите до нужной даты</p>
  </div>;
}
