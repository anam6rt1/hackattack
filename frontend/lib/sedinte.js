// Ședințele cu profesori: online, față în față sau hibrid (fiecare elev alege cum participă), și locul lor.

export const TIPURI_SEDINTA = { online: 'Online', fata_in_fata: 'Față în față', hibrid: 'Hibrid' }
export const PARTICIPARE = { online: 'online', la_sala: 'la sală' }

// { organizatie, localitate, judet } → „Liceul de Test · Sectorul 1, București”
export const textLoc = (loc) =>
  loc ? [loc.organizatie, [loc.localitate, loc.judet].filter(Boolean).join(', ')].filter(Boolean).join(' · ') : ''

// Lista de profesori arată doar următoarea ședință; restul: „și încă o sesiune”, „și alte 3 sesiuni”, „și alte 20 de sesiuni”.
export const textAlteSesiuni = (n) =>
  n === 1 ? 'și încă o sesiune' : `și alte ${n} ${n % 100 >= 20 || (n >= 100 && n % 100 === 0) ? 'de ' : ''}sesiuni`
