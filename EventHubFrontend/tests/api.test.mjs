import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getMe, getSavedEvents, saveEvent, removeSavedEvent, saveInterests, saveSettings, getRecommendations, loginWithMax, getEvents, getTags } from '../src/api.ts';
import { maxInitData } from '../src/maxBridge.ts';

globalThis.window = { setTimeout, clearTimeout, location: { hash: '' } };

test('MAX session and all shared profile operations use the existing API contracts', async () => {
  const requests = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (url, options) => {
    requests.push({ url, ...options });
    return options.method === 'POST' && url === '/api/auth/max'
      ? Response.json({ accessToken: 'profile-token', expiresAt: '2026-09-27T00:00:00Z' })
      : ['POST', 'PUT', 'PATCH', 'DELETE'].includes(options.method)
        ? new Response(null, { status: 204 }) : Response.json([]);
  };
  try {
    const session = await loginWithMax('signed-init-data');
    await getMe(session.accessToken);
    await getSavedEvents(session.accessToken);
    await saveEvent('event-1', session.accessToken);
    await removeSavedEvent('event-1', session.accessToken);
    await saveInterests(['tag-1'], session.accessToken);
    await saveSettings(false, session.accessToken);
    await getRecommendations(session.accessToken);
    assert.deepEqual(requests.map(r => [r.method, r.url]), [
      ['POST', '/api/auth/max'], ['GET', '/api/me'], ['GET', '/api/me/saved-events'],
      ['POST', '/api/me/saved-events/event-1'], ['DELETE', '/api/me/saved-events/event-1'],
      ['PUT', '/api/me/interests'], ['PATCH', '/api/me/settings'], ['GET', '/api/me/recommendations?limit=3'],
    ]);
    assert.deepEqual(JSON.parse(requests[0].body), { initData: 'signed-init-data' });
    for (const request of requests.slice(1)) assert.equal(request.headers.Authorization, 'Bearer profile-token');
    assert.deepEqual(JSON.parse(requests[5].body), { tagIds: ['tag-1'] });
    assert.deepEqual(JSON.parse(requests[6].body), { isWeeklyDigestEnabled: false });
    requests.length = 0;
    await getEvents(1, { allDates: true });
    await getTags();
    for (const request of requests) assert.equal(request.headers.Authorization, undefined);
  } finally { globalThis.fetch = originalFetch; }
});

test('failed server save rejects instead of reporting local success', async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => Response.json({ detail: 'Мероприятие уже началось.' }, { status: 409 });
  try { await assert.rejects(saveEvent('event-1', 'token'), /Мероприятие уже началось/); }
  finally { globalThis.fetch = originalFetch; }
});

test('MAX identity uses signed initData from bridge or launch fragment', () => {
  const signed = 'user=%7B%22id%22%3A42%7D&auth_date=123&hash=signature';
  window.location.hash = `#WebAppData=${encodeURIComponent(signed)}&WebAppPlatform=web`;
  assert.equal(maxInitData(), signed);
  window.WebApp = { initData: 'bridge-signed-data', initDataUnsafe: { user: { id: 999 } } };
  assert.equal(maxInitData(), 'bridge-signed-data');
  delete window.WebApp;
  window.location.hash = '';
  assert.equal(maxInitData(), '');
});
