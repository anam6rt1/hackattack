// Clientul pentru API-ul .NET (database/src/MarkAi.Api).
// Adresa vine din .env.local: NEXT_PUBLIC_API_URL=/api (prin redirecționarea din next.config.ts)
// sau direct, de ex. NEXT_PUBLIC_API_URL=http://localhost:5080
// Sesiunea e un cookie httpOnly pus de API, de aceea fiecare cerere trimite `credentials: 'include'`.
// Antetul X-MarkAi: 1 dovedește API-ului că cererea vine din site (un formular de pe alt site nu îl poate adăuga).

export const API = (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5080').replace(/\/$/, '')

// date = corpul răspunsului de eroare (de ex. { cod: 'email_neconfirmat', email } la intrarea în cont)
export class EroareApi extends Error {
  constructor(status, mesaj, date = null) {
    super(mesaj)
    this.status = status
    this.date = date
  }
}

// corp = obiect trimis ca JSON; formular = FormData (pentru fișiere)
export async function api(cale, { metoda = 'GET', corp, formular } = {}) {
  let raspuns
  try {
    raspuns = await fetch(API + cale, {
      method: metoda,
      credentials: 'include',
      headers: { 'X-MarkAi': '1', ...(corp !== undefined ? { 'Content-Type': 'application/json' } : {}) },
      body: formular ?? (corp !== undefined ? JSON.stringify(corp) : undefined),
    })
  } catch {
    throw new EroareApi(0, 'Nu ne putem conecta la server. Verifică internetul și încearcă din nou.')
  }
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
