'use client'
// /materiale : materiile disponibile; fiecare deschide harta cu insulele ei.

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { decorSVG, insulaSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'

const DECOR = decorSVG(5)
const SEMNE = ['castel', 'moara', 'turn', 'casa', 'far', 'conac', 'clopot']

export default function PaginaMateriale() {
  const elev = usePagina('elev', 'admin')
  const [materii, setMaterii] = useState(null)
  const [eroare, setEroare] = useState('')

  useEffect(() => {
    if (elev) api('/materii').then((r) => setMaterii(r.elemente)).catch((e) => setEroare(e.message))
  }, [elev])

  if (!elev || (!materii && !eroare)) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Materiale</span>
          <h1>Alege o materie și pornește pe hartă.</h1>
          <p>Fiecare capitol e o insulă. Termini lecțiile una câte una și se deschide podul spre următoarea.</p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>
        <div className="tv-coala">
          {eroare && <p className="tv-eroare">{eroare}</p>}
          <div className="tv-planuri">
            {materii?.map((m, i) => (
              <Link key={m.id} href={`/materiale/${m.id}`} className="tv-plan tv-materie">
                <svg viewBox="40 20 260 160" aria-hidden="true" dangerouslySetInnerHTML={{ __html: insulaSVG({ x: 170, y: 100, seed: 7 + i * 13, semn: SEMNE[i % SEMNE.length] }) }} />
                <h3>{m.denumire}</h3>
                {m.descriere && <p className="tv-indiciu">{m.descriere}</p>}
                <div className="tv-bara tv-bara-verde"><i style={{ width: `${m.numarCursuri ? (m.cursuriCompletate / m.numarCursuri) * 100 : 0}%` }} /></div>
                <span className="tv-cine">{m.cursuriCompletate} din {m.numarCursuri} lecții terminate</span>
              </Link>
            ))}
            {materii?.length === 0 && <p className="tv-gol">Încă nu există materii.</p>}
          </div>
        </div>
      </div>
    </main>
  )
}
