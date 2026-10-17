// Clientul pentru API-ul .NET (database/src/MarkAi.Api).
// Adresa vine din .env.local: NEXT_PUBLIC_API_URL=http://localhost:5080
// Sesiunea e un cookie httpOnly pus de API, de aceea fiecare cerere trimite `credentials: 'include'`.

export const API = (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5080').replace(/\/$/, '')

export class EroareApi extends Error {
  constructor(status, mesaj) {
    super(mesaj)
    this.status = status
  }
}

// corp = obiect trimis ca JSON; formular = FormData (pentru fișiere)
export async function api(cale, { metoda = 'GET', corp, formular } = {}) {
  let raspuns
  try {
    raspuns = await fetch(API + cale, {
      method: metoda,
      credentials: 'include',
      headers: corp !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: formular ?? (corp !== undefined ? JSON.stringify(corp) : undefined),
    })
  } catch {
    throw new EroareApi(0, 'Nu ne putem conecta la server. Verifică internetul și încearcă din nou.')
  }
  if (raspuns.status === 204) return null
  const date = await raspuns.json().catch(() => null)
  if (!raspuns.ok) {
    const implicit = raspuns.status === 401 ? 'Trebuie să intri în cont.' : raspuns.status === 403 ? 'Nu ai acces aici.' : 'A apărut o eroare. Încearcă din nou.'
    throw new EroareApi(raspuns.status, date?.mesaj || implicit)
  }
  return date
}

// Linkuri către fișiere servite de API (ex. poza unui profesor)
export const urlApi = (cale) => (cale ? API + cale : null)

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
