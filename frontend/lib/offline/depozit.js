// Depozitul local al modului offline (IndexedDB), pe dispozitivul elevului:
//   pachete     — o lecție descărcată: lecția, capitolele, lista fișierelor, întrebările și cheia de răspunsuri criptată
//   fisiere     — conținutul fișierelor descărcate (originalul și, la Office, copia PDF de previzualizare)
//   coada       — acțiunile făcute offline, în ordine, până când serverul le confirmă (POST /sincronizare)
//   raspunsuri  — ultimul răspuns al unor GET-uri (contul, progresul, temele...), ca paginile să se deschidă și fără internet
//   local       — testele, simulările și recapitulările începute offline, plus cheia de decriptare (CryptoKey neexportabil)
// Totul aparține unui singur cont: la ieșire sau la schimbarea contului se șterge (vezi lib/sesiune.js).

const NUME = 'telebac-offline-v2'
const MAGAZII = { pachete: 'parcursId', fisiere: 'cheie', coada: 'idClient', raspunsuri: 'cale', local: 'cheie' }
let deschis = null

function deschide() {
  if (typeof indexedDB === 'undefined') return Promise.reject(new Error('Acest dispozitiv nu permite salvarea offline.'))
  deschis ??= new Promise((resolve, reject) => {
    const r = indexedDB.open(NUME, 1)
    r.onupgradeneeded = () => { for (const [m, cheie] of Object.entries(MAGAZII)) if (!r.result.objectStoreNames.contains(m)) r.result.createObjectStore(m, { keyPath: cheie }) }
    r.onsuccess = () => { r.result.onversionchange = () => { r.result.close(); deschis = null }; resolve(r.result) }
    r.onerror = () => { deschis = null; reject(r.error) }
  })
  return deschis
}

async function tranzactie(magazii, mod, lucru) {
  const db = await deschide()
  return new Promise((resolve, reject) => {
    const tx = db.transaction(magazii, mod)
    let rezultat
    Promise.resolve(lucru(tx)).then((r) => { rezultat = r }, reject)
    tx.oncomplete = () => resolve(rezultat)
    tx.onerror = () => reject(tx.error)
    tx.onabort = () => reject(tx.error ?? new Error('Salvarea pe dispozitiv a eșuat. Verifică spațiul liber.'))
  })
}

const cerere = (r) => new Promise((resolve, reject) => { r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error) })

export const citeste = (magazie, cheie) => tranzactie(magazie, 'readonly', (tx) => cerere(tx.objectStore(magazie).get(cheie)))
export const toate = (magazie) => tranzactie(magazie, 'readonly', (tx) => cerere(tx.objectStore(magazie).getAll()))
export const scrie = (magazie, valoare) => tranzactie(magazie, 'readwrite', (tx) => cerere(tx.objectStore(magazie).put(valoare)))
export const sterge = (magazie, cheie) => tranzactie(magazie, 'readwrite', (tx) => cerere(tx.objectStore(magazie).delete(cheie)))

/** Mai multe scrieri într-o singură tranzacție: o lecție se salvează întreagă sau deloc. */
export const scrieToate = (scrieri) => tranzactie([...new Set(scrieri.map(([m]) => m))], 'readwrite', (tx) => {
  for (const [m, v] of scrieri) tx.objectStore(m).put(v)
})

/** Șterge tot (ieșirea din cont, alt cont pe același dispozitiv). */
export async function golesteTot() {
  try { localStorage.removeItem(MARCAJ) } catch { /* fără localStorage: nimic de șters */ }
  if (typeof indexedDB === 'undefined') return
  await tranzactie(Object.keys(MAGAZII), 'readwrite', (tx) => { for (const m of Object.keys(MAGAZII)) tx.objectStore(m).clear() })
}

/** Al cui e depozitul (id-ul contului) — null dacă e gol. */
export async function proprietar() {
  try { return (await citeste('local', 'proprietar'))?.uid ?? null } catch { return null }
}

/** Leagă depozitul de un cont: dacă era al altcuiva, îl golește întâi. */
export async function asiguraProprietar(uid) {
  if (typeof indexedDB === 'undefined') return
  const actual = await proprietar()
  if (actual !== null && actual !== uid) await golesteTot()
  if (actual !== uid) await scrie('local', { cheie: 'proprietar', uid })
  try { localStorage.setItem(MARCAJ, String(uid)) } catch { /* modul offline merge și fără marcaj, doar fără răspunsuri păstrate */ }
}

// Marcaj sincron (lib/api.js îl citește la fiecare cerere): există date offline, deci merită încărcat codul lor.
const MARCAJ = 'telebac-offline'
export const areDateOffline = () => { try { return !!localStorage.getItem(MARCAJ) } catch { return false } }
