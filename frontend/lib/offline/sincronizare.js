// Coada acțiunilor făcute offline și trimiterea lor la server (POST /sincronizare), în ordinea în care au fost făcute.
// Fiecare acțiune are id-ul ei (idClient): dacă legătura cade după ce serverul a aplicat lotul, retrimiterea nu dublează nimic.
// Serverul renotează testele: rezultatul lui (XP, promovare) e cel care contează.

import { areDateOffline, scrie, sterge, toate, citeste } from './depozit'

const LOT = 200
let ordine = 0

export async function adaugaInCoada(tip, date) {
  const op = { idClient: crypto.randomUUID(), tip, date, ordine: Date.now() * 1000 + (ordine++ % 1000), creataLa: new Date().toISOString() }
  await scrie('coada', op)
  if (navigator.onLine) setTimeout(() => sincronizeaza().catch(() => {}), 0)
  return op
}

export async function actiuniInAsteptare() {
  try { return (await toate('coada')).sort((a, b) => a.ordine - b.ordine) } catch { return [] }
}

/** Acțiunile refuzate de server (lecția nu mai e disponibilă etc.), ca elevul să afle de ce nu au contat. */
export async function actiuniRefuzate() {
  try { return (await citeste('local', 'refuzate'))?.lista ?? [] } catch { return [] }
}

export async function uitaRefuzate() { await sterge('local', 'refuzate') }

let inCurs = null

/** Trimite tot ce așteaptă; rezultatul: { aplicate, refuzate, xp }. O singură trimitere odată. */
export function sincronizeaza() {
  inCurs ??= trimite().finally(() => { inCurs = null })
  return inCurs
}

async function trimite() {
  const { api } = await import('../api')
  const total = { aplicate: 0, refuzate: 0, xp: 0, promovate: [] }
  for (;;) {
    const lot = (await actiuniInAsteptare()).slice(0, LOT)
    if (!lot.length) break
    const { rezultate } = await api('/sincronizare', { metoda: 'POST', corp: { operatii: lot.map(({ idClient, tip, date }) => ({ idClient, tip, date })) } })
    const refuzate = []
    for (const r of rezultate) {
      const op = lot.find((o) => o.idClient === r.idClient)
      await sterge('coada', r.idClient)
      if (r.stare === 'respinsa') { refuzate.push({ tip: op?.tip, mesaj: r.mesaj, la: op?.creataLa }); continue }
      total.aplicate++
      if (op?.tip === 'test' && r.rezultat) {
        total.xp += r.rezultat.xpCastigat ?? 0
        if (r.rezultat.promovat) total.promovate.push(r.rezultat.parcursId)
        await sterge('local', `test:${op.date.idLocal}`)
      }
    }
    if (refuzate.length) {
      total.refuzate += refuzate.length
      await scrie('local', { cheie: 'refuzate', lista: [...await actiuniRefuzate(), ...refuzate].slice(-50) })
    }
  }
  // Paginile citesc din nou progresul de pe server (lecțiile promovate, XP-ul).
  if (total.aplicate || total.refuzate) {
    for (const c of ['/progres', '/teme', '/utilizatori/me']) await sterge('raspunsuri', c).catch(() => {})
    window.dispatchEvent(new CustomEvent('telebac-sincronizare', { detail: total }))
  }
  return total
}

/**
 * Pornește trimiterea: la deschiderea aplicației, la reconectare, când aplicația revine în față și, cât timp așteaptă ceva,
 * la fiecare 15 secunde — evenimentul „online” nu vine mereu (o rețea fără acces la server, unele browsere).
 */
export function pornesteSincronizarea() {
  const incearca = async () => {
    if (!navigator.onLine || inCurs || !areDateOffline()) return
    if ((await actiuniInAsteptare()).length) sincronizeaza().catch(() => {})
  }
  const vizibila = () => { if (document.visibilityState === 'visible') incearca() }
  window.addEventListener('online', incearca)
  window.addEventListener('focus', incearca)
  document.addEventListener('visibilitychange', vizibila)
  const ceas = setInterval(incearca, 15_000)
  incearca()
  return () => {
    window.removeEventListener('online', incearca); window.removeEventListener('focus', incearca)
    document.removeEventListener('visibilitychange', vizibila); clearInterval(ceas)
  }
}
