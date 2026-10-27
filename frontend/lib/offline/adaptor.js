// Răspunsurile „serverului” când nu e internet: aceleași forme ca API-ul, din lecțiile descărcate și din ultimele răspunsuri
// păstrate, ca paginile existente (lecția, capitolele, testul, simulările, recapitularea) să meargă neschimbate.
// Ce se schimbă (capitol deschis, test terminat, simulare, recapitulare) intră în coadă și ajunge la server la reconectare.
// Testele se notează aici cu cheia decriptată, pentru răspuns imediat; serverul le renotează (lib/offline/sincronizare.js).

import { citeste, scrie, toate } from './depozit'
import { capitolDescarcat, lectieDescarcata, lectiiDescarcate } from './pachete'
import { decripteaza } from './cheie'
import { adaugaInCoada } from './sincronizare'

export const FARA_INTERNET = 'Ai nevoie de internet pentru această acțiune. Lecțiile descărcate și testele lor merg și offline.'

class Offline extends Error {
  constructor(mesaj = FARA_INTERNET) { super(mesaj); this.status = 0; this.offline = true }
}

// GET-uri a căror ultimă versiune se păstrează pe dispozitiv (pentru elevi), ca paginile să se deschidă și offline.
const PASTRATE = [/^\/utilizatori\/me$/, /^\/progres$/, /^\/progres\/materii(\?.*)?$/, /^\/teme(\?.*)?$/, /^\/greseli(\?.*)?$/, /^\/simulari(\?pagina=\d+)?$/,
  /^\/simulari\/optiuni$/, /^\/materii(\?.*)?$/, /^\/utilizatori\/me\/organizatii$/, /^\/utilizatori\/me\/preferinte$/, /^\/aspect\/teme$/]
export const sePastreaza = (cale) => PASTRATE.some((r) => r.test(cale))

export async function pastreaza(cale, date) {
  try { if (sePastreaza(cale)) await scrie('raspunsuri', { cale, date, la: new Date().toISOString() }) } catch { /* Spațiu plin: pagina merge oricum online. */ }
}

const amestecate = (lista) => lista.map((x) => [Math.random(), x]).sort((a, b) => a[0] - b[0]).map(([, x]) => x)
const prag = (total) => Math.ceil(total * 0.8)
const idLocal = () => -Math.floor(Date.now() % 1e9 + Math.random() * 1000)
const fara = (q) => ({ id: q.id, text: q.text, variante: q.variante })

async function pachet(parcursId) {
  const p = await lectieDescarcata(parcursId)
  if (!p) throw new Offline('Lecția nu este descărcată pe acest dispozitiv. Descarcă-o când ai internet.')
  return p
}

// Progresul local: o lecție promovată offline apare promovată până o confirmă serverul.
async function cuProgresLocal(p) {
  const local = await citeste('local', `promovata:${p.parcursId}`)
  if (!local) return p.lectie
  return { ...p.lectie, progres: { ...(p.lectie.progres ?? {}), promovatLa: p.lectie.progres?.promovatLa ?? local.la } }
}

const rute = [
  ['GET', /^\/parcursuri\/(\d+)$/, async ([id]) => cuProgresLocal(await pachet(id))],
  ['GET', /^\/parcursuri\/(\d+)\/fisiere$/, async ([id]) => ({ elemente: (await pachet(id)).fisiere, total: (await pachet(id)).fisiere.length })],
  ['GET', /^\/cursuri\/(\d+)$/, async ([id]) => {
    const g = await capitolDescarcat(Number(id))
    if (!g) throw new Offline('Capitolul nu este descărcat pe acest dispozitiv.')
    return g.capitol
  }],
  ['POST', /^\/cursuri\/(\d+)\/acces$/, async ([id]) => { await adaugaInCoada('capitol_deschis', { cursId: Number(id) }); return null }],

  // Testul lecției.
  ['POST', /^\/parcursuri\/(\d+)\/quiz$/, async ([id]) => {
    const p = await pachet(id)
    const existent = (await toate('local')).find((x) => x.cheie.startsWith('test:') && x.parcursId === p.parcursId && !x.finalizat)
    const grile = p.intrebari.filter((q) => q.tip === 'grila')
    if (!grile.length) throw new Offline('Lecția nu are încă întrebări.')
    const t = existent ?? { cheie: `test:${idLocal()}`, parcursId: p.parcursId, intrebari: amestecate(grile).slice(0, p.intrebariPeTest).map((q) => q.id), raspunsuri: {}, inceputLa: new Date().toISOString() }
    if (!existent) await scrie('local', t)
    const id2 = Number(t.cheie.slice(5))
    return { id: id2, total: t.intrebari.length, necesare: prag(t.intrebari.length), raspunse: Object.keys(t.raspunsuri).length,
      intrebari: t.intrebari.filter((q) => !(q in t.raspunsuri)).map((q) => fara(p.intrebari.find((x) => x.id === q))), offline: true }
  }],
  ['GET', /^\/parcursuri\/(\d+)\/quiz\/(-\d+)\/stare$/, async ([, iid]) => {
    const t = await citeste('local', `test:${iid}`)
    if (!t) throw new Offline('Testul nu mai există pe acest dispozitiv.')
    return { finalizataLa: t.finalizat ? t.finalizatLa : null, raspunsuri: t.intrebari.map((q) => ({ intrebareId: q, variantaId: t.raspunsuri[q] ?? 0 })) }
  }],
  ['POST', /^\/parcursuri\/(\d+)\/quiz\/(-\d+)\/raspuns$/, async ([id, iid], corp) => {
    const t = await citeste('local', `test:${iid}`)
    if (!t || t.finalizat) throw new Offline('Testul s-a încheiat. Începe unul nou.')
    if (!t.intrebari.includes(corp.intrebareId)) throw new Offline('Întrebarea nu face parte din acest test.')
    if (corp.intrebareId in t.raspunsuri) throw new Offline('Ai răspuns deja la această întrebare.')
    const p = await pachet(id)
    const chei = await decripteaza(p.cheie)
    t.raspunsuri[corp.intrebareId] = corp.variantaId
    const corect = chei.get(corp.intrebareId)?.corecta === corp.variantaId
    const ramase = t.intrebari.length - Object.keys(t.raspunsuri).length
    if (ramase > 0) { await scrie('local', t); return { corect, ramase, final: null } }

    t.finalizat = true; t.finalizatLa = new Date().toISOString()
    const corecte = t.intrebari.filter((q) => chei.get(q)?.corecta === t.raspunsuri[q]).length
    const necesare = prag(t.intrebari.length), promovat = corecte >= necesare
    await scrie('local', t)
    await adaugaInCoada('test', { idLocal: Number(iid), parcursId: p.parcursId, intrebari: t.intrebari, raspunsuri: t.intrebari.map((q) => t.raspunsuri[q]), inceputLa: t.inceputLa })
    if (promovat) await scrie('local', { cheie: `promovata:${p.parcursId}`, la: t.finalizatLa })
    return { corect, ramase: 0, final: { promovat, corecte, total: t.intrebari.length, necesare, xpCastigat: 0, promovatLa: promovat ? t.finalizatLa : null, offline: true,
      corectii: t.intrebari.map((q) => ({ intrebareId: q, variantaAleasaId: t.raspunsuri[q], variantaCorectaId: chei.get(q)?.corecta, explicatie: chei.get(q)?.explicatie ?? null })) } }
  }],

  // Simulările, din întrebările lecțiilor descărcate (aceeași materie și clasă).
  ['GET', /^\/simulari\/optiuni$/, async () => {
    const grupe = new Map()
    for (const p of await lectiiDescarcate()) {
      const k = `${p.materieId}:${p.clasa}`
      grupe.set(k, { materieId: p.materieId, clasa: p.clasa, materie: p.materie, disponibile: (grupe.get(k)?.disponibile ?? 0) + p.intrebari.length })
    }
    return [...grupe.values()].sort((a, b) => a.materie.localeCompare(b.materie, 'ro') || a.clasa.localeCompare(b.clasa))
  }],
  ['POST', /^\/simulari$/, async (_, c) => {
    if (c.numarIntrebari < 5 || c.numarIntrebari > 50 || c.durataMinute < 5 || c.durataMinute > 180) throw new Offline('Alege 5–50 de întrebări și 5–180 de minute.')
    const pachete = (await lectiiDescarcate()).filter((p) => p.materieId === c.materieId && p.clasa === c.clasa)
    const toateQ = pachete.flatMap((p) => p.intrebari.map((q) => ({ ...q, parcursId: p.parcursId, lectie: p.titlu })))
    if (toateQ.length < c.numarIntrebari) throw new Offline(`Pe acest dispozitiv sunt doar ${toateQ.length} întrebări pentru această materie și clasă.`)
    const acum = new Date()
    const s = { cheie: `simulare:${idLocal()}`, materieId: c.materieId, tip: c.tip, clasa: c.clasa, durataMinute: c.durataMinute, inceputLa: acum.toISOString(),
      expiraLa: new Date(acum.getTime() + c.durataMinute * 60000).toISOString(), intrebari: amestecate(toateQ).slice(0, c.numarIntrebari).map((q) => ({ ...q, raspunsa: false })) }
    await scrie('local', s)
    return dtoSimulare(s)
  }],
  ['GET', /^\/simulari\/(-\d+)$/, async ([id]) => dtoSimulare(await inchideDacaExpirata(await simulare(id)))],
  ['POST', /^\/simulari\/(-\d+)\/raspuns$/, async ([id], c) => {
    const s = await inchideDacaExpirata(await simulare(id))
    if (s.finalizatLa) return dtoSimulare(s)
    const q = s.intrebari.find((x) => x.id === c.intrebareId)
    if (!q) throw new Offline('Întrebarea nu aparține simulării.')
    Object.assign(q, { raspunsa: true, variantaAleasaId: c.variantaId ?? null, valoareAleasa: c.valoare ?? null })
    await scrie('local', s)
    return dtoSimulare(s)
  }],
  ['POST', /^\/simulari\/(-\d+)\/finalizare$/, async ([id]) => dtoSimulare(await inchide(await simulare(id)))],

  // Recapitularea greșelilor, pentru întrebările din lecțiile descărcate.
  ['POST', /^\/greseli\/(\d+)\/incepe$/, async ([id]) => {
    const q = (await lectiiDescarcate()).flatMap((p) => p.intrebari).find((x) => x.id === Number(id))
    if (!q) throw new Offline('Întrebarea nu e într-o lecție descărcată.')
    return { token: `local-${id}`, intrebare: fara(q) }
  }],
  ['POST', /^\/greseli\/(\d+)\/raspuns$/, async ([id], c) => {
    const p = (await lectiiDescarcate()).find((x) => x.intrebari.some((q) => q.id === Number(id)))
    if (!p) throw new Offline('Întrebarea nu e într-o lecție descărcată.')
    const k = (await decripteaza(p.cheie)).get(Number(id))
    await adaugaInCoada('recapitulare', { intrebareId: Number(id), variantaId: c.variantaId })
    return { corect: k?.corecta === c.variantaId, variantaCorectaId: k?.corecta, explicatie: k?.explicatie ?? null, rezolvata: false, scadentaLa: null, corecteConsecutive: null, offline: true }
  }],
]

async function simulare(id) {
  const s = await citeste('local', `simulare:${id}`)
  if (!s) throw new Offline('Simularea nu mai există pe acest dispozitiv.')
  return s
}

async function inchideDacaExpirata(s) { return !s.finalizatLa && Date.now() >= Date.parse(s.expiraLa) ? inchide(s) : s }

async function inchide(s) {
  if (s.finalizatLa) return s
  const chei = new Map()
  for (const p of await lectiiDescarcate()) if (s.intrebari.some((q) => q.parcursId === p.parcursId)) for (const [k, v] of await decripteaza(p.cheie)) chei.set(k, v)
  s.finalizatLa = new Date(Math.min(Date.now(), Date.parse(s.expiraLa))).toISOString()
  for (const q of s.intrebari) {
    const k = chei.get(q.id)
    Object.assign(q, { variantaCorectaId: k?.corecta ?? null, raspunsNumeric: k?.numeric ?? null, explicatie: k?.explicatie ?? null,
      corect: q.raspunsa && (q.tip === 'grila' ? q.variantaAleasaId === k?.corecta : q.valoareAleasa === k?.numeric) })
  }
  await scrie('local', s)
  await adaugaInCoada('simulare', { materieId: s.materieId, tip: s.tip, clasa: s.clasa, durataMinute: s.durataMinute, inceputLa: s.inceputLa, finalizatLa: s.finalizatLa,
    raspunsuri: s.intrebari.map((q) => ({ intrebareId: q.id, variantaId: q.raspunsa ? q.variantaAleasaId : null, valoare: q.raspunsa ? q.valoareAleasa : null })) })
  return s
}

function dtoSimulare(s) {
  const gata = !!s.finalizatLa, corecte = gata ? s.intrebari.filter((q) => q.corect).length : null
  const recap = new Map()
  if (gata) for (const q of s.intrebari.filter((x) => !x.corect)) recap.set(q.parcursId, { parcursId: q.parcursId, lectie: q.lectie, greseli: (recap.get(q.parcursId)?.greseli ?? 0) + 1 })
  return { id: Number(s.cheie.slice(9)), materieId: s.materieId, tip: s.tip, clasa: s.clasa, inceputLa: s.inceputLa, expiraLa: s.expiraLa, finalizatLa: s.finalizatLa ?? null,
    total: s.intrebari.length, serverLa: new Date().toISOString(), corecte, notaOrientativa: gata ? Math.round((1 + 9 * corecte / s.intrebari.length) * 100) / 100 : null, offline: true,
    intrebari: s.intrebari.map((q) => ({ id: q.id, parcursId: q.parcursId, lectie: q.lectie, text: q.text, tip: q.tip, variante: q.variante, raspunsa: q.raspunsa,
      variantaAleasaId: q.variantaAleasaId ?? null, valoareAleasa: q.valoareAleasa ?? null, variantaCorectaId: gata ? q.variantaCorectaId : null,
      raspunsNumeric: gata ? q.raspunsNumeric : null, explicatie: gata ? q.explicatie : null, corect: gata ? q.corect : null })),
    deRecapitulat: gata ? [...recap.values()] : null }
}

/** Răspunde unei cereri fără internet, sau aruncă o eroare cu mesaj pentru elev (status 0, ca orice problemă de rețea). */
export async function raspundeOffline(cale, { metoda = 'GET', corp } = {}) {
  const [drum] = cale.split('?')
  for (const [m, re, fn] of rute) {
    const g = m === metoda && drum.match(re)
    if (g) return fn(g.slice(1), corp)
  }
  if (metoda === 'GET') {
    const p = await citeste('raspunsuri', cale).catch(() => null)
    if (p) return p.date
  }
  throw new Offline()
}

/** Simulările și testele locale încă netrimise (pentru pagina „Descărcate”). */
export async function lucruLocal() {
  try { return (await toate('local')).filter((x) => x.cheie.startsWith('test:') || x.cheie.startsWith('simulare:')) } catch { return [] }
}

