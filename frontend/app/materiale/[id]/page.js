'use client'
// /materiale/1 : harta cu insulele (capitolele) și lecțiile unei materii.

import { useCallback, useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import HartaInsule from '@/components/HartaInsule'
import Incarcare from '@/components/Incarcare'

export default function PaginaMaterie() {
  const { id } = useParams()
  const elev = usePagina('elev', 'admin')
  const [materie, setMaterie] = useState(null)
  const [cursuri, setCursuri] = useState(null)
  const [eroare, setEroare] = useState('')

  const incarca = useCallback(() => {
    api(`/materii/${id}`).then(setMaterie).catch((e) => setEroare(e.message))
    api(`/materii/${id}/cursuri`).then((r) => setCursuri(r.elemente)).catch((e) => setEroare(e.message))
  }, [id])

  useEffect(() => {
    if (elev) incarca()
  }, [elev, incarca])

  if (eroare) return <Incarcare text={eroare} />
  if (!elev || !materie || !cursuri) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut tv-harta-pagina">
        <div className="tv-harta-antet">
          <Link href="/materiale" className="tv-erou-pill">← Materii</Link>
          <h1>{materie.denumire}</h1>
          <span className="tv-pastila tv-pastila-aur">{materie.cursuriCompletate} / {materie.numarCursuri} lecții</span>
        </div>
        {cursuri.length ? <HartaInsule cursuri={cursuri} onActualizare={incarca} /> : <p className="tv-gol" style={{ background: '#fff' }}>Materia nu are încă lecții.</p>}
      </div>
    </main>
  )
}
