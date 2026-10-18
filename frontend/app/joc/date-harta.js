// Harta jocului: insulele (teritoriile) și podurile dintre ele.
// x, y = poziția pe hartă (harta are 1000 x 640 de unități).
// semn = ce desen apare pe insulă (vezi arta.js).

export const INSULE = [
  { id: 'harap', nume: 'Harap-Alb', autor: 'Ion Creangă', x: 170, y: 140, seed: 11, semn: 'castel' },
  { id: 'enigma', nume: 'Enigma Otiliei', autor: 'George Călinescu', x: 500, y: 118, seed: 23, semn: 'conac' },
  { id: 'luceafarul', nume: 'Luceafărul', autor: 'Mihai Eminescu', x: 840, y: 140, seed: 37, semn: 'turn' },
  { id: 'ion', nume: 'Ion', autor: 'Liviu Rebreanu', x: 280, y: 330, seed: 5, semn: 'casa' },
  { id: 'plumb', nume: 'Plumb', autor: 'George Bacovia', x: 545, y: 335, seed: 51, semn: 'clopot' },
  { id: 'iona', nume: 'Iona', autor: 'Marin Sorescu', x: 820, y: 360, seed: 63, semn: 'far' },
  { id: 'moara', nume: 'Moara cu noroc', autor: 'Ioan Slavici', x: 160, y: 520, seed: 71, semn: 'moara' },
  { id: 'morometii', nume: 'Moromeții', autor: 'Marin Preda', x: 485, y: 530, seed: 83, semn: 'casa' },
  { id: 'baltagul', nume: 'Baltagul', autor: 'Mihail Sadoveanu', x: 830, y: 535, seed: 97, semn: 'casa', brazi: true },
]

export const PODURI = [
  ['harap', 'enigma'], ['enigma', 'luceafarul'], ['harap', 'ion'], ['enigma', 'plumb'],
  ['luceafarul', 'iona'], ['ion', 'plumb'], ['plumb', 'iona'], ['ion', 'moara'],
  ['plumb', 'morometii'], ['iona', 'baltagul'], ['moara', 'morometii'], ['morometii', 'baltagul'],
]

// Cetățile de pornire: primul jucător primește Moara, al doilea Luceafărul, al treilea Baltagul
export const CETATI = ['moara', 'luceafarul', 'baltagul']

// Culorile jucătorilor, în aceeași ordine
export const CULORI = ['#e5483b', '#7a52d8', '#e0469a']

export const insula = (id, insule = INSULE) => insule.find((i) => i.id === id)

// Insulele pentru o materie: fiecare capitol (grup de lecții) primește un loc pe hartă.
// Capitolele care sunt chiar operele desenate își păstrează insula; restul ocupă locurile libere.
// Locurile rămase fără capitol păstrează opera desenată (pentru română) sau devin „Insula N”,
// iar acolo jocul folosește întrebările generale ale materiei.
export function insuleDinCursuri(cursuri) {
  const capitole = []
  for (const c of cursuri) {
    const nume = c.capitol || c.titlu
    let cap = capitole.find((x) => x.nume === nume)
    if (!cap) capitole.push((cap = { nume, cursIds: [] }))
    cap.cursIds.push(c.id)
  }
  const potrivite = new Map(capitole.filter((c) => INSULE.some((i) => i.nume === c.nume)).map((c) => [c.nume, c]))
  const ramase = capitole.filter((c) => !potrivite.has(c.nume))
  const pastreazaOpere = potrivite.size > 0
  let n = 0
  return INSULE.map((ins) => {
    const cap = potrivite.get(ins.nume) ?? ramase.shift()
    n++
    if (cap) return { ...ins, nume: cap.nume, autor: potrivite.has(cap.nume) ? ins.autor : `${cap.cursIds.length} ${cap.cursIds.length === 1 ? 'lecție' : 'lecții'}`, cursIds: cap.cursIds }
    return pastreazaOpere ? { ...ins, cursIds: [] } : { ...ins, nume: `Insula ${n}`, autor: 'întrebări generale', cursIds: [] }
  })
}

export function vecini(id) {
  return PODURI.filter((p) => p.includes(id)).map((p) => (p[0] === id ? p[1] : p[0]))
}

// Insulele pe care le poate ataca un jucător: legate prin pod de ale lui,
// care nu sunt deja ale lui și nu sunt cetatea altui jucător.
export function tinte(proprietari, jucatorId, cetatiOcupate) {
  // pozițiile și podurile sunt aceleași pentru orice materie, doar numele insulelor diferă
  const ale_mele = INSULE.filter((i) => proprietari[i.id] === jucatorId).map((i) => i.id)
  const rezultat = new Set()
  ale_mele.forEach((m) =>
    vecini(m).forEach((v) => {
      if (proprietari[v] !== jucatorId && !cetatiOcupate.includes(v)) rezultat.add(v)
    })
  )
  return [...rezultat]
}
