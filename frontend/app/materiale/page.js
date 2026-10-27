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
import Paginare from '@/components/Paginare'
import { CAPITOLE_PE_PAGINA_CAUTARE, LECTII_PE_PAGINA_CAUTARE, MATERII_PE_PAGINA_MATERIALE_ADMIN, MATERII_PE_PAGINA_MATERIALE_ELEV, cuPagina } from '@/lib/pagini'
import { rute } from '@/lib/rute'

const DECOR = decorSVG(5)
const SEMNE = ['castel', 'moara', 'turn', 'casa', 'far', 'conac', 'clopot']

export default function PaginaMateriale() {
  const elev = usePagina('elev', 'admin')
  const [materii, setMaterii] = useState(null)
  const [total, setTotal] = useState(0)
  const [pagina, setPagina] = useState(1)
  const [eroare, setEroare] = useState('')
  const marime = elev?.rol === 'admin' ? MATERII_PE_PAGINA_MATERIALE_ADMIN : MATERII_PE_PAGINA_MATERIALE_ELEV
  const [cautare, setCautareText] = useState('')
  const [paginiCautare, setPaginiCautare] = useState({ lectii: 1, capitole: 1 })
  const [raspuns, setRaspuns] = useState({ cheie: '', date: null, eroare: '' }) // ultimul răspuns al căutării, cu cererea pentru care a venit
  // Un text nou pornește ambele liste de la prima pagină.
  const setCautare = (v) => { setCautareText(v); setPaginiCautare({ lectii: 1, capitole: 1 }) }

  useEffect(() => {
    if (elev) api(cuPagina('/materii', pagina, marime)).then((r) => { setMaterii(r.elemente); setTotal(r.total) }).catch((e) => setEroare(e.message))
  }, [elev, pagina, marime])

  // Caută la o pauză scurtă după ultima tastă (o pagină nouă se cere imediat); răspunsurile pentru o cerere veche sunt ignorate.
  const q = cautare.trim()
  const cheie = cuPagina(`/cautare?q=${encodeURIComponent(q)}&marimeCapitole=${CAPITOLE_PE_PAGINA_CAUTARE}`
    + (paginiCautare.capitole > 1 ? `&paginaCapitole=${paginiCautare.capitole}` : ''), paginiCautare.lectii, LECTII_PE_PAGINA_CAUTARE)
  const primaPagina = paginiCautare.lectii === 1 && paginiCautare.capitole === 1
  useEffect(() => {
    if (q.length < 2) return
    let anulat = false
    const t = setTimeout(() => {
      api(cheie)
        .then((r) => !anulat && setRaspuns({ cheie, date: r, eroare: '' }))
        .catch((e) => !anulat && setRaspuns({ cheie, date: null, eroare: e.message }))
    }, primaPagina ? 300 : 0)
    return () => {
      anulat = true
      clearTimeout(t)
    }
  }, [q, cheie, primaPagina])
  const activ = q.length >= 2
  const rezultate = activ && raspuns.cheie === cheie ? raspuns.date : null
  const eroareCautare = activ && raspuns.cheie === cheie ? raspuns.eroare : ''
  const urca = () => window.scrollTo?.({ top: 0, behavior: 'smooth' })

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
              {rezultate.totalLectii === 0 && rezultate.totalCapitole === 0 && <p className="tv-gol">Nu am găsit nimic pentru „{q}”.</p>}
              {rezultate.totalLectii > 0 && (
                <h3 style={{ margin: '0 0 8px', color: 'var(--navy)' }}>
                  {rezultate.totalLectii === 1 ? 'O lecție' : `${rezultate.totalLectii} lecții`}
                </h3>
              )}
              {rezultate.lectii.length > 0 && (
                <div className="tv-planuri">
                  {rezultate.lectii.map((l) => (
                    <Link key={l.id} href={rute.parcurs(l.id)} className="tv-plan tv-lectie">
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
              <Paginare eticheta="Paginile lecțiilor găsite" pagina={paginiCautare.lectii} totalPagini={Math.ceil(rezultate.totalLectii / LECTII_PE_PAGINA_CAUTARE)}
                onAlege={(p) => { setPaginiCautare((x) => ({ ...x, lectii: p })); urca() }} />
              {rezultate.capitole.length > 0 && (
                <>
                  <h3 style={{ margin: '18px 0 8px', color: 'var(--navy)' }}>
                    {rezultate.totalCapitole === 1 ? 'Un capitol' : `${rezultate.totalCapitole} capitole`}
                  </h3>
                  <ul className="tv-lista">
                    {rezultate.capitole.map((c) => (
                      <li key={c.id}>
                        <Link href={rute.parcurs(c.parcursId)} className="tv-rand">
                          <div>
                            <b>{c.titlu}</b>
                            <small>{c.materie} · {c.parcurs} · {numeClasa(c.clasa)}{c.capitol ? ` · ${c.capitol}` : ''}</small>
                          </div>
                        </Link>
                      </li>
                    ))}
                  </ul>
                  <Paginare compact eticheta="Paginile capitolelor găsite" pagina={paginiCautare.capitole} totalPagini={Math.ceil(rezultate.totalCapitole / CAPITOLE_PE_PAGINA_CAUTARE)}
                    onAlege={(p) => setPaginiCautare((x) => ({ ...x, capitole: p }))} />
                </>
              )}
            </>
          )}
          {activ && !rezultate && !eroareCautare && <p className="tv-asteapta">Se caută...</p>}
          {!activ && <div className="tv-planuri">
            {materii?.map((m, i) => (
              <Link key={m.id} href={rute.materie(m.id)} className="tv-plan tv-materie">
                {/* Același desen pentru o materie pe orice pagină. */}
                <svg viewBox="40 20 260 160" aria-hidden="true" dangerouslySetInnerHTML={{ __html: insulaSVG({ x: 170, y: 100, seed: 7 + ((pagina - 1) * marime + i) * 13, semn: SEMNE[((pagina - 1) * marime + i) % SEMNE.length] }) }} />
                <h3>{m.denumire}</h3>
                {m.descriere && <p className="tv-indiciu">{m.descriere}</p>}
                <div className="tv-bara tv-bara-verde"><i style={{ width: `${m.numarLectii ? (m.lectiiCompletate / m.numarLectii) * 100 : 0}%` }} /></div>
                <span className="tv-cine">{m.lectiiCompletate} din {m.numarLectii} {m.numarLectii === 1 ? 'lecție terminată' : 'lecții terminate'}</span>
              </Link>
            ))}
            {materii?.length === 0 && <p className="tv-gol">Încă nu există materii.</p>}
          </div>}
          {!activ && <Paginare pagina={pagina} totalPagini={Math.ceil(total / marime)} onAlege={(p) => { setPagina(p); urca() }} />}
        </div>
      </div>
    </main>
  )
}
