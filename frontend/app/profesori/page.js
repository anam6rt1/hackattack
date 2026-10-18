'use client'
// /profesori
//  • elev: lista profesorilor verificați, cu tariful fix sau „Pro bono”, ședințele viitoare și locurile libere;
//    rezervarea se face direct (plata e simulată deocamdată).
//  • profesor: ședințele proprii (programare, înscrieri, anulare).

import { useCallback, useEffect, useState } from 'react'
import { api, dataOra, urlApi } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { decorSVG } from '../joc/arta'
import Incarcare from '@/components/Incarcare'
import SloturileMele from '@/components/profesori/SloturileMele'
import AnuleazaRezervare from '@/components/profesori/AnuleazaRezervare'

const DECOR = decorSVG(41)

function Rezervare({ slot, profesor, pret, onInchide, onRezervat }) {
  const [stare, setStare] = useState('confirmare') // confirmare | trimite | gata
  const [eroare, setEroare] = useState('')

  async function rezerva() {
    setStare('trimite')
    setEroare('')
    try {
      await api('/rezervari', { metoda: 'POST', corp: { slotId: slot.id } })
      setStare('gata')
      onRezervat()
    } catch (e) {
      setEroare(e.message)
      setStare('confirmare')
    }
  }

  return (
    <div className="tv-fundal" onClick={onInchide}>
      <div className="tv-fereastra" role="dialog" aria-modal="true" style={{ maxWidth: 440, paddingTop: 28 }} onClick={(e) => e.stopPropagation()}>
        {stare === 'gata' ? (
          <div className="tv-form" style={{ textAlign: 'center' }}>
            <h3 style={{ color: 'var(--maro)', fontSize: 26 }}>Ai rezervat locul!</h3>
            <p className="tv-indiciu">{slot.titlu} cu {profesor.nume}, {dataOra(slot.inceputLa)}. Ședința apare și pe pagina ta de start.</p>
            <button className="tv-btn" onClick={onInchide}>Gata</button>
          </div>
        ) : (
          <div className="tv-form">
            <h3 style={{ color: 'var(--maro)', fontSize: 24 }}>{slot.titlu}</h3>
            <ul className="tv-lista">
              <li className="tv-rand"><div><b>{profesor.nume}</b><small>{slot.materie}</small></div></li>
              <li className="tv-rand"><div><b>{dataOra(slot.inceputLa)}</b><small>{slot.locuriLibere} {slot.locuriLibere === 1 ? 'loc liber' : 'locuri libere'}</small></div></li>
              <li className="tv-rand">
                <div>
                  <b>{slot.proBono ? 'Gratuit (pro bono)' : `${pret.suma} ${pret.moneda}`}</b>
                  <small>{slot.proBono ? 'Profesorul oferă această ședință fără plată.' : `Tariful fix al platformei pentru ${pret.durataMinute} de minute.`}</small>
                </div>
              </li>
            </ul>
            {slot.descriere && <p className="tv-indiciu">{slot.descriere}</p>}
            {!slot.proBono && <p className="tv-indiciu">Plata este simulată în această versiune: nu se percepe niciun ban.</p>}
            {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
            <div className="tv-grid-2">
              <button className="tv-btn tv-gri" onClick={onInchide}>Renunț</button>
              <button className="tv-btn" onClick={rezerva} disabled={stare === 'trimite'}>{stare === 'trimite' ? 'Se rezervă...' : 'Rezervă locul'}</button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function Director() {
  const [profesori, setProfesori] = useState(null)
  const [materii, setMaterii] = useState([])
  const [pret, setPret] = useState(null)
  const [filtru, setFiltru] = useState({ materieId: '', gratuit: false, saptamana: false })
  const [ales, setAles] = useState(null) // { slot, profesor }
  const [rezervarileMele, setRezervarileMele] = useState({}) // slotId → id-ul rezervării mele confirmate
  const [eroare, setEroare] = useState('')

  const incarca = useCallback(() => {
    const q = new URLSearchParams()
    if (filtru.materieId) q.set('materieId', filtru.materieId)
    if (filtru.gratuit) q.set('gratuit', 'true')
    if (filtru.saptamana) q.set('zile', '7')
    api('/profesori?' + q).then((r) => setProfesori(r.elemente)).catch((e) => setEroare(e.message))
    api('/rezervari')
      .then((r) => setRezervarileMele(Object.fromEntries(r.elemente.filter((x) => x.status === 'confirmata').map((x) => [x.slot.id, x.id]))))
      .catch(() => {})
  }, [filtru])

  useEffect(() => {
    api('/materii').then((r) => setMaterii(r.elemente)).catch(() => {})
    api('/tarif').then(setPret).catch(() => {})
  }, [])
  useEffect(() => incarca(), [incarca])

  return (
    <>
      <section className="tv-sectiune">
        <h2>Profesori verificați</h2>
        <p>
          O ședință costă {pret ? `${pret.suma} ${pret.moneda}` : 'un tarif fix'}, oricare ar fi profesorul. Unii profesori oferă și ședințe pro bono, gratuite.
        </p>
      </section>

      <div className="tv-filtre">
        <label className="tv-eticheta-camp">
          <span>Materie</span>
          <select className="tv-camp-text" value={filtru.materieId} onChange={(e) => setFiltru({ ...filtru, materieId: e.target.value })}>
            <option value="">Toate</option>
            {materii.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
          </select>
        </label>
        <label className="tv-optiune"><input type="checkbox" checked={filtru.gratuit} onChange={(e) => setFiltru({ ...filtru, gratuit: e.target.checked })} /> Doar pro bono</label>
        <label className="tv-optiune"><input type="checkbox" checked={filtru.saptamana} onChange={(e) => setFiltru({ ...filtru, saptamana: e.target.checked })} /> Locuri în următoarele 7 zile</label>
      </div>

      {eroare && <p className="tv-eroare">{eroare}</p>}
      {!profesori ? (
        <p className="tv-asteapta">Se încarcă...</p>
      ) : profesori.length === 0 ? (
        <p className="tv-gol">Niciun profesor nu se potrivește filtrelor.</p>
      ) : (
        <div className="tv-profesori">
          {profesori.map((p) => (
            <article key={p.id} className="tv-cutie">
              <div className="tv-nivel">
                {p.pozaUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={urlApi(p.pozaUrl)} alt="" className="tv-poza" width={64} height={64} />
                ) : (
                  <span className="tv-nivel-insigna" style={{ width: 64, height: 64, fontSize: 26 }}>{p.nume.charAt(0)}</span>
                )}
                <div style={{ minWidth: 0 }}>
                  <h3>{p.nume}</h3>
                  <p>{p.materii.map((m) => m.denumire).join(', ')}</p>
                </div>
                {p.oferaProBono && <span className="tv-pastila tv-pastila-verde">Pro bono</span>}
              </div>
              {p.descriere && <p>{p.descriere}</p>}
              <div className="tv-mic">Ședințe viitoare</div>
              {p.sloturi.length ? (
                <ul className="tv-lista">
                  {p.sloturi.slice(0, 5).map((s) => (
                    <li key={s.id} className="tv-rand">
                      <div>
                        <b>{s.titlu}</b>
                        <small>
                          {dataOra(s.inceputLa)} · {s.locuriLibere > 0 ? `${s.locuriLibere} din ${s.capacitate} locuri libere` : 'complet'} · {s.proBono ? 'gratuit' : pret ? `${pret.suma} ${pret.moneda}` : 'plătit'}
                        </small>
                      </div>
                      {s.rezervatDeMine ? (
                        <span className="tv-butoane">
                          <span className="tv-pastila tv-pastila-verde">Rezervat</span>
                          {s.linkIntalnire && <a className="tv-pastila tv-pastila-aur" href={s.linkIntalnire} target="_blank" rel="noreferrer">Intră în ședință</a>}
                          {rezervarileMele[s.id] && <AnuleazaRezervare rezervareId={rezervarileMele[s.id]} onAnulat={incarca} />}
                        </span>
                      ) : (
                        <button className="tv-pastila tv-pastila-aur" disabled={s.locuriLibere === 0} onClick={() => setAles({ slot: s, profesor: p })}>
                          {s.locuriLibere === 0 ? 'Complet' : 'Rezervă'}
                        </button>
                      )}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="tv-indiciu">Nicio ședință programată momentan.</p>
              )}
            </article>
          ))}
        </div>
      )}

      {ales && pret !== undefined && (
        <Rezervare slot={ales.slot} profesor={ales.profesor} pret={pret ?? { suma: 0, moneda: 'RON', durataMinute: 60 }} onInchide={() => setAles(null)} onRezervat={incarca} />
      )}
    </>
  )
}

function PanouProfesor() {
  const [panou, setPanou] = useState(null)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    api('/profesori/me').then(setPanou).catch((e) => setEroare(e.message))
  }, [])
  if (eroare) return <p className="tv-eroare">{eroare}</p>
  if (!panou) return <p className="tv-asteapta">Se încarcă...</p>
  return (
    <>
      <section className="tv-sectiune">
        <h2>Ședințele mele</h2>
        <p>
          Elevii plătesc tariful fix al platformei; tu primești onorariul negociat de {panou.onorariuNegociat} {panou.moneda} pe elev. Ședințele pro bono sunt gratuite pentru elevi.
        </p>
      </section>
      <SloturileMele panou={panou} onActualizat={setPanou} />
    </>
  )
}

export default function PaginaProfesori() {
  const utilizator = usePagina('elev', 'profesor')
  if (!utilizator) return <Incarcare />
  const profesor = utilizator.rol === 'profesor'

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">{profesor ? 'Pentru profesori' : 'Profesori'}</span>
          <h1>{profesor ? 'Programează ședințe pentru elevii tăi.' : 'Ajutor de la un profesor, când ai nevoie.'}</h1>
          <p>
            {profesor
              ? 'Alege ora și numărul de locuri. Elevii văd ședința imediat în lista profesorilor.'
              : 'Toți profesorii sunt verificați de echipa noastră. Rezervi un loc la o ședință și primești detaliile pe pagina de start.'}
          </p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>
        <div className="tv-coala">{profesor ? <PanouProfesor /> : <Director />}</div>
      </div>
    </main>
  )
}
