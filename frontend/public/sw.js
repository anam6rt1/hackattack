// Cache only the public offline shell. Lesson text is saved explicitly in IndexedDB.
const STATIC_CACHE = 'telebac-offline-shell-v1'
const STATIC_FILES = ['/offline.html', '/offline-page.js', '/offline-store.js', '/icon-192.png', '/icon-512.png']
self.addEventListener('install', event => event.waitUntil(caches.open(STATIC_CACHE).then(c => c.addAll(STATIC_FILES))))
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()))
self.addEventListener('fetch', event => {
  const u = new URL(event.request.url)
  if (u.origin !== self.location.origin || event.request.method !== 'GET' || u.pathname.startsWith('/api/')) return
  if (STATIC_FILES.includes(u.pathname)) { event.respondWith(caches.open(STATIC_CACHE).then(async c => (await c.match(u.pathname)) || fetch(event.request))); return }
  if (event.request.mode === 'navigate') event.respondWith(fetch(event.request).catch(() => caches.match('/offline.html')))
})
self.addEventListener('push', event => {
  let data = {}
  try { data = event.data?.json() ?? {} } catch { /* Keep a generic message for malformed payloads. */ }
  event.waitUntil(self.registration.showNotification('TeleBac', { body: 'Ai o notificare nouă în aplicație.', data: { url: data.url || '/' } }))
})
self.addEventListener('notificationclick', event => {
  event.notification.close()
  const url = new URL(event.notification.data?.url || '/', self.location.origin)
  const safe = url.origin === self.location.origin && ['/', '/teme', '/organizatie', '/simulari', '/setari'].includes(url.pathname)
  event.waitUntil(self.clients.openWindow(safe ? url.href : self.location.origin))
})
