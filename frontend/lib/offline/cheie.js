// Cheia cu care se citesc răspunsurile corecte ale lecțiilor descărcate (decizia proprietarului: notare locală, cu chei criptate).
// Serverul o derivă din cont și din versiunea sesiunii (GET /utilizatori/me/cheie-offline); aici devine un CryptoKey
// neexportabil, păstrat în IndexedDB: codul paginii îl poate folosi, dar nu îl poate citi ca text. Răspunsurile decriptate
// stau doar în memorie, cât durează un test. Cine controlează dispozitivul le poate afla totuși — notele adevărate le dă serverul.

import { citeste, scrie } from './depozit'

const base64 = (s) => Uint8Array.from(atob(s), (c) => c.charCodeAt(0))

/** Cere cheia de la server (online) și o păstrează; o versiune nouă de sesiune o înlocuiește. */
export async function improspateazaCheie(api) {
  const { cheie, versiune } = await api('/utilizatori/me/cheie-offline')
  const crypto = await globalThis.crypto.subtle.importKey('raw', base64(cheie), { name: 'AES-GCM' }, false, ['decrypt'])
  await scrie('local', { cheie: 'cheie', crypto, versiune })
  return crypto
}

async function cheieLocala() {
  const c = await citeste('local', 'cheie')
  if (!c?.crypto) throw new Error('Lecția trebuie descărcată din nou: cheia de pe acest dispozitiv lipsește.')
  return c.crypto
}

/** Decriptează cheia de răspunsuri a unui pachet: [{ id, corecta, numeric, explicatie }], ca hartă după id. */
export async function decripteaza(cheieCriptata) {
  const date = base64(cheieCriptata)
  try {
    const clar = await globalThis.crypto.subtle.decrypt({ name: 'AES-GCM', iv: date.slice(0, 12) }, await cheieLocala(), date.slice(12))
    return new Map(JSON.parse(new TextDecoder().decode(clar)).map((x) => [x.id, x]))
  } catch (e) {
    if (e.message?.includes('descărcată')) throw e
    // Sesiunea a fost închisă de pe alt dispozitiv sau parola s-a schimbat: cheia veche nu mai deschide pachetul.
    throw new Error('Lecția trebuie descărcată din nou: cheia de pe acest dispozitiv s-a schimbat.')
  }
}
