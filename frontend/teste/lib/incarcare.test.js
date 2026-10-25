import { afterEach, describe, expect, it, vi } from 'vitest'
import { incarcaFormular } from '@/lib/incarcare'

afterEach(() => vi.unstubAllGlobals())
function fals() {
  class XHR {
    static instanta
    constructor() { XHR.instanta = this; this.upload = {}; this.headers = {} }
    open(m, url) { this.metoda = m; this.url = url }
    setRequestHeader(k, v) { this.headers[k] = v }
    send(body) { this.body = body }
    abort() { this.onabort() }
  }
  vi.stubGlobal('XMLHttpRequest', XHR)
  return () => XHR.instanta
}
describe('încărcarea multipart', () => {
  it('trimite cookie-uri, boundary automat și progresul real, apoi așteaptă serverul', async () => {
    const get = fals(), progres = vi.fn(), form = new FormData()
    const p = incarcaFormular('/fisiere', form, { onProgress: progres })
    const x = get()
    expect(x.withCredentials).toBe(true)
    expect(x.headers).toEqual({ 'X-MarkAi': '1' })
    expect(x.body).toBe(form)
    x.upload.onprogress({ loaded: 25, total: 100, lengthComputable: true })
    expect(progres).toHaveBeenLastCalledWith({ trimisi: 25, total: 100, procent: 25 })
    x.upload.onload()
    expect(progres).toHaveBeenLastCalledWith({ procent: 100, asteapta: true })
    x.status = 201; x.responseText = '{"id":7}'; x.onload()
    await expect(p).resolves.toEqual({ id: 7 })
  })
  it('semnalul de anulare oprește cererea și întoarce AbortError', async () => {
    const get = fals(), controller = new AbortController()
    const p = incarcaFormular('/fisiere', new FormData(), { signal: controller.signal })
    const spy = vi.spyOn(get(), 'abort')
    controller.abort()
    await expect(p).rejects.toHaveProperty('name', 'AbortError')
    expect(spy).toHaveBeenCalledOnce()
  })
  it('un semnal deja anulat nu creează o cerere', async () => {
    const get = fals(), controller = new AbortController(); controller.abort()
    await expect(incarcaFormular('/x', new FormData(), { signal: controller.signal })).rejects.toHaveProperty('name', 'AbortError')
    expect(get()).toBeUndefined()
  })
  it.each(['onerror', 'ontimeout'])('%s permite reîncercarea', async event => {
    const get = fals(), p = incarcaFormular('/x', new FormData())
    get()[event]()
    await expect(p).rejects.toMatchObject({ status: 0 })
  })
  it('păstrează mesajul și statusul de validare de pe server', async () => {
    const get = fals(), p = incarcaFormular('/x', new FormData())
    get().status = 413; get().responseText = '{"mesaj":"Limita este 5 MB"}'; get().onload()
    await expect(p).rejects.toMatchObject({ status: 413, message: 'Limita este 5 MB' })
  })
})
