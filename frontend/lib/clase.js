// Clasele unei Lecții (DB: parcurs.clasa) și cum se afișează.
export const CLASE = ['VIII', 'IX', 'X', 'XI', 'XII', 'Bac']
export const numeClasa = (c) => (c === 'Bac' ? 'Bac' : `clasa a ${c}-a`)
