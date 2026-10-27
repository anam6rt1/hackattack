'use client'
// /antrenament : Lecțiile terminate (testul trecut), ca să le reiei: harta cu notițele și videoclipurile, sau testul. Reluarea nu mai dă XP.

import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'
import GreselileMele from '@/components/GreselileMele'
import { numeClasa } from '@/lib/clase'
import { rute } from '@/lib/rute'

const DECOR = decorSVG(19)
const FORMAT = new Intl.DateTimeFormat('ro-RO', { day: 'numeric', month: 'long' })

export default function PaginaAntrenament() {
  const elev = usePagina('elev')
  const [progres, setProgres] = useState(null)
  const [cautare, setCautare] = useState('')

  useEffect(() => {
    if (elev) api('/progres').then(setProgres).catch(() => setProgres({ lectiiCompletate: [] }))
  }, [elev])

  const grupuri = useMemo(() => {
    const g = new Map()
    const text = cautare.trim().toLowerCase()
    for (const l of progres?.lectiiCompletate ?? []) {
      if (text && !l.titlu.toLowerCase().includes(text)) continue
      if (!g.has(l.materieId)) g.set(l.materieId, { materie: l.materie, materieId: l.materieId, lectii: [] })
      g.get(l.materieId).lectii.push(l)
    }
    return [...g.values()]
  }, [progres, cautare])

  if (!elev || !progres) return <Incarcare />
  const total = progres.lectiiCompletate.length

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Antrenament</span>
          <h1>Recapitulează ce ai învățat.</h1>
          <p>{total ? `Ai terminat ${total} ${total === 1 ? 'lecție' : 'lecții'}. Reia notițele, videoclipurile sau testul oricând.` : 'Lecțiile pe care le termini apar aici, ca să le poți relua înainte de examen.'}</p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          <Link className="tv-btn" href="/simulari">Simulări Bac și Evaluare Națională</Link>
          <GreselileMele />
          {total === 0 ? (
            <div className="tv-cutie" style={{ justifyItems: 'start' }}>
              <h3>Încă nu ai lecții terminate</h3>
              <p>Alege o lecție: o termini când treci testul ei, cu întrebări din toate capitolele.</p>
              <Link className="tv-btn" href="/materiale" style={{ width: 'auto', textDecoration: 'none' }}>Mergi la materiale</Link>
            </div>
          ) : (
            <>
              <label className="tv-eticheta-camp" style={{ maxWidth: 420 }}>
                <span>Caută o lecție</span>
                <input className="tv-camp-text" value={cautare} onChange={(e) => setCautare(e.target.value)} placeholder="ex. Harap-Alb" />
              </label>
              {grupuri.map((g) => (
                <section key={g.materieId} className="tv-cutie">
                  <h3>{g.materie}</h3>
                  <ul className="tv-lista">
                    {g.lectii.map((l) => (
                      <li key={l.parcursId} className="tv-rand">
                        <div>
                          <b>{l.titlu}</b>
                          <small>{numeClasa(l.clasa)} · terminată pe {FORMAT.format(new Date(l.completatLa))}</small>
                        </div>
                        <span className="tv-butoane">
                          <Link className="tv-pastila tv-pastila-aur" href={rute.parcurs(l.parcursId)}>Notițe și video</Link>
                          <Link className="tv-pastila" href={rute.test(l.parcursId)}>Test</Link>
                        </span>
                      </li>
                    ))}
                  </ul>
                </section>
              ))}
              {grupuri.length === 0 && <p className="tv-gol">Nicio lecție nu se potrivește căutării.</p>}
            </>
          )}
        </div>
      </div>
    </main>
  )
}
