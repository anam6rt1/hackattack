'use client'
import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'

export default function PrevizualizarePdf({ fisier }) {
  const canvas = useRef(null)
  const link = useRef(null)
  const adresa = useRef('')
  const zona = useRef(null)
  const fereastra = useRef(null)
  const butonEcran = useRef(null)
  const [latime, setLatime] = useState(900)
  const [zoom, setZoom] = useState(1)
  const [extins, setExtins] = useState(false)
  const [document, setDocument] = useState(null)
  const [pagina, setPagina] = useState(1)
  const [afisata, setAfisata] = useState(null)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    const element = zona.current
    if (!element || typeof ResizeObserver === 'undefined') return
    const resize = new ResizeObserver(([entry]) => setLatime(Math.max(200, entry.contentRect.width - 24)))
    resize.observe(element)
    return () => resize.disconnect()
  }, [extins])
  useEffect(() => {
    if (!extins) return
    const anterior = globalThis.document.body.style.overflow
    globalThis.document.body.style.overflow = 'hidden'
    const escape = e => {
      if (e.key === 'Escape') setExtins(false)
      if (e.key === 'Tab') {
        const elemente = fereastra.current?.querySelectorAll('button:not(:disabled), a[href], select, summary')
        if (!elemente?.length) return
        const primul = elemente[0], ultimul = elemente[elemente.length - 1]
        if (e.shiftKey && globalThis.document.activeElement === primul) { e.preventDefault(); ultimul.focus() }
        else if (!e.shiftKey && globalThis.document.activeElement === ultimul) { e.preventDefault(); primul.focus() }
      }
    }
    globalThis.document.addEventListener('keydown', escape)
    const redaFocus = () => butonEcran.current?.focus()
    return () => { globalThis.document.body.style.overflow = anterior; globalThis.document.removeEventListener('keydown', escape); queueMicrotask(redaFocus) }
  }, [extins])
  useEffect(() => {
    let activ = true, task, port, worker
    const local = URL.createObjectURL(new Blob([fisier], { type: 'application/pdf' }))
    adresa.current = local
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
      const scale = Math.min(latime * zoom * (globalThis.devicePixelRatio || 1) / original.width, Math.sqrt(4_000_000 / (original.width * original.height)))
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
  }, [document, pagina, latime, zoom, extins])

  const continut = <div ref={fereastra} role={extins ? 'dialog' : undefined} aria-modal={extins || undefined} aria-label={extins ? `Previzualizare: ${fisier.name}` : undefined}
    style={extins ? { position: 'fixed', inset: 0, zIndex: 10000, background: '#f1f5f9', color: '#152d4e', padding: 16, display: 'flex', flexDirection: 'column', overflow: 'auto' } : { minWidth: 0 }}>
    <button ref={butonEcran} type="button" autoFocus={extins || undefined} className="tv-pastila" onClick={() => setExtins(!extins)}>{extins ? 'Închide ecranul complet' : 'Ecran complet'}</button>
    {eroare ? <p role="alert">{eroare}</p> : !document ? <p role="status">Se deschide PDF-ul…</p> : <>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <button type="button" className="tv-pastila" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>Pagina precedentă</button>
        <span>Pagina {pagina} din {document.numPages}</span>
        <button type="button" className="tv-pastila" disabled={pagina >= document.numPages} onClick={() => setPagina(p => p + 1)}>Pagina următoare</button>
        <button type="button" className="tv-pastila" aria-label="Micșorează" disabled={zoom <= 0.5} onClick={() => setZoom(z => Math.max(0.5, z - 0.25))}>−</button>
        <span aria-live="polite">{Math.round(zoom * 100)}%</span>
        <button type="button" className="tv-pastila" aria-label="Mărește" disabled={zoom >= 3} onClick={() => setZoom(z => Math.min(3, z + 0.25))}>+</button>
        <button type="button" className="tv-pastila" onClick={() => setZoom(1)}>Potrivește la lățime</button>
      </div>
      {afisata?.pagina !== pagina && <p role="status">Se afișează pagina…</p>}
    </>}
    <div ref={zona} style={{ overflow: 'auto', minHeight: 100, flex: extins ? 1 : undefined }}>
      <canvas ref={canvas} role="img" aria-label={`Pagina ${pagina} din ${fisier.name}`} style={{ display: document && !eroare ? 'block' : 'none', width: `${Math.round(latime * zoom)}px`, height: 'auto', marginTop: 12, background: 'white' }} />
    </div>
    {afisata?.pagina === pagina && <details><summary>Textul paginii</summary><p style={{ whiteSpace: 'pre-wrap' }}>{afisata.text || 'Pagină fără text selectabil (de exemplu, o scanare).'}</p></details>}
    <p><a ref={element => { link.current = element; if (element && adresa.current) element.href = adresa.current }} target="_blank" rel="noreferrer">Deschide previzualizarea într-o filă nouă</a></p>
  </div>
  return extins ? createPortal(continut, globalThis.document.body) : continut
}
