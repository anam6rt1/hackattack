'use client'
// /materiale/1 : Lecțiile unei materii (fiecare făcută de un profesor), cu filtru de clasă și de loc.
// Clasa implicită: clasa elevului + „Bac”; fără clasă setată, toate. Loc: toate / organizația mea / doar publice /
// aproape de mine (județul din Setări; cele din localitatea elevului primele).

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import { CLASE, numeClasa } from '@/lib/clase'

export default function PaginaMaterie() {
  const { id } = useParams()
  const elev = usePagina('elev', 'admin')
  const [materie, setMaterie] = useState(null)
  const [lectii, setLectii] = useState(null)
  const [aleseDeElev, setAlese] = useState(null) // null = implicitul de mai jos; [] = toate
  const [unde, setUnde] = useState('toate')
  const [eroare, setEroare] = useState('')

  const alese = aleseDeElev ?? (elev?.clasa ? [elev.clasa, 'Bac'] : [])

  useEffect(() => {
    if (!elev) return
    api(`/materii/${id}`).then(setMaterie).catch((e) => setEroare(e.message))
  }, [elev, id])

  const q = new URLSearchParams()
  if (alese.length) q.set('clasa', alese.join(','))
  if (unde !== 'toate') q.set('filtru', unde)
  const filtru = q.size ? `?${q}` : ''
  useEffect(() => {
    if (!elev) return
    api(`/materii/${id}/parcursuri${filtru}`).then((r) => setLectii(r.elemente)).catch((e) => setEroare(e.message))
  }, [elev, id, filtru])

  if (eroare) return <Incarcare text={eroare} />
  if (!elev || !materie || !lectii) return <Incarcare />

  const comuta = (c) => setAlese(alese.includes(c) ? alese.filter((x) => x !== c) : [...alese, c])

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <Link href="/materiale" className="tv-erou-pill">← Materii</Link>
          <h1>{materie.denumire}</h1>
          <p>Alege o lecție. Fiecare lecție e o hartă cu insule, câte una pentru fiecare capitol; poți începe cu orice capitol.</p>
          <span className="tv-pastila tv-pastila-aur">{materie.cursuriCompletate} din {materie.numarCursuri} capitole terminate</span>
        </section>
        <div className="tv-coala">
          <div className="tv-filtre" role="group" aria-label="Filtru după clasă">
            <button type="button" className="tv-filtru" aria-pressed={alese.length === 0} onClick={() => setAlese([])}>Toate</button>
            {CLASE.map((c) => (
              <button key={c} type="button" className="tv-filtru" aria-pressed={alese.includes(c)} onClick={() => comuta(c)}>
                {c === 'Bac' ? 'Bac' : `a ${c}-a`}
              </button>
            ))}
          </div>
          <div className="tv-filtre" role="group" aria-label="Filtru după loc">
            {[['toate', 'Toate'], ['organizatia-mea', 'Organizația mea'], ['public', 'Pentru toți'], ['local', 'Aproape de mine']].map(([v, t]) => (
              <button key={v} type="button" className="tv-filtru" aria-pressed={unde === v} disabled={v === 'local' && !elev.judetId} onClick={() => setUnde(v)}>
                {t}
              </button>
            ))}
            {!elev.judetId && <Link href="/setari" className="tv-indiciu">Alege-ți județul în Setări ca să vezi lecțiile din apropiere.</Link>}
          </div>
          <div className="tv-planuri">
            {lectii.map((l) => (
              <Link key={l.id} href={`/parcurs/${l.id}`} className="tv-plan tv-lectie">
                <span className="tv-butoane">
                  <span className="tv-pastila tv-pastila-aur">{numeClasa(l.clasa)}</span>
                  {l.vizibilitate === 'organizatie' && <span className="tv-pastila">Doar organizația</span>}
                  {!l.publicat && <span className="tv-pastila tv-pastila-rosu">Nepublicată</span>}
                </span>
                <h3>{l.titlu}</h3>
                {l.descriere && <p className="tv-indiciu">{l.descriere}</p>}
                <span className="tv-cine">{l.autor ?? 'Autor necunoscut'}{l.organizatie ? ` · ${l.organizatie}` : ' · conținut al platformei'}</span>
                {l.loc && <span className="tv-cine">{l.loc}{l.inLocalitateaTa ? ' · în localitatea ta' : ''}</span>}
                <div className="tv-bara tv-bara-verde"><i style={{ width: `${l.numarCapitole ? (l.capitoleCompletate / l.numarCapitole) * 100 : 0}%` }} /></div>
                <span className="tv-cine">{l.capitoleCompletate} din {l.numarCapitole} {l.numarCapitole === 1 ? 'capitol terminat' : 'capitole terminate'}</span>
              </Link>
            ))}
          </div>
          {lectii.length === 0 && (
            <p className="tv-gol">
              {unde === 'local' ? 'Nu există lecții ale organizațiilor din județul tău aici.' : unde !== 'toate' ? 'Nu există lecții pentru filtrul ales.'
                : alese.length ? 'Nu există lecții pentru clasa aleasă. Alege „Toate” sau altă clasă.' : 'Materia nu are încă lecții.'}
            </p>
          )}
        </div>
      </div>
    </main>
  )
}
