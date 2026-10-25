import { API, EroareApi } from './api'

// XHR exposes actual bytes sent; fetch does not offer upload progress in all supported browsers.
export function incarcaFormularSimplu(cale, formular, { signal, onProgress, timeout = 120000, metoda = 'POST' } = {}) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) return reject(new DOMException('Încărcare anulată', 'AbortError'))
    const xhr = new XMLHttpRequest()
    const abort = () => { xhr.abort(); reject(new DOMException('Încărcare anulată', 'AbortError')) }
    const cleanup = () => signal?.removeEventListener('abort', abort)
    xhr.open(metoda, API + cale)
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

async function sesiune(cale, { signal, metoda = 'GET', corp } = {}) {
  const r = await fetch(API + cale, { method: metoda, credentials: 'include', signal,
    headers: { 'X-MarkAi': '1', ...(corp ? { 'Content-Type': 'application/json' } : {}) }, body: corp ? JSON.stringify(corp) : undefined })
  const data = r.status === 204 ? null : await r.json().catch(() => null)
  if (!r.ok) throw new EroareApi(r.status, data?.mesaj || 'Încărcarea nu a reușit. Încearcă din nou.', data)
  return data
}

export async function incarcaFormular(cale, formular, optiuni = {}) {
  const lectie = cale.match(/^\/parcursuri\/(\d+)\/fisiere(?:\?|$)/)
  const fisier = formular.get('fisier')
  if (!lectie || !(fisier instanceof File) || !globalThis.crypto?.subtle) return incarcaFormularSimplu(cale, formular, optiuni)
  const { signal, onProgress } = optiuni
  const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', await fisier.arrayBuffer())), x => x.toString(16).padStart(2, '0')).join('')
  signal?.throwIfAborted()
  const cheie = `markai-upload:${cale}:${hash}`
  let token
  try { token = localStorage.getItem(cheie) } catch {}
  if (token && !/^[a-f0-9]{32}$/.test(token)) token = null
  let stare
  if (token) {
    try { stare = await sesiune(`/incarcari/${token}`, { signal }) }
    catch (e) { if (e.status !== 404) throw e; token = null }
  }
  if (!token) {
    try {
      stare = await sesiune(`/parcursuri/${lectie[1]}/incarcari`, { signal, metoda: 'POST', corp: {
        nume: fisier.name, marime: fisier.size, sha256: hash, inlocuieste: new URLSearchParams(cale.split('?')[1]).get('inlocuieste') === 'true',
      } })
    } catch (e) {
      // Older API versions remain usable during a rolling deployment.
      if (e.status === 404 || e.status === 405) return incarcaFormularSimplu(cale, formular, optiuni)
      throw e
    }
    token = stare?.token
    if (!/^[a-f0-9]{32}$/.test(token)) throw new Error('Încărcarea nu poate fi pornită. Încearcă din nou.')
    try { localStorage.setItem(cheie, token) } catch {}
  }
  let offset = stare.offset
  const bucata = stare.bucata
  if (!Number.isSafeInteger(offset) || offset < 0 || offset > fisier.size || !Number.isSafeInteger(bucata) || bucata <= 0 || bucata > 4 * 1024 * 1024) throw new Error('Încărcarea nu poate fi reluată.')
  while (offset < fisier.size) {
    const inceput = offset, final = Math.min(offset + bucata, fisier.size)
    const raspuns = await incarcaFormularSimplu(`/incarcari/${token}?offset=${offset}`, fisier.slice(offset, final), { ...optiuni, metoda: 'PUT',
      onProgress: p => onProgress?.({ trimisi: inceput + Math.min(p.trimisi || 0, final - inceput), total: fisier.size,
        procent: Math.round((inceput + Math.min(p.trimisi || 0, final - inceput)) / fisier.size * 100) }),
    })
    if (raspuns?.offset !== final) throw new Error('Încărcarea nu poate continua. Reîncearcă pentru a o relua.')
    offset = final
    onProgress?.({ trimisi: offset, total: fisier.size, procent: Math.round(offset / fisier.size * 100) })
  }
  onProgress?.({ procent: 100, asteapta: true })
  const rezultat = await sesiune(`/incarcari/${token}/finalizeaza`, { metoda: 'POST', signal })
  try { localStorage.removeItem(cheie) } catch {}
  sesiune(`/incarcari/${token}`, { metoda: 'DELETE' }).catch(() => {})
  return rezultat
}
