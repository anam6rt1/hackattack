'use client'
// /profesori
//  • elev: profesorii verificați care au ședințe viitoare, cu tariful fix sau „Pro bono” și locurile libere; căutare după nume
//    și materie (fără diacritice); filtre: din organizația mea / aproape de mine (doar localitatea din Setări), online / față
//    în față (hibridele apar la ambele); rezervarea se face direct (plata e simulată deocamdată); la o ședință hibridă elevul
//    alege cum participă. Linkul ședințelor rezervate e pe pagina de start. Fiecare profesor arată doar următoarea ședință;
//    „și alte X sesiuni” le aduce pe toate (cu același filtru online / față în față).
//  • profesor: ședințele proprii (programare, înscrieri, anulare).

import { useCallback, useEffect, useState } from 'react'
import Link from 'next/link'
import { api, dataOra, urlApi } from '@/lib/api'
import { PARTICIPARE, TIPURI_SEDINTA, textAlteSesiuni, textLoc } from '@/lib/sedinte'
import { esteProfesor, usePagina } from '@/lib/sesiune'
import Paginare from '@/components/Paginare'
import { PROFESORI_PE_PAGINA_ELEV } from '@/lib/pagini'
import { decorSVG } from '../joc/arta'
import Incarcare from '@/components/Incarcare'
import SloturileMele from '@/components/profesori/SloturileMele'
import AnuleazaRezervare from '@/components/profesori/AnuleazaRezervare'

const DECOR = decorSVG(41)

function Rezervare({ slot, profesor, pret, onInchide, onRezervat }) {
  const [stare, setStare] = useState('confirmare') // confirmare | trimite | gata
  const [eroare, setEroare] = useState('')
  const [participare, setParticipare] = useState(null) // doar la ședințele hibride

  async function rezerva() {
    if (slot.tip === 'hibrid' && !participare) return setEroare('Alege dacă vii la sală sau participi online.')
    setStare('trimite')
    setEroare('')
    try {
      await api('/rezervari', { metoda: 'POST', corp: { slotId: slot.id, participare: slot.tip === 'hibrid' ? participare : null } })
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
                  <b>{TIPURI_SEDINTA[slot.tip]}</b>
                  <small>
                    {slot.tip === 'online' ? 'Primești linkul după rezervare.' : `${textLoc(slot.loc)}. Detaliile exacte ale locului le vezi după rezervare.`}
                  </small>
                </div>
              </li>
              <li className="tv-rand">
                <div>
                  <b>{slot.proBono ? 'Gratuit (pro bono)' : `${pret.suma} ${pret.moneda}`}</b>
                  <small>{slot.proBono ? 'Profesorul oferă această ședință fără plată.' : `Tariful fix al platformei pentru ${pret.durataMinute} de minute.`}</small>
                </div>
              </li>
            </ul>
            {slot.descriere && <p className="tv-indiciu">{slot.descriere}</p>}
            {slot.tip === 'hibrid' && (
              <fieldset className="tv-eticheta-camp">
                <span>Cum participi?</span>
                <div className="tv-optiuni">
                  {Object.entries(PARTICIPARE).map(([v, t]) => (
                    <label key={v} className="tv-optiune">
                      <input type="radio" name="participare" value={v} checked={participare === v} onChange={() => setParticipare(v)} /> {t === 'online' ? 'Online' : 'La sală'}
                    </label>
                  ))}
                </div>
              </fieldset>
            )}
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

const FILTRE = [['toate', 'Toți profesorii'], ['organizatia-mea', 'Din organizația mea'], ['local', 'Aproape de mine']]
const TIPURI_FILTRU = [['', 'Orice ședință'], ['online', 'Online'], ['fata_in_fata', 'Față în față']]

function Director({ utilizator }) {
  const [profesori, setProfesori] = useState(null)
  const [total, setTotal] = useState(0) // toți cei care se potrivesc; serverul trimite câte o pagină
  const [materii, setMaterii] = useState([])
  const [pret, setPret] = useState(null)
  const [filtru, setFiltru] = useState({ materieId: '', gratuit: false, saptamana: false, unde: 'toate', tip: '', q: '', pagina: 1 })
  // Orice filtru nou pornește de la prima pagină.
  const schimba = (modificare) => setFiltru((f) => ({ ...f, ...modificare, pagina: 1 }))
  const [text, setText] = useState('') // ce scrie elevul; ajunge în filtru după o scurtă pauză
  const areOrganizatie = utilizator.organizatii?.some((o) => o.stare === 'aprobat')
  const [ales, setAles] = useState(null) // { slot, profesor }
  const [rezervarileMele, setRezervarileMele] = useState({}) // slotId → id-ul rezervării mele confirmate
  const [eroare, setEroare] = useState('')
  // id profesor → toate ședințele lui viitoare (după „și alte X sesiuni”), pentru filtrul (și pagina) de atunci: la alt filtru,
  // cardurile pornesc din nou restrânse.
  const [extinse, setExtinse] = useState({ pentru: null, sloturi: {} })
  const deschise = extinse.pentru === filtru ? extinse.sloturi : {}

  const incarca = useCallback(() => {
    const q = new URLSearchParams()
    if (filtru.materieId) q.set('materieId', filtru.materieId)
    if (filtru.gratuit) q.set('gratuit', 'true')
    if (filtru.saptamana) q.set('zile', '7')
    if (filtru.unde !== 'toate') q.set('filtru', filtru.unde)
    if (filtru.tip) q.set('tip', filtru.tip)
    if (filtru.q) q.set('q', filtru.q)
    if (filtru.pagina > 1) q.set('pagina', String(filtru.pagina))
    q.set('marime', String(PROFESORI_PE_PAGINA_ELEV))
    api('/profesori?' + q).then((r) => { setProfesori(r.elemente); setTotal(r.total) }).catch((e) => setEroare(e.message))
    api('/rezervari')
      .then((r) => setRezervarileMele(Object.fromEntries(r.elemente.filter((x) => x.status === 'confirmata').map((x) => [x.slot.id, x.id]))))
      .catch(() => {})
  }, [filtru])

  function extinde(id) {
    api(`/profesori/${id}` + (filtru.tip ? `?tip=${filtru.tip}` : ''))
      .then((p) => setExtinse((e) => ({ pentru: filtru, sloturi: { ...(e.pentru === filtru ? e.sloturi : {}), [id]: p.sloturi } })))
      .catch((e) => setEroare(e.message))
  }
  function ascunde(id) {
    const rest = { ...deschise }
    delete rest[id]
    setExtinse({ pentru: filtru, sloturi: rest })
  }
  // După o rezervare sau o anulare: lista și ședințele deschise, ca locurile libere să fie la zi.
  function reincarca() {
    incarca()
    Object.keys(deschise).forEach(extinde)
  }

  function alegePagina(p) {
    setFiltru((f) => ({ ...f, pagina: p }))
    window.scrollTo?.({ top: 0, behavior: 'smooth' })
  }

  useEffect(() => {
    api('/materii').then((r) => setMaterii(r.elemente)).catch(() => {})
    api('/tarif').then(setPret).catch(() => {})
  }, [])
  useEffect(() => incarca(), [incarca])
  useEffect(() => {
    const t = setTimeout(() => setFiltru((f) => (f.q === text.trim() ? f : { ...f, q: text.trim(), pagina: 1 })), 300)
    return () => clearTimeout(t)
  }, [text])

  // Doar profesorii cu ședințe viitoare apar în listă, deci mesajul spune asta, nu „niciun profesor”.
  const alteFiltre = filtru.materieId || filtru.tip || filtru.gratuit || filtru.saptamana
  const unde = filtru.unde === 'local' ? ' din localitatea ta' : filtru.unde === 'organizatia-mea' ? ' din organizația ta' : ''
  const gol = filtru.q
    ? `Niciun profesor${unde} cu ședințe programate nu se potrivește căutării „${filtru.q}”${alteFiltre ? ' și filtrelor alese' : ''}.`
    : `Niciun profesor${unde} nu are ședințe programate${alteFiltre ? ' care să se potrivească filtrelor' : ''} momentan.`

  return (
    <>
      <section className="tv-sectiune">
        <h2>Profesori verificați</h2>
        <p>
          O ședință costă {pret ? `${pret.suma} ${pret.moneda}` : 'un tarif fix'}, oricare ar fi profesorul. Unii profesori oferă și ședințe pro bono, gratuite.
        </p>
      </section>

      {/* Toate filtrele într-o singură bară: unde, tipul ședinței, materia și opțiunile. */}
      <div className="tv-bara-filtre">
        <input type="search" className="tv-camp-text" aria-label="Caută un profesor" placeholder="Caută după nume sau materie" maxLength={100}
          value={text} onChange={(e) => setText(e.target.value)} />
        <div className="tv-filtre" role="group" aria-label="Unde sunt profesorii">
          {FILTRE.map(([v, t]) => (
            <button key={v} type="button" className="tv-filtru" aria-pressed={filtru.unde === v}
              disabled={(v === 'local' && !utilizator.localitateId) || (v === 'organizatia-mea' && !areOrganizatie)}
              onClick={() => schimba({ unde: v })}>
              {t}
            </button>
          ))}
        </div>
        <div className="tv-filtre" role="group" aria-label="Tipul ședinței">
          {TIPURI_FILTRU.map(([v, t]) => (
            <button key={v} type="button" className="tv-filtru" aria-pressed={filtru.tip === v} onClick={() => schimba({ tip: v })}>
              {t}
            </button>
          ))}
        </div>
        <div className="tv-filtre">
          <select className="tv-camp-text" aria-label="Materie" value={filtru.materieId} onChange={(e) => schimba({ materieId: e.target.value })}>
            <option value="">Toate materiile</option>
            {materii.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
          </select>
          <label className="tv-optiune"><input type="checkbox" checked={filtru.gratuit} onChange={(e) => schimba({ gratuit: e.target.checked })} /> Doar pro bono</label>
          <label className="tv-optiune"><input type="checkbox" checked={filtru.saptamana} onChange={(e) => schimba({ saptamana: e.target.checked })} /> Locuri în următoarele 7 zile</label>
        </div>
        {!utilizator.localitateId && <Link href="/setari" className="tv-indiciu">Alege-ți localitatea în Setări ca să vezi profesorii din localitatea ta.</Link>}
      </div>

      {eroare && <p className="tv-eroare">{eroare}</p>}
      {!profesori ? (
        <p className="tv-asteapta">Se încarcă...</p>
      ) : profesori.length === 0 ? (
        <p className="tv-gol">{gol}</p>
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
                  {p.loc && <p className="tv-indiciu">{textLoc(p.loc)}{p.inLocalitateaTa ? ' · în localitatea ta' : ''}</p>}
                </div>
                {p.oferaProBono && <span className="tv-pastila tv-pastila-verde">Pro bono</span>}
              </div>
              {p.descriere && <p>{p.descriere}</p>}
              <div className="tv-mic">Ședințe viitoare</div>
              <ul className="tv-lista">
                {(deschise[p.id] ?? p.sloturi).map((s) => (
                  <li key={s.id} className="tv-rand">
                    <div>
                      <b>{s.titlu}</b>
                      <small>
                        {dataOra(s.inceputLa)} · {TIPURI_SEDINTA[s.tip]}{s.loc ? ` (${[textLoc({ ...s.loc, judet: null }), s.notaLoc].filter(Boolean).join(' · ')})` : ''} · {s.locuriLibere > 0 ? `${s.locuriLibere} din ${s.capacitate} locuri libere` : 'complet'} · {s.proBono ? 'gratuit' : pret ? `${pret.suma} ${pret.moneda}` : 'plătit'}
                      </small>
                    </div>
                    {s.rezervatDeMine ? (
                      <span className="tv-butoane">
                        <span className="tv-pastila tv-pastila-verde">Rezervat</span>
                        {rezervarileMele[s.id] && <AnuleazaRezervare rezervareId={rezervarileMele[s.id]} onAnulat={reincarca} />}
                      </span>
                    ) : (
                      <button className="tv-pastila tv-pastila-aur" disabled={s.locuriLibere === 0} onClick={() => setAles({ slot: s, profesor: p })}>
                        {s.locuriLibere === 0 ? 'Complet' : 'Rezervă'}
                      </button>
                    )}
                  </li>
                ))}
              </ul>
              {p.alteSesiuni > 0 && (
                <button type="button" className="tv-alte-sesiuni" aria-expanded={Boolean(deschise[p.id])}
                  onClick={() => (deschise[p.id] ? ascunde(p.id) : extinde(p.id))}>
                  {deschise[p.id] ? 'Ascunde celelalte sesiuni' : textAlteSesiuni(p.alteSesiuni)}
                </button>
              )}
            </article>
          ))}
        </div>
      )}
      {profesori && profesori.length > 0 && (
        <>
          <Paginare pagina={filtru.pagina} totalPagini={Math.ceil(total / PROFESORI_PE_PAGINA_ELEV)} onAlege={alegePagina} />
          <p className="tv-indiciu" style={{ textAlign: 'center' }}>{total} {total === 1 ? 'profesor' : 'profesori'}</p>
        </>
      )}

      {ales && pret !== undefined && (
        <Rezervare slot={ales.slot} profesor={ales.profesor} pret={pret ?? { suma: 0, moneda: 'RON', durataMinute: 60 }} onInchide={() => setAles(null)} onRezervat={reincarca} />
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
  const profesor = esteProfesor(utilizator)

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
        <div className="tv-coala">{profesor ? <PanouProfesor /> : <Director utilizator={utilizator} />}</div>
      </div>
    </main>
  )
}
