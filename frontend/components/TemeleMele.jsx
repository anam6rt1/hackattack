'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api, dataOra } from '@/lib/api'
import Paginare from '@/components/Paginare'
import { rute } from '@/lib/rute'

export default function TemeleMele() {
  const [pagina, setPagina] = useState(1)
  const [date, setDate] = useState(null)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    let activ = true
    api(`/teme?pagina=${pagina}`).then(r => { if (activ) { setDate(r); setEroare('') } }).catch(e => { if (activ) setEroare(e.message) })
    return () => { activ = false }
  }, [pagina])
  return <section className="tv-cutie" aria-label="Temele mele">
    <h2>Temele mele</h2>
    <p>Treci testele lecțiilor pentru a preda tema. Lecțiile deja promovate sunt luate în calcul.</p>
    {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    {!date && !eroare && <p>Se încarcă temele…</p>}
    {date?.elemente.length === 0 && <p>Nu ai teme momentan.</p>}
    {date?.elemente.map(t => {
      const predata = t.lectii.length > 0 && t.lectii.every(l => l.promovatLa)
      const intarziata = predata && t.lectii.some(l => new Date(l.promovatLa) > new Date(t.termenLa))
      return <article className="tv-cutie" key={t.id}>
        <h3>{t.titlu}</h3><p>{t.organizatie} · Termen: {dataOra(t.termenLa)}</p>
        {t.descriere && <p style={{ whiteSpace: 'pre-wrap' }}>{t.descriere}</p>}
        <strong>{predata ? (intarziata ? 'Predată cu întârziere' : 'Predată') : new Date(t.termenLa) < new Date() ? 'Termen depășit — poți continua' : 'De rezolvat'}</strong>
        <ul className="tv-lista">{t.lectii.map(l => <li key={l.id} className="tv-rand"><Link href={rute.parcurs(l.id)}>{l.titlu}</Link><span>{l.promovatLa ? 'Promovată' : <Link href={rute.test(l.id)}>Deschide testul</Link>}</span></li>)}</ul>
      </article>
    })}
    <Paginare pagina={pagina} totalPagini={Math.ceil((date?.total ?? 0) / 10)} onAlege={p => { setDate(null); setPagina(p) }} />
  </section>
}
