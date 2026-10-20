'use client'
// /materiale : materiile disponibile; fiecare deschide harta cu insulele ei.
// Căutarea (GET /cautare) găsește lecții și capitole după titlu și descriere, fără diacritice („sarpe” găsește „Șarpe”).

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api } from '@/lib/api'
import { numeClasa } from '@/lib/clase'
import { usePagina } from '@/lib/sesiune'
import { decorSVG, insulaSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'

const DECOR = decorSVG(5)
const SEMNE = ['castel', 'moara', 'turn', 'casa', 'far', 'conac', 'clopot']

export default function PaginaMateriale() {
  const elev = usePagina('elev', 'admin')
  const [materii, setMaterii] = useState(null)
  const [eroare, setEroare] = useState('')
  const [cautare, setCautare] = useState('')
  const [raspuns, setRaspuns] = useState({ q: '', date: null, eroare: '' }) // ultimul răspuns al căutării, cu textul pentru care a venit

  useEffect(() => {
    if (elev) api('/materii').then((r) => setMaterii(r.elemente)).catch((e) => setEroare(e.message))
  }, [elev])

  // Caută la o pauză scurtă după ultima tastă; răspunsurile pentru un text vechi sunt ignorate.
  const q = cautare.trim()
  useEffect(() => {
    if (q.length < 2) return
    let anulat = false
    const t = setTimeout(() => {
      api(`/cautare?q=${encodeURIComponent(q)}`)
        .then((r) => !anulat && setRaspuns({ q, date: r, eroare: '' }))
        .catch((e) => !anulat && setRaspuns({ q, date: null, eroare: e.message }))
    }, 300)
    return () => {
      anulat = true
      clearTimeout(t)
    }
  }, [q])
  const activ = q.length >= 2
  const rezultate = activ && raspuns.q === q ? raspuns.date : null
  const eroareCautare = activ && raspuns.q === q ? raspuns.eroare : ''

  if (!elev || (!materii && !eroare)) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Materiale</span>
          <h1>Alege o materie și pornește pe hartă.</h1>
          <p>Fiecare materie are lecții făcute de profesori. O lecție e o hartă cu insule, câte una pentru fiecare capitol, și poți începe cu oricare.</p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>
        <div className="tv-coala">
          {eroare && <p className="tv-eroare">{eroare}</p>}
          <label className="tv-eticheta-camp" style={{ marginBottom: 18 }}>
            <span>Caută o lecție sau un capitol</span>
            <input className="tv-camp-text" type="search" value={cautare} onChange={(e) => setCautare(e.target.value)} placeholder="ex. Luceafărul, Moara cu noroc, sarpe..." maxLength={100} />
          </label>
          {eroareCautare && <p className="tv-eroare">{eroareCautare}</p>}
          {rezultate && (
            <>
              {rezultate.lectii.length === 0 && rezultate.capitole.length === 0 && <p className="tv-gol">Nu am găsit nimic pentru „{q}”.</p>}
              {rezultate.lectii.length > 0 && (
                <div className="tv-planuri">
                  {rezultate.lectii.map((l) => (
                    <Link key={l.id} href={`/parcurs/${l.id}`} className="tv-plan tv-lectie">
                      <span className="tv-butoane">
                        <span className="tv-pastila tv-pastila-aur">{numeClasa(l.clasa)}</span>
                        {l.vizibilitate === 'organizatie' && <span className="tv-pastila">Doar organizația</span>}
                      </span>
                      <h3>{l.titlu}</h3>
                      {l.descriere && <p className="tv-indiciu">{l.descriere}</p>}
                      <span className="tv-cine">{l.autor ?? 'Autor necunoscut'}{l.organizatie ? ` · ${l.organizatie}` : ' · conținut al platformei'}</span>
                    </Link>
                  ))}
                </div>
              )}
              {rezultate.capitole.length > 0 && (
                <>
                  <h3 style={{ margin: '18px 0 8px', color: 'var(--navy)' }}>Capitole</h3>
                  <ul className="tv-lista">
                    {rezultate.capitole.map((c) => (
                      <li key={c.id}>
                        <Link href={`/parcurs/${c.parcursId}`} className="tv-rand">
                          <div>
                            <b>{c.titlu}</b>
                            <small>{c.materie} · {c.parcurs} · {numeClasa(c.clasa)}{c.capitol ? ` · ${c.capitol}` : ''}</small>
                          </div>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </>
          )}
          {activ && !rezultate && !eroareCautare && <p className="tv-asteapta">Se caută...</p>}
          {!activ && <div className="tv-planuri">
            {materii?.map((m, i) => (
              <Link key={m.id} href={`/materiale/${m.id}`} className="tv-plan tv-materie">
                <svg viewBox="40 20 260 160" aria-hidden="true" dangerouslySetInnerHTML={{ __html: insulaSVG({ x: 170, y: 100, seed: 7 + i * 13, semn: SEMNE[i % SEMNE.length] }) }} />
                <h3>{m.denumire}</h3>
                {m.descriere && <p className="tv-indiciu">{m.descriere}</p>}
                <div className="tv-bara tv-bara-verde"><i style={{ width: `${m.numarCursuri ? (m.cursuriCompletate / m.numarCursuri) * 100 : 0}%` }} /></div>
                <span className="tv-cine">{m.cursuriCompletate} din {m.numarCursuri} capitole terminate</span>
              </Link>
            ))}
            {materii?.length === 0 && <p className="tv-gol">Încă nu există materii.</p>}
          </div>}
        </div>
      </div>
    </main>
  )
}
