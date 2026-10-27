import { afterEach, expect, it, vi } from 'vitest'
import { optimizeazaDocument } from '@/lib/optimizare-document.mjs'
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals() })
const fisier = () => new File([new Uint8Array(100000)], 'lectie.pdf', { type: 'application/pdf', lastModified: 7 })
function mockWorker(bytes) {
  const stop = vi.fn()
  vi.stubGlobal('Worker', class {
    postMessage() { if (bytes) queueMicrotask(() => this.onmessage({ data: { bytes } })) }
    terminate() { stop() }
  })
  return stop
}
it('trimite fișierul rescris numai dacă economia depășește pragul', async () => {
  const stop = mockWorker(new Uint8Array(60000)), f = fisier()
  const r = await optimizeazaDocument(f)
  expect(r.economisiti).toBe(40000)
  expect(r.fisier.name).toBe(f.name)
  expect(r.fisier.type).toBe('application/pdf')
  expect(r.fisier.lastModified).toBe(7)
  expect(stop).toHaveBeenCalledOnce()
})
it('păstrează originalul pentru rezultate mai mari sau sub 5% economie', async () => {
  for (const size of [98000, 110000]) {
    mockWorker(new Uint8Array(size)); const f = fisier()
    expect((await optimizeazaDocument(f)).fisier).toBe(f)
  }
})
it('opțiunea original și lipsa Worker păstrează octeții inițiali', async () => {
  const f = fisier(); vi.stubGlobal('Worker', undefined)
  expect((await optimizeazaDocument(f)).fisier).toBe(f)
  expect((await optimizeazaDocument(f, { original: true })).fisier).toBe(f)
})
it('oprește procesarea la anulare și la timeout', async () => {
  let stop = mockWorker(null), c = new AbortController()
  const p = optimizeazaDocument(fisier(), { signal: c.signal }); c.abort()
  await expect(p).rejects.toHaveProperty('name', 'AbortError')
  expect(stop).toHaveBeenCalledOnce()
  vi.useFakeTimers(); stop = mockWorker(null)
  const f = fisier(), timeout = optimizeazaDocument(f)
  await vi.advanceTimersByTimeAsync(21000) // 20 s plus 1 s per started MiB
  expect((await timeout).fisier).toBe(f)
  expect(stop).toHaveBeenCalledOnce()
})
