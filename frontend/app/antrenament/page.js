'use client'
// /antrenament : capitolele terminate, ca să le reiei (video, notițe, întrebări). Reluarea nu mai dă XP.

import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'
import TabNivel from '@/components/TabNivel'

const DECOR = decorSVG(19)
const FORMAT = new Intl.DateTimeFormat('ro-RO', { day: 'numeric', month: 'long' })

export default function PaginaAntrenament() {
  const elev = usePagina('elev')
  const [progres, setProgres] = useState(null)
  const [deschis, setDeschis] = useState(null)
  const [cautare, setCautare] = useState('')

  useEffect(() => {
    if (elev) api('/progres').then(setProgres).catch(() => setProgres({ cursuriCompletate: [] }))
  }, [elev])

  const grupuri = useMemo(() => {
    const g = new Map()
    const text = cautare.trim().toLowerCase()
    for (const c of progres?.cursuriCompletate ?? []) {
      if (text && !`${c.titlu} ${c.parcurs} ${c.capitol ?? ''}`.toLowerCase().includes(text)) continue
      if (!g.has(c.materieId)) g.set(c.materieId, { materie: c.materie, materieId: c.materieId, cursuri: [] })
      g.get(c.materieId).cursuri.push(c)
    }
    return [...g.values()]
  }, [progres, cautare])

  if (!elev || !progres) return <Incarcare />
  const total = progres.cursuriCompletate.length

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Antrenament</span>
          <h1>Recapitulează ce ai învățat.</h1>
          <p>{total ? `Ai terminat ${total} ${total === 1 ? 'capitol' : 'capitole'}. Reia notițele, videoclipul sau întrebările oricând.` : 'Capitolele pe care le termini apar aici, ca să le poți relua înainte de examen.'}</p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          {total === 0 ? (
            <div className="tv-cutie" style={{ justifyItems: 'start' }}>
              <h3>Încă nu ai capitole terminate</h3>
              <p>Pornește pe hartă: fiecare capitol e terminat când treci testul de la final.</p>
              <Link className="tv-btn" href="/materiale" style={{ width: 'auto', textDecoration: 'none' }}>Mergi la materiale</Link>
            </div>
          ) : (
            <>
              <label className="tv-eticheta-camp" style={{ maxWidth: 420 }}>
                <span>Caută un capitol</span>
                <input className="tv-camp-text" value={cautare} onChange={(e) => setCautare(e.target.value)} placeholder="ex. Harap-Alb" />
              </label>
              {grupuri.map((g) => (
                <section key={g.materieId} className="tv-cutie">
                  <h3>{g.materie}</h3>
                  <ul className="tv-lista">
                    {g.cursuri.map((c) => (
                      <li key={c.cursId} className="tv-rand">
                        <div>
                          <b>{c.titlu}</b>
                          <small>{c.parcurs} · terminat pe {FORMAT.format(new Date(c.completatLa))}</small>
                        </div>
                        <span className="tv-butoane">
                          <button className="tv-pastila tv-pastila-aur" onClick={() => setDeschis(c)}>Notițe și video</button>
                          <Link className="tv-pastila" href={`/nivel/${c.cursId}`}>Întrebări</Link>
                        </span>
                      </li>
                    ))}
                  </ul>
                </section>
              ))}
              {grupuri.length === 0 && <p className="tv-gol">Niciun capitol nu se potrivește căutării.</p>}
            </>
          )}
        </div>
      </div>

      {deschis && (
        <TabNivel
          key={deschis.cursId}
          insula={{ titlu: deschis.parcurs, autor: deschis.materie }}
          nivel={{ id: deschis.cursId, titlu: 'Recapitulare', curs: { titlu: deschis.titlu } }}
          activitati={{ play: true }}
          onInchide={() => setDeschis(null)}
          onActualizare={() => {}}
        />
      )}
    </main>
  )
}
