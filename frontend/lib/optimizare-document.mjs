export async function optimizeazaDocument(fisier, { original = false, signal } = {}) {
  if (signal?.aborted) throw new DOMException('Procesare anulată', 'AbortError')
  const intact = motiv => ({ fisier, economisiti: 0, motiv })
  if (original) return intact('Ai ales păstrarea originalului.')
  const ext = fisier.name.split('.').pop().toLowerCase()
  if (!['pdf', 'docx', 'pptx', 'xlsx'].includes(ext)) return intact('Format fără optimizare locală disponibilă.')
  if (fisier.size < 32 * 1024) return intact('Fișier mic: se păstrează originalul.')
  if (fisier.size > 20 * 1024 * 1024) return intact('Peste 20 MB: se păstrează originalul.')
  if (typeof Worker === 'undefined') return intact('Browserul nu permite optimizarea locală.')
  let worker, timer, abort
  try {
    worker = new Worker(new URL('./optimizare-document.worker.js', import.meta.url))
    const r = await new Promise((resolve, reject) => {
      abort = () => reject(new DOMException('Procesare anulată', 'AbortError'))
      signal?.addEventListener('abort', abort, { once: true })
      timer = setTimeout(() => reject(new Error('Timeout')), 20000)
      worker.onmessage = e => resolve(e.data)
      worker.onerror = () => reject(new Error('Worker indisponibil'))
      worker.onmessageerror = () => reject(new Error('Răspuns invalid'))
      worker.postMessage(fisier)
    })
    if (!r.bytes) return intact(r.motiv || 'Optimizarea nu este disponibilă.')
    if (!(r.bytes instanceof Uint8Array) || !r.bytes.length || r.bytes.length > fisier.size * 0.95)
      return intact('Economia este sub 5% sau rezultatul este mai mare: păstrăm originalul.')
    const type = { pdf: 'application/pdf', docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', pptx: 'application/vnd.openxmlformats-officedocument.presentationml.presentation', xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }[ext]
    const output = new File([r.bytes], fisier.name, { type, lastModified: fisier.lastModified })
    return { fisier: output, economisiti: fisier.size - output.size, motiv: 'Optimizat local, fără reducerea calității imaginilor.' }
  } catch (e) {
    if (e.name === 'AbortError') throw e
    return intact('Optimizarea nu a putut fi terminată: păstrăm originalul.')
  } finally {
    clearTimeout(timer)
    if (abort) signal?.removeEventListener('abort', abort)
    worker?.terminate()
  }
}
