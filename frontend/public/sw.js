// Service worker-ul site-ului (în aplicațiile desktop/mobil fișierele sunt deja pe dispozitiv, deci nu e folosit acolo).
// Ca site-ul să se deschidă și fără internet pentru elevii cu lecții descărcate:
//   - /_next/static/... (codul și stilurile, cu nume unice la fiecare build): din cache, apoi din rețea;
//   - paginile și datele lor de navigare (RSC): din rețea, cu ultima copie păstrată ca rezervă.
// Nimic de la /api: datele elevului stau în IndexedDB (lib/offline) și se șterg la ieșirea din cont.
const STATIC = 'telebac-static-v2'
const PAGINI = 'telebac-pagini-v2'

self.addEventListener('install', () => self.skipWaiting())
self.addEventListener('activate', (event) => event.waitUntil((async () => {
  for (const k of await caches.keys()) if (![STATIC, PAGINI].includes(k)) await caches.delete(k)
  await self.clients.claim()
})()))

self.addEventListener('fetch', (event) => {
  const u = new URL(event.request.url)
  if (u.origin !== self.location.origin || event.request.method !== 'GET' || u.pathname.startsWith('/api/')) return
  if (u.pathname.startsWith('/_next/static/') || /\.(png|svg|woff2?|ico)$/.test(u.pathname)) {
    event.respondWith(caches.open(STATIC).then(async (c) => (await c.match(event.request)) || fetch(event.request).then((r) => {
      if (r.ok) c.put(event.request, r.clone())
      return r
    })))
    return
  }
  event.respondWith((async () => {
    const c = await caches.open(PAGINI)
    try {
      const r = await fetch(event.request)
      if (r.ok && r.type === 'basic') c.put(event.request, r.clone())
      return r
    } catch (e) {
      // O pagină cu alt ?id= (altă lecție) are același cod: oricare copie a ei merge, datele vin din IndexedDB.
      const copie = (await c.match(event.request)) || (await c.match(event.request, { ignoreSearch: true }))
      if (copie) return copie
      if (event.request.mode === 'navigate') { const acasa = await c.match('/'); if (acasa) return acasa }
      throw e
    }
  })())
})

self.addEventListener('push', (event) => {
  let data = {}
  try { data = event.data?.json() ?? {} } catch { /* Keep a generic message for malformed payloads. */ }
  event.waitUntil(self.registration.showNotification('TeleBac', { body: 'Ai o notificare nouă în aplicație.', data: { url: data.url || '/' } }))
})
self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = new URL(event.notification.data?.url || '/', self.location.origin)
  const safe = url.origin === self.location.origin && ['/', '/teme', '/organizatie', '/simulari', '/setari'].includes(url.pathname)
  event.waitUntil(self.clients.openWindow(safe ? url.href : self.location.origin))
})
