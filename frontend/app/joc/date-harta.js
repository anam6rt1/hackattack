// Harta desenată de mână de pe pagina /joc (doar decor, înainte de a intra într-o cameră).
// În joc, harta (9–30 de insule) o generează serverul și vine în starea camerei: { latime, inaltime, insule, poduri }.
// x, y = poziția pe hartă (unități ale desenului); semn = ce clădire apare pe insulă (vezi arta.js).

const INSULE = [
  { id: 'harap', nume: 'Harap-Alb', autor: 'Ion Creangă', x: 170, y: 140, seed: 11, semn: 'castel' },
  { id: 'enigma', nume: 'Enigma Otiliei', autor: 'George Călinescu', x: 500, y: 118, seed: 23, semn: 'conac' },
  { id: 'luceafarul', nume: 'Luceafărul', autor: 'Mihai Eminescu', x: 840, y: 140, seed: 37, semn: 'turn' },
  { id: 'ion', nume: 'Ion', autor: 'Liviu Rebreanu', x: 280, y: 330, seed: 5, semn: 'casa' },
  { id: 'plumb', nume: 'Plumb', autor: 'George Bacovia', x: 545, y: 335, seed: 51, semn: 'clopot' },
  { id: 'iona', nume: 'Iona', autor: 'Marin Sorescu', x: 820, y: 360, seed: 63, semn: 'far' },
  { id: 'moara', nume: 'Moara cu noroc', autor: 'Ioan Slavici', x: 160, y: 520, seed: 71, semn: 'moara' },
  { id: 'morometii', nume: 'Moromeții', autor: 'Marin Preda', x: 485, y: 530, seed: 83, semn: 'casa' },
  { id: 'baltagul', nume: 'Baltagul', autor: 'Mihail Sadoveanu', x: 830, y: 535, seed: 97, semn: 'casa' },
]

const PODURI = [
  ['harap', 'enigma'], ['enigma', 'luceafarul'], ['harap', 'ion'], ['enigma', 'plumb'],
  ['luceafarul', 'iona'], ['ion', 'plumb'], ['plumb', 'iona'], ['ion', 'moara'],
  ['plumb', 'morometii'], ['iona', 'baltagul'], ['moara', 'morometii'], ['morometii', 'baltagul'],
]

export const HARTA_DEMO = { latime: 1000, inaltime: 640, insule: INSULE, poduri: PODURI }

export const insula = (id, harta) => harta.insule.find((i) => i.id === id)
