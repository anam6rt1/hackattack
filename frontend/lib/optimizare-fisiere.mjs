// Optimizarea este opțională; documentele și dovezile nu se modifică implicit.
export async function optimizeazaFotografie(fisier, { original = false, signal } = {}) {
  if (signal?.aborted) throw new DOMException('Procesare anulată', 'AbortError')
  const neschimbat = { fisier, economisiti: 0 }
  if (original || !fisier || !/^image\/(jpeg|png|webp)$/.test(fisier.type)
      || fisier.size < 128 * 1024 || fisier.size > 16 * 1024 * 1024
      || typeof Worker === 'undefined') return neschimbat

  let worker
  let timer
  let abort
  try {
    worker = new Worker(new URL('./optimizare-imagine.worker.js', import.meta.url))
    const blob = await new Promise((resolve, reject) => {
      abort = () => reject(new DOMException('Procesare anulată', 'AbortError'))
      signal?.addEventListener('abort', abort, { once: true })
      timer = setTimeout(() => reject(new Error('Procesarea a durat prea mult')), 12000)
      worker.onmessage = ({ data }) => data?.blob ? resolve(data.blob) : reject(new Error('Format indisponibil'))
      worker.onerror = () => reject(new Error('Optimizare indisponibilă'))
      worker.onmessageerror = () => reject(new Error('Răspuns invalid'))
      worker.postMessage(fisier)
    })
    // Acceptăm numai un WebP real și o economie de cel puțin 5%.
    if (blob.type !== 'image/webp' || !blob.size || blob.size > fisier.size * 0.95) return neschimbat
    const nume = fisier.name.replace(/\.[^.]+$/, '') + '.webp'
    const optimizat = new File([blob], nume, { type: blob.type, lastModified: fisier.lastModified })
    return { fisier: optimizat, economisiti: fisier.size - optimizat.size }
  } catch (e) {
    if (e.name === 'AbortError') throw e
    return neschimbat
  } finally {
    if (abort) signal?.removeEventListener('abort', abort)
    clearTimeout(timer)
    worker?.terminate()
  }
}
