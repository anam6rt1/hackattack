// Lecțiile descărcate pentru lucru fără internet: tot ce trebuie ca pagina lecției, capitolele, fișierele și testul să meargă
// offline (vezi lib/offline/adaptor.js). Se descarcă online, prin aceleași endpointuri ca site-ul, plus banca de întrebări
// cu răspunsurile criptate (GET /parcursuri/{id}/offline).

import { API, api } from '../api'
import { citeste, scrieToate, sterge, toate, asiguraProprietar } from './depozit'
import { improspateazaCheie } from './cheie'

const OFFICE = ['docx', 'pptx', 'xlsx']

async function descarcaBinar(cale, semnal) {
  const r = await fetch(API + cale, { credentials: 'include', headers: { 'X-MarkAi': '1' }, signal: semnal })
  if (!r.ok) throw new Error('Un fișier al lecției nu a putut fi descărcat. Reîncearcă.')
  return r.blob()
}

/**
 * Descarcă (sau actualizează) o lecție. `onProgres(pas, din)` arată cât a rămas. Totul se scrie la final, într-o singură
 * tranzacție: o descărcare întreruptă nu lasă o lecție pe jumătate.
 */
export async function descarcaLectie(parcursId, uid, { onProgres, semnal } = {}) {
  await asiguraProprietar(uid)
  const vechi = await lectieDescarcata(parcursId)
  // Cere browserului să nu șteargă datele când face curat. Fără await: Firefox întreabă utilizatorul, iar descărcarea
  // nu trebuie să aștepte răspunsul (sau să rămână blocată dacă nu vine).
  navigator.storage?.persist?.().catch(() => false)
  // Contul, ca aplicația pornită fără internet să știe cine e conectat.
  await (await import('./adaptor')).pastreaza('/utilizatori/me', await api('/utilizatori/me'))
  const lectie = await api(`/parcursuri/${parcursId}`)
  const fisiere = (await api(`/parcursuri/${parcursId}/fisiere`)).elemente
  const pasi = 2 + lectie.capitole.length + fisiere.length * 2
  let facuti = 0
  const pas = () => onProgres?.(++facuti, pasi)

  const pachet = await api(`/parcursuri/${parcursId}/offline`); pas()
  const { versiune } = await improspateazaCheie(api).then(() => citeste('local', 'cheie')); pas()
  const capitole = {}
  for (const c of lectie.capitole) { capitole[c.id] = await api(`/cursuri/${c.id}`); pas() }
  const scrieri = []
  let marime = 0
  for (const f of fisiere) {
    const original = await descarcaBinar(`/fisiere/${f.id}?descarca=true`, semnal); pas()
    marime += original.size
    scrieri.push(['fisiere', { cheie: `${f.id}:original`, parcursId, blob: original }])
    if (OFFICE.includes(f.tip)) {
      // Copia PDF a documentelor Office: dispozitivul nu le poate converti singur pentru previzualizare.
      const pdf = await descarcaBinar(`/fisiere/${f.id}/previzualizare`, semnal).catch(() => null)
      if (pdf) { marime += pdf.size; scrieri.push(['fisiere', { cheie: `${f.id}:pdf`, parcursId, blob: pdf }]) }
    }
    pas()
  }
  const record = {
    parcursId, uid, titlu: lectie.titlu, materieId: lectie.materieId, materie: lectie.materie, clasa: lectie.clasa,
    lectie, capitole, fisiere, intrebari: pachet.intrebari, cheie: pachet.cheie, intrebariPeTest: pachet.intrebariPeTest,
    versiuneCheie: versiune, descarcataLa: new Date().toISOString(),
    marime: marime + new Blob([JSON.stringify({ lectie, capitole, pachet })]).size,
  }
  await scrieToate([['pachete', record], ...scrieri])
  // An update keeps the old copy until the new one is saved; only files the Lecție no longer has go afterwards.
  const noi = new Set(fisiere.map((f) => f.id))
  await stergeFisiere((vechi?.fisiere ?? []).filter((f) => !noi.has(f.id)))
  return record
}

async function stergeFisiere(fisiere) {
  for (const f of fisiere) for (const v of ['original', 'pdf']) await sterge('fisiere', `${f.id}:${v}`)
}

export async function stergeLectie(parcursId) {
  await stergeFisiere((await lectieDescarcata(parcursId))?.fisiere ?? [])
  await sterge('pachete', Number(parcursId))
}

export async function lectiiDescarcate() {
  try { return (await toate('pachete')).sort((a, b) => a.titlu.localeCompare(b.titlu, 'ro')) } catch { return [] }
}

export async function lectieDescarcata(parcursId) {
  try { return (await citeste('pachete', Number(parcursId))) ?? null } catch { return null }
}

/** Fișierul descărcat (`varianta`: 'original' sau 'pdf'), sau null. */
export async function fisierDescarcat(id, varianta = 'original') {
  try { return (await citeste('fisiere', `${id}:${varianta}`))?.blob ?? null } catch { return null }
}

/** Capitolul, din lecția descărcată care îl conține. */
export async function capitolDescarcat(cursId) {
  for (const p of await lectiiDescarcate()) if (p.capitole[cursId]) return { pachet: p, capitol: p.capitole[cursId] }
  return null
}

// Pe site (nu în aplicații, care au fișierele pe dispozitiv): service worker-ul și paginile de care are nevoie un elev offline,
// cu codul lor, încărcate acum, cât e internet — altfel testul nu s-ar putea deschide offline dacă n-a fost vizitat înainte.
const PAGINI_OFFLINE = ['/', '/parcurs', '/test', '/descarcate', '/simulari', '/teme', '/materiale', '/antrenament']
export async function pregatesteSiteul() {
  const { platforma } = await import('../platforma')
  if (platforma() !== 'web' || !('serviceWorker' in navigator)) return
  // At most 15 s in all: some browsers (private modes, automated Firefox) never settle register() or ready, and the student
  // must not wait on that — the Lecție itself is already saved; only reopening pages offline depends on this.
  const limita = AbortSignal.timeout(15_000)
  const sau = (p) => Promise.race([p, new Promise((_, nu) => limita.addEventListener('abort', () => nu(limita.reason), { once: true }))])
  await sau(navigator.serviceWorker.register('/sw.js'))
  await sau(navigator.serviceWorker.ready)
  const resurse = new Set()
  for (const p of PAGINI_OFFLINE) {
    const html = await fetch(p, { credentials: 'include', signal: limita }).then((r) => (r.ok ? r.text() : ''), () => '')
    for (const m of html.matchAll(/\/_next\/static\/[^"'\s)]+/g)) resurse.add(m[0])
  }
  await sau(Promise.all([...resurse].map((r) => fetch(r, { signal: limita }).catch(() => null))))
}
