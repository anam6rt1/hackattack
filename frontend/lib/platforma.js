// Unde rulează clientul: pe site (browser), în aplicația desktop (Electron) sau în cea mobilă (Capacitor).
// Aplicațiile încarcă același cod, ca export static; diferă doar adresa serverului și felul în care ajung cererile la el.
//  - site: /api/... prin redirecționarea Next (next.config.ts), cookie-ul de sesiune e first-party;
//  - desktop: /api/... ajunge la procesul principal Electron, care îl trimite serverului cu sesiunea lui (desktop/main.js);
//  - mobil: adresa completă a serverului; CapacitorHttp face cererile nativ (fără CORS, cookie-uri în depozitul nativ).

const CHEIE_SERVER = 'telebac-server'

export function platforma() {
  if (typeof window === 'undefined') return 'web'
  if (window.telebacDesktop) return 'desktop'
  if (window.Capacitor?.isNativePlatform?.()) return 'mobil'
  return 'web'
}

export const esteAplicatie = () => platforma() !== 'web'

function citeste(cheie) {
  try { return localStorage.getItem(cheie) } catch { return null }
}

/**
 * Serverul aplicației (adresa site-ului, ex. https://scoala.ro), sau null dacă nu a fost ales încă.
 * Desktop: îl păstrează procesul principal (care trimite și cererile /api); mobil: îl păstrează pagina; ambele pot primi unul
 * implicit la build (NEXT_PUBLIC_SITE_URL). Pe site e chiar pagina.
 */
export function serverAles() {
  if (platforma() === 'web') return window.location.origin
  if (platforma() === 'desktop') return window.telebacDesktop.server || null
  return (citeste(CHEIE_SERVER) || process.env.NEXT_PUBLIC_SITE_URL || '').replace(/\/$/, '') || null
}

/** Adresa publică a site-ului (linkuri de împărțit, coduri QR). */
export const adresaSite = () => serverAles() ?? ''

/** Verifică adresa (HTTPS, răspunde ca un server TeleBac), o păstrează și repornește aplicația pe pagina de start. */
export async function alegeServer(adresa) {
  let u
  try { u = new URL(adresa.includes('://') ? adresa : `https://${adresa}`) } catch { throw new Error('Adresa nu este validă.') }
  if (u.protocol !== 'https:' && u.hostname !== 'localhost' && u.hostname !== '127.0.0.1') throw new Error('Serverul trebuie să folosească HTTPS.')
  // Alt server înseamnă alte conturi: lecțiile descărcate și rezultatele offline ale celui vechi nu mai au unde ajunge.
  if (serverAles() && serverAles() !== u.origin) await (await import('./offline/depozit')).golesteTot().catch(() => {})
  if (platforma() === 'desktop') {
    await window.telebacDesktop.alegeServer(u.origin)
  } else {
    const r = await fetch(`${u.origin}/api/judete`, { headers: { 'X-MarkAi': '1' } }).catch(() => null)
    if (!r?.ok) throw new Error('Serverul nu răspunde. Verifică adresa și conexiunea la internet.')
    localStorage.setItem(CHEIE_SERVER, u.origin)
  }
  // A full reload, not router.push: the API address is fixed when the page loads (lib/api.js).
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.href = '/'
}

/** Baza cererilor către API. */
export function adresaApi() {
  if (platforma() === 'mobil') return adresaSite() + '/api'
  return (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5080').replace(/\/$/, '')
}
