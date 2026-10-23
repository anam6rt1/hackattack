// Ședințele cu profesori: online, față în față sau hibrid (fiecare elev alege cum participă), și locul lor.

export const TIPURI_SEDINTA = { online: 'Online', fata_in_fata: 'Față în față', hibrid: 'Hibrid' }
export const PARTICIPARE = { online: 'online', la_sala: 'la sală' }

// { organizatie, localitate, judet } → „Liceul de Test · Sectorul 1, București”
export const textLoc = (loc) =>
  loc ? [loc.organizatie, [loc.localitate, loc.judet].filter(Boolean).join(', ')].filter(Boolean).join(' · ') : ''
