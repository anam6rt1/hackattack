'use client'
// Panoul elevului: nivelul și XP-ul, unde a rămas, progresul pe materii și ședințele rezervate.

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api, dataOra } from '@/lib/api'
import { textLoc } from '@/lib/sedinte'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'
import AnuleazaRezervare from '@/components/profesori/AnuleazaRezervare'
import Paginare from '@/components/Paginare'
import { PROGRES_MATERII_PE_PAGINA_ACASA_ELEV, cuPagina } from '@/lib/pagini'

const DECOR = decorSVG(29)

export default function AcasaElev({ utilizator }) {
  const [progres, setProgres] = useState(null)
  // Progresul pe materii: doar materiile în care a deschis o lecție, câte o pagină.
  const [materii, setMaterii] = useState(null)
  const [totalMaterii, setTotalMaterii] = useState(0)
  const [paginaMaterii, setPaginaMaterii] = useState(1)
  const [rezervari, setRezervari] = useState([])
  const [eroare, setEroare] = useState('')

  const incarcaRezervari = () =>
    api('/rezervari').then((r) => setRezervari(r.elemente.filter((x) => x.status === 'confirmata' && new Date(x.slot.inceputLa) > new Date()))).catch(() => {})

  useEffect(() => {
    api('/progres').then(setProgres).catch((e) => setEroare(e.message))
  }, [])
  useEffect(() => {
    api(cuPagina('/progres/materii', paginaMaterii, PROGRES_MATERII_PE_PAGINA_ACASA_ELEV))
      .then((r) => { setMaterii(r.elemente); setTotalMaterii(r.total) }).catch(() => setMaterii([]))
  }, [paginaMaterii])
  useEffect(() => {
    api('/rezervari').then((r) => setRezervari(r.elemente.filter((x) => x.status === 'confirmata' && new Date(x.slot.inceputLa) > new Date()))).catch(() => {})
  }, [])

  if (eroare) return <Incarcare text={eroare} />
  if (!progres) return <Incarcare />

  const { nivel, ultimaLectie, lectiiCompletate } = progres
  const pana = nivel.xpUrmatorNivel ? nivel.xpUrmatorNivel - nivel.xpNivel : 1
  const procent = nivel.xpUrmatorNivel ? Math.round(((nivel.xpTotal - nivel.xpNivel) / pana) * 100) : 100

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Acasă</span>
          <h1>Salut, {utilizator.nume.split(' ')[0]}!</h1>
          <p>{ultimaLectie ? `Data trecută ai lucrat la „${ultimaLectie.capitol ?? ultimaLectie.titlu}”.` : 'Alege o materie și o lecție: fiecare capitol e o insulă cu notițe și video, iar lecția se termină cu un test.'}</p>
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
              <h3>{ultimaLectie && !ultimaLectie.completata ? 'Continuă de unde ai rămas' : 'Următorul pas'}</h3>
              {ultimaLectie ? (
                <Link className="tv-rand" href={`/parcurs/${ultimaLectie.parcursId}`}>
                  <div>
                    <b>{ultimaLectie.titlu}</b>
                    <small>{ultimaLectie.materie}{ultimaLectie.capitol ? ` · ultimul capitol: ${ultimaLectie.capitol}` : ''}</small>
                  </div>
                  <span className={`tv-pastila ${ultimaLectie.completata ? 'tv-pastila-verde' : 'tv-pastila-aur'}`}>{ultimaLectie.completata ? 'Terminată' : 'În lucru'}</span>
                </Link>
              ) : (
                <p>Deschide Materiale, alege o lecție și începe cu orice capitol de pe hartă.</p>
              )}
              <div className="tv-butoane">
                <Link className="tv-btn" href="/materiale">Mergi la materiale</Link>
                <Link className="tv-btn tv-aur" href="/joc">Joacă Triviador</Link>
                {lectiiCompletate.length > 0 && <Link className="tv-btn tv-gri" href="/antrenament">Recapitulează</Link>}
              </div>
            </div>
          </div>

          <div className="tv-panou">
            <div className="tv-cutie tv-panou-lat">
              <h3>Progresul pe materii</h3>
              {materii && materii.length === 0 && (
                <p className="tv-indiciu">Deschide o lecție din <Link href="/materiale">Materiale</Link> și materia ei apare aici, cu progresul tău.</p>
              )}
              <ul className="tv-lista">
                {(materii ?? []).map((m) => (
                  <li key={m.materieId}>
                    <Link className="tv-rand" href={`/materiale/${m.materieId}`}>
                      <div>
                        <b>{m.denumire}</b>
                        <small>{m.lectiiCompletate} din {m.totalLectii} lecții · {m.puncte} puncte</small>
                        <div className="tv-bara tv-bara-verde" style={{ marginTop: 6 }}>
                          <i style={{ width: `${m.totalLectii ? (m.lectiiCompletate / m.totalLectii) * 100 : 0}%` }} />
                        </div>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
              <Paginare compact pagina={paginaMaterii} totalPagini={Math.ceil(totalMaterii / PROGRES_MATERII_PE_PAGINA_ACASA_ELEV)} onAlege={setPaginaMaterii} />
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
                        {r.participare === 'la_sala' ? (
                          <small>La sală: {textLoc(r.slot.loc)}{r.slot.notaLoc ? ` · ${r.slot.notaLoc}` : ''}</small>
                        ) : r.slot.linkIntalnire ? (
                          <a className="tv-link-sedinta" href={r.slot.linkIntalnire} target="_blank" rel="noreferrer">Intră în ședință →</a>
                        ) : (
                          <small>Profesorul va adăuga linkul ședinței.</small>
                        )}
                      </div>
                      <AnuleazaRezervare rezervareId={r.id} onAnulat={incarcaRezervari} />
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
