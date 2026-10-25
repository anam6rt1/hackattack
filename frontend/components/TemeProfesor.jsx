'use client'

import { useEffect, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import Paginare from '@/components/Paginare'

function useDate(url, versiune = 0) {
  const [stare, setStare] = useState({ date: null, eroare: '' })
  useEffect(() => {
    let activ = true
    if (url) api(url).then(date => { if (activ) setStare({ date, eroare: '', url }) }).catch(e => { if (activ) setStare({ date: null, eroare: e.message, url }) })
    return () => { activ = false }
  }, [url, versiune])
  return stare.url === url ? stare : { date: null, eroare: '' }
}

function AlegerePagini({ url, titlu, alese, onAlege, versiune = 0, cautare = false }) {
  const [pagina, setPagina] = useState(1)
  const [text, setText] = useState('')
  const { date, eroare } = useDate(`${url}?pagina=${pagina}&q=${encodeURIComponent(text)}`, versiune)
  return <fieldset className="tv-cutie"><legend>{titlu}</legend>
    {cautare && <input className="tv-camp-text" type="search" aria-label={`Caută ${titlu.toLowerCase()}`} value={text} onChange={e => { setText(e.target.value); setPagina(1) }} />}
    <p>{alese.length} selectate</p>
    {eroare && <p role="alert">{eroare}</p>}
    {date?.elemente.map(x => <label className="tv-bifa" key={x.id}><input type="checkbox" checked={alese.includes(x.id)} onChange={e => onAlege(e.target.checked ? [...alese, x.id] : alese.filter(id => id !== x.id))} />{x.titlu ?? x.nume}</label>)}
    {date?.elemente.length === 0 && <p>Niciun rezultat.</p>}
    <Paginare pagina={pagina} totalPagini={Math.ceil((date?.total ?? 0) / 20)} onAlege={setPagina} />
  </fieldset>
}

function Grupe({ baza, versiune, onSalvat }) {
  const [id, setId] = useState(null)
  const [nume, setNume] = useState('')
  const [elevi, setElevi] = useState([])
  const [pagina, setPagina] = useState(1)
  const [ocupat, setOcupat] = useState(false)
  const [mesaj, setMesaj] = useState('')
  const { date, eroare } = useDate(`${baza}/grupe?pagina=${pagina}`, versiune)
  async function editeaza(g) {
    setOcupat(true); setMesaj('')
    try { const r = await api(`${baza}/grupe/${g.id}`); setId(r.id); setNume(r.nume); setElevi(r.eleviIds) } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  async function salveaza(e) {
    e.preventDefault(); setOcupat(true); setMesaj('')
    try {
      await api(`${baza}/grupe${id ? '/' + id : ''}`, { metoda: id ? 'PUT' : 'POST', corp: { nume, eleviIds: elevi } })
      setId(null); setNume(''); setElevi([]); setMesaj('Grupa a fost salvată.'); onSalvat()
    } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  async function sterge(g) {
    setOcupat(true)
    try { await api(`${baza}/grupe/${g.id}`, { metoda: 'DELETE' }); onSalvat(); setMesaj('Grupa a fost ștearsă.') } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <section className="tv-cutie"><h2>Grupele tale</h2>
    {eroare && <p role="alert">{eroare}</p>}
    {date?.elemente.map(g => <div className="tv-rand" key={g.id}><span>{g.nume} · {g.elevi} elevi</span><div className="tv-butoane"><button className="tv-pastila" disabled={ocupat} onClick={() => editeaza(g)}>Editează {g.nume}</button><button className="tv-pastila" disabled={ocupat} onClick={() => sterge(g)}>Șterge {g.nume}</button></div></div>)}
    <Paginare pagina={pagina} totalPagini={Math.ceil((date?.total ?? 0) / 20)} onAlege={setPagina} />
    <form className="tv-form" onSubmit={salveaza}><h3>{id ? 'Editează grupa' : 'Creează o grupă'}</h3>
      <label>Numele grupei<input className="tv-camp-text" required maxLength={100} value={nume} onChange={e => setNume(e.target.value)} /></label>
      <AlegerePagini url={`${baza}/elevi`} titlu="Elevi" alese={elevi} onAlege={setElevi} cautare />
      <button className="tv-btn" disabled={ocupat || elevi.length > 200}>Salvează grupa</button>
      {id && <button type="button" className="tv-pastila" onClick={() => { setId(null); setNume(''); setElevi([]) }}>Renunță la editare</button>}
    </form>{mesaj && <p role="status">{mesaj}</p>}
  </section>
}

function Raport({ baza, tema }) {
  const [versiune, setVersiune] = useState(0)
  const [pagina, setPagina] = useState(1)
  const [stare, setStare] = useState('')
  const [ordine, setOrdine] = useState('nume')
  const { date, eroare } = useDate(`${baza}/${tema.id}/rezultate?pagina=${pagina}&stare=${stare}&ordine=${ordine}`, versiune)
  return <section className="tv-cutie" aria-label="Rezultatele temei"><h2>Rezultate: {tema.titlu}</h2>
    <button className="tv-pastila" onClick={() => setVersiune(v => v + 1)}>Actualizează rezultatele</button>
    <label>Starea temei<select className="tv-camp-text" value={stare} onChange={e => { setStare(e.target.value); setPagina(1) }}><option value="">Toți elevii</option><option value="predat">Predată</option><option value="inceput">Începută</option><option value="nedeschis">Nedeschisă</option></select></label>
    <label>Ordine<select className="tv-camp-text" value={ordine} onChange={e => { setOrdine(e.target.value); setPagina(1) }}><option value="nume">Nume</option><option value="progres">Progres</option></select></label>
    {eroare && <p role="alert">{eroare}</p>}
    {date && <p>{date.total} elevi</p>}
    <ul className="tv-lista">{date?.elemente.map(e => <li className="tv-rand" key={e.elevId}><b>{e.nume}</b><span>{e.terminate}/{e.total} lecții · {e.stare === 'predat' ? new Date(e.predatLa) > new Date(tema.termenLa) ? 'Predată cu întârziere' : 'Predată' : e.stare === 'inceput' ? 'Începută' : 'Nedeschisă'}</span></li>)}</ul>
    <Paginare pagina={pagina} totalPagini={Math.ceil((date?.total ?? 0) / 20)} onAlege={setPagina} />
  </section>
}

function OrganizatieTeme({ org }) {
  const baza = `/organizatii/${org.id}/teme`
  const [versiune, setVersiune] = useState(0)
  const [pagina, setPagina] = useState(1)
  const [titlu, setTitlu] = useState('')
  const [descriere, setDescriere] = useState('')
  const [termen, setTermen] = useState('')
  const [lectii, setLectii] = useState([])
  const [grupe, setGrupe] = useState([])
  const [toti, setToti] = useState(true)
  const [ocupat, setOcupat] = useState(false)
  const [mesaj, setMesaj] = useState('')
  const [raport, setRaport] = useState(null)
  const { date, eroare } = useDate(`${baza}?pagina=${pagina}`, versiune)
  async function creeaza(e) {
    e.preventDefault(); setOcupat(true); setMesaj('')
    try {
      await api(baza, { metoda: 'POST', corp: { titlu, descriere, termenLa: new Date(termen).toISOString(), parcursuriIds: lectii, toataOrganizatia: toti, grupeIds: toti ? [] : grupe } })
      setTitlu(''); setDescriere(''); setLectii([]); setGrupe([]); setTermen(''); setVersiune(v => v + 1); setPagina(1); setMesaj('Tema a fost publicată.')
    } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  async function anuleaza(t) {
    setOcupat(true)
    try { await api(`${baza}/${t.id}`, { metoda: 'DELETE' }); setVersiune(v => v + 1); setRaport(null) } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <>
    <section className="tv-cutie"><h2>Publică o temă</h2><p>{org.admin ? 'Poți vedea și administra toate temele organizației.' : 'Gestionezi temele și grupele tale.'} Elevii pot preda și după termen.</p>
      <form className="tv-form" onSubmit={creeaza}>
        <label>Titlul temei<input className="tv-camp-text" required maxLength={200} value={titlu} onChange={e => setTitlu(e.target.value)} /></label>
        <label>Instrucțiuni<textarea className="tv-camp-text" maxLength={2000} value={descriere} onChange={e => setDescriere(e.target.value)} /></label>
        <label>Termen de predare<input className="tv-camp-text" type="datetime-local" required value={termen} onChange={e => setTermen(e.target.value)} /></label>
        <AlegerePagini url={`${baza}/lectii`} titlu="Lecții" alese={lectii} onAlege={setLectii} cautare />
        <label>Destinatari<select className="tv-camp-text" value={toti ? 'toti' : 'grupe'} onChange={e => setToti(e.target.value === 'toti')}><option value="toti">Toți elevii organizației</option><option value="grupe">Grupe selectate</option></select></label>
        {!toti && <AlegerePagini url={`${baza}/grupe`} titlu="Grupe" alese={grupe} onAlege={setGrupe} versiune={versiune} />}
        <button className="tv-btn" disabled={ocupat || lectii.length < 1 || lectii.length > 10 || (!toti && (grupe.length < 1 || grupe.length > 10))}>Publică tema</button>
      </form>{mesaj && <p role="status">{mesaj}</p>}
    </section>
    <section className="tv-cutie"><h2>Temele organizației</h2>{eroare && <p role="alert">{eroare}</p>}
      {date?.elemente.map(t => <article className="tv-cutie" key={t.id}><h3>{t.titlu}</h3><p>{t.profesor} · {dataOra(t.termenLa)}{t.anulata ? ' · Anulată' : ''}</p><div className="tv-butoane"><button className="tv-pastila" onClick={() => setRaport(t)}>Rezultate {t.titlu}</button>{!t.anulata && <button className="tv-pastila" disabled={ocupat} onClick={() => anuleaza(t)}>Anulează tema {t.titlu}</button>}</div></article>)}
      <Paginare pagina={pagina} totalPagini={Math.ceil((date?.total ?? 0) / 20)} onAlege={setPagina} />
    </section>
    {raport && <Raport key={raport.id} baza={baza} tema={raport} />}
    <Grupe baza={baza} versiune={versiune} onSalvat={() => setVersiune(v => v + 1)} />
  </>
}

export default function TemeProfesor() {
  const { date, eroare } = useDate('/teme/organizatii')
  const [ales, setAles] = useState(null)
  const org = date?.find(o => o.id === ales) ?? date?.[0]
  return <>
    {eroare && <p role="alert">{eroare}</p>}
    {!date && !eroare && <p>Se încarcă organizațiile…</p>}
    {date?.length === 0 && <p>Ai nevoie de o organizație aprobată pentru a publica teme.</p>}
    {org && <><label>Organizația<select className="tv-camp-text" value={org.id} onChange={e => setAles(Number(e.target.value))}>{date.map(o => <option key={o.id} value={o.id}>{o.nume}</option>)}</select></label><OrganizatieTeme key={org.id} org={org} /></>}
  </>
}
