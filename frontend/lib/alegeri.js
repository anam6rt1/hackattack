// Câte materii și organizații poate alege un profesor. Serverul are aceleași limite
// (database/src/MarkAi.Api/Infrastructura/Alegeri.cs) și refuză mai multe.

// Materiile predate (înregistrare și Setări).
export const MATERII_PROFESOR = 4

// Organizațiile în care predă sau așteaptă aprobarea (înregistrare).
export const ORGANIZATII_PROFESOR = 4

// Organizațiile în care e arătată o lecție, pe lângă a ei (editorul de conținut).
export const PARTAJARI_LECTIE = 4

/** Organizațiile aprobate care se potrivesc căutării (nume, județ sau localitate), cele din județ dacă e dat. */
export const urlOrganizatii = (q, judetId) => {
  const p = new URLSearchParams()
  if (judetId) p.set('judetId', judetId)
  if (q.trim().length > 1) p.set('q', q.trim())
  const s = p.toString()
  return '/organizatii' + (s ? `?${s}` : '')
}

const fara = (t) => t.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()

/** Pentru o listă scurtă, deja încărcată: fiecare cuvânt căutat apare în text (fără diacritice). */
export const potriveste = (text, q) => fara(q).split(/\s+/).filter(Boolean).every((c) => fara(text).includes(c))
