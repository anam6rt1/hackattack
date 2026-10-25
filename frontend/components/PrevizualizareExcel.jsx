'use client'
import { useEffect, useState } from 'react'
import { API } from '@/lib/api'
import PrevizualizarePdf from './PrevizualizarePdf'

export default function PrevizualizareExcel({ fisier, salvat }) {
  const [foi, setFoi] = useState(null)
  const [selectata, setSelectata] = useState(0)
  const [pdf, setPdf] = useState(null)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    const c = new AbortController()
    const timer = setTimeout(() => c.abort(), 95000)
    let activ = true
    async function citeste() {
      const formular = new FormData()
      if (fisier) formular.append('fisier', fisier)
      const cale = salvat ? `/fisiere/${salvat.id}/${foi === null ? 'foi' : `previzualizare?foaie=${selectata}`}`
        : `/fisiere/previzualizare?${foi === null ? 'foi=true' : `foaie=${selectata}`}`
      const r = await fetch(API + cale, { method: salvat ? 'GET' : 'POST', body: salvat ? undefined : formular,
        credentials: 'include', headers: { 'X-MarkAi': '1' }, signal: c.signal })
      if (!r.ok) { const e = await r.json().catch(() => null); throw new Error(e?.mesaj || 'Foaia nu poate fi deschisă momentan.') }
      if (foi === null) {
        const lista = await r.json()
        if (!Array.isArray(lista) || !lista.length || !lista.every(x => typeof x === 'string')) throw new Error('Documentul nu are foi de afișat.')
        if (activ) setFoi(lista)
      } else {
        const blob = await r.blob()
        if (blob.size > 20 * 1024 * 1024 || !blob.type.includes('application/pdf')) throw new Error('Foaia este prea mare pentru previzualizare.')
        if (activ) setPdf({ index: selectata, fisier: new File([blob], `${salvat?.nume || fisier.name} — ${foi[selectata]}.pdf`, { type: 'application/pdf' }) })
      }
    }
    citeste().catch(e => { if (activ) setEroare(e.name === 'AbortError' ? 'Deschiderea durează prea mult. Reîncearcă.' : e.message) }).finally(() => clearTimeout(timer))
    return () => { activ = false; clearTimeout(timer); c.abort() }
  }, [fisier, salvat, foi, selectata])
  return <div>
    {foi && <label>Foaia de calcul <select aria-label="Foaia de calcul" value={selectata} onChange={e => { setEroare(''); setSelectata(Number(e.target.value)) }}>
      {foi.map((nume, i) => <option key={i} value={i}>{nume}</option>)}
    </select></label>}
    {eroare ? <p role="alert">{eroare}</p> : pdf?.index === selectata ? <PrevizualizarePdf key={selectata} fisier={pdf.fisier} /> : <p role="status">Se deschide foaia…</p>}
  </div>
}
