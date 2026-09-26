type MaxBackButton = {
  show(): void;
  hide(): void;
  onClick(callback: () => void): void;
  offClick(callback: () => void): void;
};

type MaxWebApp = {
  initData?: string;
  close?: () => void;
  BackButton?: MaxBackButton;
  getViewportSize?: () => Promise<{ height: string; width: string }>;
  openLink?: (url: string) => void;
};

declare global {
  interface Window { WebApp?: MaxWebApp }
}

export function maxApp() {
  const app = window.WebApp;
  return maxInitData() ? app : undefined;
}

export function maxInitData() {
  return window.WebApp?.initData || new URLSearchParams(window.location.hash.slice(1)).get('WebAppData') || '';
}

export function openExternal(url: string) {
  if (!/^https?:\/\//i.test(url)) return;
  if (maxApp()?.openLink) maxApp()!.openLink!(url);
  else window.open(url, '_blank', 'noopener,noreferrer');
}

export function observeMaxBack(active: boolean, callback: () => void) {
  const back = maxApp()?.BackButton;
  if (!back) return () => {};
  if (active) {
    back.show();
    back.onClick(callback);
  } else back.hide();
  return () => {
    if (active) back.offClick(callback);
    back.hide();
  };
}

// iOS changes the visible viewport independently of the layout viewport (keyboard, MAX chrome).
export function observeMaxViewport() {
  let mounted = true;
  let revision = 0;
  const viewport = window.visualViewport;
  const style = document.documentElement.style;
  const apply = (height: number, top = 0) => {
    if (!mounted || !Number.isFinite(height) || height <= 0) return;
    style.setProperty('--max-viewport-height', `${height}px`);
    style.setProperty('--max-viewport-top', `${top}px`);
  };
  const sync = () => {
    const current = ++revision;
    if (viewport && viewport.height > 0 && viewport.scale === 1) {
      // Clamp stale iOS offsets after closing the keyboard.
      const top = Math.max(0, Math.min(viewport.offsetTop, window.innerHeight - viewport.height));
      apply(viewport.height, top);
      return;
    }
    apply(window.innerHeight);
    if (viewport) return; // Keep browser zoom available without resizing the app to the zoomed rectangle.
    void maxApp()?.getViewportSize?.().then(size => {
      const height = /^\d+(?:\.\d+)?(?:px)?$/.test(String(size.height)) ? Number.parseFloat(size.height) : NaN;
      if (mounted && current === revision && height > 0) apply(Math.min(height, window.innerHeight));
    }).catch(() => {});
  };
  sync();
  window.addEventListener('resize', sync);
  viewport?.addEventListener('resize', sync);
  viewport?.addEventListener('scroll', sync);
  return () => {
    mounted = false;
    revision++;
    window.removeEventListener('resize', sync);
    viewport?.removeEventListener('resize', sync);
    viewport?.removeEventListener('scroll', sync);
    style.removeProperty('--max-viewport-height');
    style.removeProperty('--max-viewport-top');
  };
}
