'use client'
// Panoul profesorului aprobat: câștiguri, onorariul negociat și ședințele programate.

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api, urlApi } from '@/lib/api'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'
import SloturileMele from '@/components/profesori/SloturileMele'

const DECOR = decorSVG(53)

export default function AcasaProfesor({ utilizator }) {
  const [panou, setPanou] = useState(null)
  const [eroare, setEroare] = useState('')

  useEffect(() => {
    api('/profesori/me').then(setPanou).catch((e) => setEroare(e.message))
  }, [])

  if (eroare) return <Incarcare text={eroare} />
  if (!panou) return <Incarcare />

  const inscrieri = panou.sloturi.reduce((n, s) => n + s.elevi.filter((e) => e.status === 'confirmata').length, 0)

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Panou de profesor</span>
          <h1>Bine ai revenit, {utilizator.nume.split(' ')[0]}!</h1>
          <p>Programează ședințe, vezi cine s-a înscris și adaugă lecții noi pentru elevi.</p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          <Link className="tv-btn" href="/teme">Teme și grupe de elevi</Link>
          <div className="tv-panou">
            <div className="tv-cutie">
              <div className="tv-mic">Câștiguri</div>
              <div className="tv-pret">{panou.castiguri.total} <small>{panou.castiguri.moneda}</small></div>
              <p>{panou.castiguri.inAsteptare} {panou.castiguri.moneda} urmează să fie decontați.</p>
              <p className="tv-indiciu">Onorariul tău negociat: <b>{panou.onorariuNegociat} {panou.moneda}</b> pe elev și ședință plătită.</p>
            </div>
            <div className="tv-cutie">
              <div className="tv-mic">Înscrieri</div>
              <div className="tv-pret">{inscrieri}</div>
              <p>elevi înscriși în total la ședințele tale.</p>
            </div>
            <div className="tv-cutie">
              <div className="tv-nivel">
                {panou.profil.pozaUrl && (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={urlApi(`${panou.profil.pozaUrl}?marime=mica`)} alt="" className="tv-poza" width={64} height={64} loading="lazy" decoding="async" />
                )}
                <div>
                  <div className="tv-mic">Profil public</div>
                  <h3>{panou.profil.nume}</h3>
                  <p>{panou.profil.materii.map((m) => m.denumire).join(', ')}</p>
                </div>
              </div>
              <div className="tv-butoane">
                <Link className="tv-btn tv-gri" href="/admin/continut">Conținutul meu</Link>
              </div>
            </div>
          </div>
          <SloturileMele panou={panou} onActualizat={setPanou} />
        </div>
      </div>
    </main>
  )
}
