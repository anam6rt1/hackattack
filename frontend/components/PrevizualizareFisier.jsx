'use client'
import { useEffect, useRef, useState } from 'react'
import { API, marime } from '@/lib/api'
import PrevizualizarePdf from './PrevizualizarePdf'
import PrevizualizareExcel from './PrevizualizareExcel'

function Office({ fisier }) {
  const [pdf, setPdf] = useState(null)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    const c = new AbortController()
    const timer = setTimeout(() => c.abort(), 95000)
    let activ = true
    async function deschide() {
      const formular = new FormData()
      formular.append('fisier', fisier)
      const r = await fetch(`${API}/fisiere/previzualizare`, { method: 'POST', credentials: 'include', headers: { 'X-MarkAi': '1' }, body: formular, signal: c.signal })
      if (!r.ok) { const e = await r.json().catch(() => null); throw new Error(e?.mesaj || 'Documentul nu poate fi deschis momentan.') }
      const blob = await r.blob()
      if (blob.size > 20 * 1024 * 1024 || !blob.type.includes('application/pdf')) throw new Error('Documentul nu poate fi previzualizat. Descarcă-l pentru a-l deschide.')
      if (activ) setPdf(new File([blob], `${fisier.name}.pdf`, { type: 'application/pdf' }))
    }
    deschide().catch(e => { if (activ) setEroare(e.name === 'AbortError' ? 'Deschiderea durează prea mult. Încearcă din nou.' : e.message) }).finally(() => clearTimeout(timer))
    return () => { activ = false; clearTimeout(timer); c.abort() }
  }, [fisier])
  if (eroare) return <p role="alert">{eroare}</p>
  return pdf ? <PrevizualizarePdf fisier={pdf} /> : <p role="status">Se deschide documentul…</p>
}

function Continut({ fisier }) {
  const media = useRef(null)
  const ext = fisier.name.split('.').pop().toLowerCase()
  const imagine = /^image\/(jpeg|png|webp|gif)$/.test(fisier.type)
  useEffect(() => {
    let local
    if (imagine) {
      local = URL.createObjectURL(fisier)
      if (media.current) media.current.src = local
    }
    return () => { if (local) URL.revokeObjectURL(local) }
  }, [fisier, ext, imagine])
  // Blob URLs are local and must be rendered directly, without the Next image proxy.
  // eslint-disable-next-line @next/next/no-img-element
  if (imagine) return <img ref={media} alt={`Previzualizare: ${fisier.name}`} style={{ maxWidth: '100%', maxHeight: 320, objectFit: 'contain' }} />
  if (ext === 'pdf' || fisier.type === 'application/pdf') return <PrevizualizarePdf fisier={fisier} />
  if (ext === 'xlsx') return <PrevizualizareExcel fisier={fisier} />
  if (['docx', 'pptx', 'xlsx'].includes(ext)) return <Office fisier={fisier} />
  return <p>Previzualizarea acestui format nu este disponibilă. Pentru fișiere .doc, .ppt sau .xls, salvează o copie .docx, .pptx sau .xlsx.</p>
}

export default function PrevizualizareFisier({ fisier, eticheta = 'Previzualizează', deschis = false, origine = null, marimeOcteti = fisier?.size }) {
  const [open, setOpen] = useState(deschis)
  if (!fisier) return null
  return <div style={{ minWidth: 0, border: '1px solid #cbd5e1', borderRadius: 12, padding: 12 }}>
    <button type="button" className="tv-pastila" aria-expanded={open} onClick={() => setOpen(!open)}>{open ? 'Ascunde previzualizarea' : eticheta}: {fisier.name}</button>
    <small style={{ display: 'block' }}>{marime(marimeOcteti)}{origine && ` · ${origine}`}</small>
    {open && <div style={{ maxHeight: 560, overflow: 'auto', marginTop: 12 }}><Continut key={`${fisier.name}-${fisier.lastModified}-${fisier.size}`} fisier={fisier} /></div>}
  </div>
}
