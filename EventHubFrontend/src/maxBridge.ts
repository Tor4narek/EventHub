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
  return app?.initData ? app : undefined;
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
