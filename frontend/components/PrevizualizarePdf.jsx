'use client'
import { useEffect, useRef, useState } from 'react'

export default function PrevizualizarePdf({ fisier }) {
  const canvas = useRef(null)
  const link = useRef(null)
  const [document, setDocument] = useState(null)
  const [pagina, setPagina] = useState(1)
  const [afisata, setAfisata] = useState(null)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    let activ = true, task, port, worker
    const local = URL.createObjectURL(new Blob([fisier], { type: 'application/pdf' }))
    if (link.current) link.current.href = local
    const timer = setTimeout(() => {
      if (activ) { setEroare('PDF-ul este prea complex pentru previzualizare.'); task?.destroy(); port?.terminate() }
    }, 20000)
    async function incarca() {
      if (fisier.size > 20 * 1024 * 1024) throw new Error('Previzualizarea PDF este limitată la 20 MB.')
      const pdfjs = await import('pdfjs-dist')
      if (!activ) return
      port = new Worker(new URL('pdfjs-dist/build/pdf.worker.min.mjs', import.meta.url), { type: 'module' })
      worker = pdfjs.PDFWorker.create({ port })
      const data = new Uint8Array(await fisier.arrayBuffer())
      if (!activ) return
      task = pdfjs.getDocument({ data, worker, useWorkerFetch: false, enableXfa: false, useSystemFonts: true })
      const doc = await task.promise
      if (activ) { clearTimeout(timer); setDocument(doc) }
    }
    incarca().catch(e => { if (activ) { clearTimeout(timer); setEroare(e.name === 'PasswordException' ? 'PDF protejat prin parolă. Deschide-l în aplicația lui.' : 'PDF-ul nu poate fi previzualizat. Poți deschide originalul.') } })
    return () => { activ = false; clearTimeout(timer); task?.destroy(); worker?.destroy(); port?.terminate(); URL.revokeObjectURL(local) }
  }, [fisier])

  useEffect(() => {
    if (!document) return
    let activ = true, render
    async function afiseaza() {
      const page = await document.getPage(pagina)
      if (!activ) return
      const original = page.getViewport({ scale: 1 })
      const scale = Math.min(1.5, 900 / original.width, Math.sqrt(4_000_000 / (original.width * original.height)))
      const viewport = page.getViewport({ scale })
      canvas.current.width = Math.ceil(viewport.width)
      canvas.current.height = Math.ceil(viewport.height)
      render = page.render({ canvas: canvas.current, viewport })
      await render.promise
      const text = await page.getTextContent()
      if (activ) setAfisata({ pagina, text: text.items.filter(i => 'str' in i).map(i => i.str).join(' ') })
    }
    afiseaza().catch(e => { if (activ && e.name !== 'RenderingCancelledException') setEroare('Pagina nu poate fi afișată. Deschide PDF-ul original.') })
    return () => { activ = false; render?.cancel() }
  }, [document, pagina])

  return <div>
    {eroare ? <p role="alert">{eroare}</p> : !document ? <p role="status">Se deschide PDF-ul…</p> : <>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <button type="button" className="tv-pastila" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>Pagina precedentă</button>
        <span>Pagina {pagina} din {document.numPages}</span>
        <button type="button" className="tv-pastila" disabled={pagina >= document.numPages} onClick={() => setPagina(p => p + 1)}>Pagina următoare</button>
      </div>
      {afisata?.pagina !== pagina && <p role="status">Se afișează pagina…</p>}
    </>}
    <canvas ref={canvas} role="img" aria-label={`Pagina ${pagina} din ${fisier.name}`} style={{ display: document && !eroare ? 'block' : 'none', maxWidth: '100%', height: 'auto', marginTop: 12 }} />
    {afisata?.pagina === pagina && <details><summary>Textul paginii</summary><p style={{ whiteSpace: 'pre-wrap' }}>{afisata.text || 'Pagină fără text selectabil (de exemplu, o scanare).'}</p></details>}
    <p><a ref={link} target="_blank" rel="noreferrer">Deschide previzualizarea într-o filă nouă</a></p>
  </div>
}
