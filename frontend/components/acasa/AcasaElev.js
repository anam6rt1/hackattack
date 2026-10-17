'use client'
// Panoul elevului: nivelul și XP-ul, unde a rămas, progresul pe materii și ședințele rezervate.

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api, dataOra } from '@/lib/api'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'

const DECOR = decorSVG(29)

export default function AcasaElev({ utilizator }) {
  const [progres, setProgres] = useState(null)
  const [rezervari, setRezervari] = useState([])
  const [eroare, setEroare] = useState('')

  useEffect(() => {
    api('/progres').then(setProgres).catch((e) => setEroare(e.message))
    api('/rezervari').then((r) => setRezervari(r.elemente.filter((x) => x.status === 'confirmata' && new Date(x.slot.inceputLa) > new Date()))).catch(() => {})
  }, [])

  if (eroare) return <Incarcare text={eroare} />
  if (!progres) return <Incarcare />

  const { nivel, materii, ultimulCurs, cursuriCompletate } = progres
  const pana = nivel.xpUrmatorNivel ? nivel.xpUrmatorNivel - nivel.xpNivel : 1
  const procent = nivel.xpUrmatorNivel ? Math.round(((nivel.xpTotal - nivel.xpNivel) / pana) * 100) : 100

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Acasă</span>
          <h1>Salut, {utilizator.nume.split(' ')[0]}!</h1>
          <p>{ultimulCurs ? `Data trecută ai lucrat la „${ultimulCurs.titlu}”.` : 'Începe cu prima insulă: o lecție scurtă și o întrebare.'}</p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>

        <div className="tv-coala">
          <div className="tv-panou">
            <div className="tv-cutie">
              <div className="tv-nivel">
                <span className="tv-nivel-insigna" aria-label={`Nivelul ${nivel.nivel}`}>{nivel.nivel}</span>
                <div>
                  <div className="tv-mic">Nivelul tău</div>
                  <h3>{nivel.titlu}</h3>
                  <p>{nivel.xpTotal} XP</p>
                </div>
              </div>
              <div className="tv-bara" role="progressbar" aria-valuenow={procent} aria-valuemin={0} aria-valuemax={100}>
                <i style={{ width: `${procent}%` }} />
              </div>
              <p className="tv-indiciu">
                {nivel.xpUrmatorNivel
                  ? `Mai ai ${nivel.xpUrmatorNivel - nivel.xpTotal} XP până la „${nivel.titluUrmator}”. Fiecare lecție terminată îți aduce 100 XP.`
                  : 'Ai atins cel mai înalt nivel. Felicitări!'}
              </p>
            </div>

            <div className="tv-cutie tv-panou-lat">
              <h3>{ultimulCurs && !ultimulCurs.completat ? 'Continuă de unde ai rămas' : 'Următorul pas'}</h3>
              {ultimulCurs ? (
                <Link className="tv-rand" href={`/materiale/${ultimulCurs.materieId}`}>
                  <div>
                    <b>{ultimulCurs.titlu}</b>
                    <small>{ultimulCurs.materie}{ultimulCurs.capitol ? ` · ${ultimulCurs.capitol}` : ''}</small>
                  </div>
                  <span className={`tv-pastila ${ultimulCurs.completat ? 'tv-pastila-verde' : 'tv-pastila-aur'}`}>{ultimulCurs.completat ? 'Terminat' : 'În lucru'}</span>
                </Link>
              ) : (
                <p>Deschide Materiale și alege prima lecție de pe hartă.</p>
              )}
              <div className="tv-butoane">
                <Link className="tv-btn" href="/materiale">Mergi la materiale</Link>
                <Link className="tv-btn tv-aur" href="/joc">Joacă Triviador</Link>
                {cursuriCompletate.length > 0 && <Link className="tv-btn tv-gri" href="/antrenament">Recapitulează</Link>}
              </div>
            </div>
          </div>

          <div className="tv-panou">
            <div className="tv-cutie tv-panou-lat">
              <h3>Progresul pe materii</h3>
              <ul className="tv-lista">
                {materii.map((m) => (
                  <li key={m.materieId}>
                    <Link className="tv-rand" href={`/materiale/${m.materieId}`}>
                      <div>
                        <b>{m.denumire}</b>
                        <small>{m.cursuriCompletate} din {m.totalCursuri} lecții · {m.puncte} puncte</small>
                        <div className="tv-bara tv-bara-verde" style={{ marginTop: 6 }}>
                          <i style={{ width: `${m.totalCursuri ? (m.cursuriCompletate / m.totalCursuri) * 100 : 0}%` }} />
                        </div>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            <div className="tv-cutie">
              <h3>Ședințele tale</h3>
              {rezervari.length ? (
                <ul className="tv-lista">
                  {rezervari.slice(0, 4).map((r) => (
                    <li key={r.id} className="tv-rand">
                      <div>
                        <b>{r.slot.titlu}</b>
                        <small>{dataOra(r.slot.inceputLa)} · {r.profesor}</small>
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p>Nu ai ședințe rezervate.</p>
              )}
              <Link className="tv-btn tv-albastru" href="/profesori" style={{ textAlign: 'center', textDecoration: 'none' }}>Găsește un profesor</Link>
            </div>
          </div>
        </div>
      </div>
    </main>
  )
}
