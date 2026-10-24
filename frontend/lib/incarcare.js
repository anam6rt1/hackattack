import { API, EroareApi } from './api'

// XHR exposes actual bytes sent; fetch does not offer upload progress in all supported browsers.
export function incarcaFormular(cale, formular, { signal, onProgress, timeout = 120000 } = {}) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) return reject(new DOMException('Încărcare anulată', 'AbortError'))
    const xhr = new XMLHttpRequest()
    const abort = () => { xhr.abort(); reject(new DOMException('Încărcare anulată', 'AbortError')) }
    const cleanup = () => signal?.removeEventListener('abort', abort)
    xhr.open('POST', API + cale)
    xhr.withCredentials = true
    xhr.timeout = timeout
    xhr.setRequestHeader('X-MarkAi', '1')
    xhr.upload.onprogress = e => onProgress?.({ trimisi: e.loaded, total: e.lengthComputable ? e.total : null,
      procent: e.lengthComputable && e.total ? Math.min(100, Math.round(e.loaded / e.total * 100)) : null })
    xhr.upload.onload = () => onProgress?.({ procent: 100, asteapta: true })
    xhr.onload = () => {
      cleanup()
      let data = null
      try { data = JSON.parse(xhr.responseText) } catch {}
      if (xhr.status >= 200 && xhr.status < 300) resolve(data)
      else reject(new EroareApi(xhr.status, data?.mesaj || (xhr.status === 401 ? 'Trebuie să intri în cont.' : xhr.status === 403 ? 'Nu ai acces aici.' : 'Încărcarea nu a reușit. Încearcă din nou.'), data))
    }
    xhr.onerror = () => { cleanup(); reject(new EroareApi(0, 'Conexiunea s-a întrerupt. Poți reîncerca încărcarea.')) }
    xhr.ontimeout = () => { cleanup(); reject(new EroareApi(0, 'Încărcarea a durat prea mult. Poți reîncerca.')) }
    xhr.onabort = () => { cleanup(); reject(new DOMException('Încărcare anulată', 'AbortError')) }
    signal?.addEventListener('abort', abort, { once: true })
    try { xhr.send(formular) } catch (e) { cleanup(); reject(e) }
  })
}
