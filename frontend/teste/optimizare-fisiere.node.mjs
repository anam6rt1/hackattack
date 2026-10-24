import { test, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { optimizeazaFotografie } from '../lib/optimizare-fisiere.mjs'

const initialWorker = globalThis.Worker
afterEach(() => { globalThis.Worker = initialWorker })
const poza = () => new File([new Uint8Array(200_000)], 'portret.jpg', { type: 'image/jpeg', lastModified: 100 })

function workerCu(blob, eroare = false) {
  const stare = { pornit: 0, oprit: 0 }
  globalThis.Worker = class {
    constructor() { stare.pornit++ }
    postMessage() { queueMicrotask(() => eroare ? this.onerror() : this.onmessage({ data: { blob } })) }
    terminate() { stare.oprit++ }
  }
  return stare
}

test('păstrează originalul la cerere și nu pornește workerul', async () => {
  const stare = workerCu(null)
  const f = poza()
  assert.equal((await optimizeazaFotografie(f, { original: true })).fisier, f)
  assert.equal(stare.pornit, 0)
})

test('documentele, fișierele mici și cele peste buget rămân intacte', async () => {
  const stare = workerCu(null)
  for (const f of [new File(['pdf'], 'curs.pdf', { type: 'application/pdf' }),
    new File(['x'], 'mic.png', { type: 'image/png' }),
    new File([new Uint8Array(17 * 1024 * 1024)], 'mare.jpg', { type: 'image/jpeg' })]) {
    assert.equal((await optimizeazaFotografie(f)).fisier, f)
  }
  assert.equal(stare.pornit, 0)
})

test('trimite WebP cu nume și MIME corecte și raportează economia', async () => {
  const stare = workerCu(new Blob([new Uint8Array(100_000)], { type: 'image/webp' }))
  const rezultat = await optimizeazaFotografie(poza())
  assert.equal(rezultat.fisier.name, 'portret.webp')
  assert.equal(rezultat.fisier.type, 'image/webp')
  assert.equal(rezultat.fisier.lastModified, 100)
  assert.equal(rezultat.economisiti, 100_000)
  assert.equal(stare.oprit, 1)
})

test('rezultatul mai mare, invalid sau cu economie sub 5% este ignorat', async () => {
  for (const blob of [new Blob([new Uint8Array(210_000)], { type: 'image/webp' }),
    new Blob([new Uint8Array(195_000)], { type: 'image/webp' }),
    new Blob(['x'], { type: 'image/png' }), new Blob([], { type: 'image/webp' })]) {
    const stare = workerCu(blob)
    const f = poza()
    assert.equal((await optimizeazaFotografie(f)).fisier, f)
    assert.equal(stare.oprit, 1)
  }
})

test('browser fără Worker sau eroare de procesare revine la original', async () => {
  const f = poza()
  globalThis.Worker = undefined
  assert.equal((await optimizeazaFotografie(f)).fisier, f)
  const stare = workerCu(null, true)
  assert.equal((await optimizeazaFotografie(f)).fisier, f)
  assert.equal(stare.oprit, 1)
})
