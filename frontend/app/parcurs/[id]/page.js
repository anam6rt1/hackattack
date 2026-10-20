'use client'
// /parcurs/7 : harta unei Lecții — câte o insulă pentru fiecare capitol, toate deschise; ordinea e doar o recomandare.

import { useCallback, useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import HartaInsule from '@/components/HartaInsule'
import Incarcare from '@/components/Incarcare'
import { numeClasa } from '@/lib/clase'

export default function PaginaParcurs() {
  const { id } = useParams()
  const elev = usePagina('elev', 'admin')
  const [lectie, setLectie] = useState(null)
  const [eroare, setEroare] = useState('')

  const incarca = useCallback(() => {
    api(`/parcursuri/${id}`).then(setLectie).catch((e) => setEroare(e.message))
  }, [id])

  useEffect(() => {
    if (elev) incarca()
  }, [elev, incarca])

  if (eroare) return <Incarcare text={eroare} />
  if (!elev || !lectie) return <Incarcare />

  const terminate = lectie.capitole.filter((c) => c.progres?.quizPromovat).length
  return (
    <main className="tv-pagina">
      <div className="tv-continut tv-harta-pagina">
        <div className="tv-harta-antet">
          <Link href={`/materiale/${lectie.materieId}`} className="tv-erou-pill">← {lectie.materie}</Link>
          <h1>{lectie.titlu}</h1>
          <span className="tv-pastila tv-pastila-aur">{numeClasa(lectie.clasa)} · {terminate} / {lectie.capitole.length} capitole</span>
        </div>
        <p className="tv-harta-autor">
          {lectie.autor ?? 'Autor necunoscut'}{lectie.organizatie ? ` · ${lectie.organizatie}` : ' · conținut al platformei'}
          {lectie.vizibilitate === 'organizatie' ? ' · doar pentru organizație' : ''}
        </p>
        {lectie.capitole.length ? (
          <HartaInsule lectie={lectie} onActualizare={incarca} />
        ) : (
          <p className="tv-gol" style={{ background: '#fff' }}>Lecția nu are încă capitole.</p>
        )}
      </div>
    </main>
  )
}
