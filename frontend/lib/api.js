// Clientul pentru API-ul .NET (database/src/MarkAi.Api).
// Adresa vine din .env.local: NEXT_PUBLIC_API_URL=/api (prin redirecționarea din next.config.ts)
// sau direct, de ex. NEXT_PUBLIC_API_URL=http://localhost:5080
// Sesiunea e un cookie httpOnly pus de API, de aceea fiecare cerere trimite `credentials: 'include'`.
// Antetul X-MarkAi: 1 dovedește API-ului că cererea vine din site (un formular de pe alt site nu îl poate adăuga).

import { adresaApi } from './platforma'
import { areDateOffline } from './offline/depozit'
import { raporteazaServer } from './useOnline'

// Pe site: /api (sau adresa din .env.local); în aplicația mobilă: adresa serverului ales (vezi lib/platforma.js).
export const API = adresaApi()

// date = corpul răspunsului de eroare (de ex. { cod: 'email_neconfirmat', email } la intrarea în cont)
export class EroareApi extends Error {
  constructor(status, mesaj, date = null) {
    super(mesaj)
    this.status = status
    this.date = date
  }
}

// GET-uri identice pornite în același timp (de ex. meniul și pagina cer amândouă /progres) folosesc o singură cerere;
// fiecare apelant primește propria copie a datelor, ca modificarea uneia să nu le atingă pe celelalte.
// O modificare (intrare în cont, ieșire, salvare) poate schimba răspunsul: un GET cerut după ea nu primește unul pornit înainte.
const inCurs = new Map()
export const uitaCereriInCurs = () => inCurs.clear()

// Modul offline (lib/offline): testele și simulările începute fără internet au id negativ și rămân pe dispozitiv; cât timp
// nu e rețea, lecțiile descărcate și ultimele răspunsuri păstrate țin locul serverului. Codul lor se încarcă doar la nevoie.
const LOCAL = /\/(quiz|simulari)\/-\d+/
const offline = () => import('./offline/adaptor')
const faraRetea = () => typeof navigator !== 'undefined' && navigator.onLine === false

// corp = obiect trimis ca JSON; formular = FormData (pentru fișiere)
export async function api(cale, optiuni = {}) {
  if (LOCAL.test(cale) || (faraRetea() && areDateOffline())) return (await offline()).raspundeOffline(cale, optiuni)
  try {
    return await online(cale, optiuni)
  } catch (e) {
    if (e.status !== 0 || !areDateOffline()) throw e
    return (await offline()).raspundeOffline(cale, optiuni)
  }
}

async function online(cale, optiuni) {
  if ((optiuni.metoda ?? 'GET') !== 'GET') { uitaCereriInCurs(); return cerere(cale, optiuni) }
  let comuna = inCurs.get(cale)
  if (!comuna) {
    comuna = cerere(cale, optiuni).finally(() => { if (inCurs.get(cale) === comuna) inCurs.delete(cale) })
    inCurs.set(cale, comuna)
    if (areDateOffline()) comuna.then((d) => offline().then((o) => o.pastreaza(cale, d))).catch(() => {})
  }
  const date = await comuna
  return date === null || typeof date !== 'object' ? date : structuredClone(date)
}

async function cerere(cale, { metoda = 'GET', corp, formular } = {}) {
  let raspuns
  try {
    raspuns = await fetch(API + cale, {
      method: metoda,
      credentials: 'include',
      headers: { 'X-MarkAi': '1', ...(corp !== undefined ? { 'Content-Type': 'application/json' } : {}) },
      body: formular ?? (corp !== undefined ? JSON.stringify(corp) : undefined),
    })
  } catch {
    raporteazaServer(false)
    throw new EroareApi(0, 'Nu ne putem conecta la server. Verifică internetul și încearcă din nou.')
  }
  raporteazaServer(true)
  if (raspuns.status === 204) return null
  const date = await raspuns.json().catch(() => null)
  if (!raspuns.ok) {
    const implicit = raspuns.status === 401 ? 'Trebuie să intri în cont.' : raspuns.status === 403 ? 'Nu ai acces aici.' : 'A apărut o eroare. Încearcă din nou.'
    throw new EroareApi(raspuns.status, date?.mesaj || implicit, date)
  }
  return date
}

// Linkuri către fișiere servite de API (ex. poza unui profesor)
export const urlApi = (cale) => (cale ? API + cale : null)

// Mărimea unui fișier pentru oameni: „850 KB”, „12,4 MB”
export const marime = (o) => (o >= 1024 * 1024 ? `${(o / 1024 / 1024).toFixed(1).replace('.', ',')} MB` : `${Math.max(1, Math.round(o / 1024))} KB`)

// „https://www.youtube.com/watch?v=abc” sau „https://youtu.be/abc” → „abc”
export function idYoutube(url) {
  if (!url) return null
  try {
    const u = new URL(url)
    if (u.hostname === 'youtu.be') return u.pathname.slice(1) || null
    if (u.hostname.endsWith('youtube.com')) return u.searchParams.get('v') || u.pathname.split('/').pop() || null
  } catch {}
  return null
}

const FORMAT_DATA = new Intl.DateTimeFormat('ro-RO', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
export const dataOra = (iso) => (iso ? FORMAT_DATA.format(new Date(iso)) : '')
