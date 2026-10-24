'use client'
import { useEffect, useRef, useState } from 'react'
import { API } from '@/lib/api'
import PrevizualizareFisier from './PrevizualizareFisier'

export default function PrevizualizareSalvat({ fisier }) {
  const [local, setLocal] = useState(null)
  const [eroare, setEroare] = useState('')
  const [busy, setBusy] = useState(false)
  const controller = useRef(null)
  useEffect(() => () => controller.current?.abort(), [])
  async function deschide() {
    const c = new AbortController()
    const timer = setTimeout(() => c.abort(), 95000)
    controller.current = c
    setBusy(true); setEroare('')
    try {
      const limita = 20 * 1024 * 1024
      if (fisier.marimeOcteti > limita) throw new Error('Pentru fișiere peste 20 MB, folosește Deschide sau descarcă documentul.')
      const office = ['docx', 'pptx', 'xlsx'].includes(fisier.tip)
      const r = await fetch(`${API}/fisiere/${fisier.id}${office ? '/previzualizare' : '?descarca=true'}`, { credentials: 'include', headers: { 'X-MarkAi': '1' }, signal: c.signal })
      if (!r.ok) { const e = await r.json().catch(() => null); throw new Error(e?.mesaj || 'Fișierul nu poate fi deschis. Verifică accesul și reîncearcă.') }
      const reader = r.body.getReader()
      const bucati = []
      let total = 0
      try {
        while (true) {
          const { value, done } = await reader.read()
          if (done) break
          total += value.byteLength
          if (total > limita) { await reader.cancel(); throw new Error('Fișierul depășește limita de previzualizare de 20 MB.') }
          bucati.push(value)
        }
      } finally { reader.releaseLock() }
      setLocal(new File(bucati, fisier.nume, { type: r.headers.get('Content-Type') || '' }))
    } catch (e) { if (e.name !== 'AbortError') setEroare(e.message) }
    finally { clearTimeout(timer); setBusy(false); controller.current = null }
  }
  return <div style={{ width: '100%', minWidth: 0, ...(local ? { flex: '1 0 100%' } : {}) }}>
    {!local && <button type="button" className="tv-pastila" disabled={busy} onClick={deschide}>{busy ? 'Se deschide…' : 'Previzualizează documentul'}</button>}
    {busy && <button type="button" className="tv-pastila" onClick={() => controller.current?.abort()}>Anulează deschiderea</button>}
    {eroare && <p role="alert">{eroare}</p>}
    {local && <><PrevizualizareFisier fisier={local} marimeOcteti={fisier.marimeOcteti} deschis origine="Fișier salvat" /><button type="button" className="tv-pastila" onClick={() => setLocal(null)}>Închide documentul</button></>}
  </div>
}
