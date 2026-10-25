import { afterEach, expect, it, vi } from 'vitest'
import { optimizeazaFotografie } from '@/lib/optimizare-fisiere.mjs'

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals() })
function workerBlocat() {
  const stop = vi.fn()
  vi.stubGlobal('Worker', class { postMessage() {} terminate() { stop() } })
  return stop
}
const foto = () => new File([new Uint8Array(200000)], 'p.png', { type: 'image/png' })

it('oprește workerul blocat după 12 secunde și păstrează originalul', async () => {
  vi.useFakeTimers()
  const stop = workerBlocat(), f = foto(), p = optimizeazaFotografie(f)
  await vi.advanceTimersByTimeAsync(12000)
  expect((await p).fisier).toBe(f)
  expect(stop).toHaveBeenCalledOnce()
})
it('anularea selecției închide workerul și nu publică un rezultat vechi', async () => {
  const stop = workerBlocat(), c = new AbortController()
  const p = optimizeazaFotografie(foto(), { signal: c.signal })
  c.abort()
  await expect(p).rejects.toHaveProperty('name', 'AbortError')
  expect(stop).toHaveBeenCalledOnce()
})
