'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api, dataOra } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import Paginare from '@/components/Paginare'
import { rute } from '@/lib/rute'

function Intrebare({ q, ocupat, final, onRaspunde }) {
  const [valoare, setValoare] = useState(q.valoareAleasa ?? '')
  return <article className="tv-cutie"><h3>{q.text}</h3><p>{q.lectie}</p>
    {q.tip === 'grila' ? <div className="tv-form">{q.variante.map(v => <button className="tv-pastila" key={v.id} disabled={ocupat || final} aria-pressed={q.variantaAleasaId === v.id} onClick={() => onRaspunde({ intrebareId: q.id, variantaId: v.id })}>{q.variantaAleasaId === v.id ? '● ' : ''}{v.text}{final && q.variantaCorectaId === v.id ? ' ✓' : ''}</button>)}</div>
      : <form className="tv-form" onSubmit={e => { e.preventDefault(); onRaspunde({ intrebareId: q.id, valoare: Number(valoare) }) }}><label>Răspuns numeric<input className="tv-camp-text" type="number" step="1" min={-2147483648} max={2147483647} required value={valoare} disabled={ocupat || final} onChange={e => setValoare(e.target.value)} /></label>{!final && <button className="tv-pastila" disabled={ocupat}>Salvează răspunsul numeric</button>}</form>}
    {q.raspunsa && !final && <p>Răspuns salvat. Îl poți schimba până la final.</p>}
    {final && <p>{q.corect ? 'Corect.' : q.raspunsa ? 'Răspuns greșit.' : 'Fără răspuns.'} {q.tip !== 'grila' && `Răspuns corect: ${q.raspunsNumeric}.`} {q.explicatie}</p>}
  </article>
}

function Examen({ initial, onInchis }) {
  const [date, setDate] = useState(initial)
  const [eroare, setEroare] = useState('')
  const [ocupat, setOcupat] = useState(false)
  const [ceas, setCeas] = useState(() => ({ acum: Date.now(), decalaj: new Date(initial.serverLa).getTime() - Date.now() }))
  const final = !!date.finalizatLa
  useEffect(() => {
    if (date.finalizatLa) return
    let activ = true; let inchide = false
    const timer = setInterval(() => {
      const acum = Date.now()
      setCeas(c => ({ ...c, acum }))
      if (!inchide && acum + ceas.decalaj >= new Date(date.expiraLa).getTime()) {
        inchide = true
        api(`/simulari/${date.id}/finalizare`, { metoda: 'POST' }).then(r => { if (activ) setDate(r) }).catch(e => { if (activ) { setEroare(e.message); inchide = false } })
      }
    }, 1000)
    return () => { activ = false; clearInterval(timer) }
  }, [date.id, date.expiraLa, date.finalizatLa, ceas.decalaj])
  async function trimite(corp, finalizare = false) {
    setOcupat(true); setEroare('')
    try { setDate(await api(`/simulari/${date.id}/${finalizare ? 'finalizare' : 'raspuns'}`, { metoda: 'POST', ...(corp ? { corp } : {}) })) } catch (e) { setEroare(e.message) } finally { setOcupat(false) }
  }
  const secunde = Math.max(0, Math.ceil((new Date(date.expiraLa).getTime() - ceas.acum - ceas.decalaj) / 1000))
  const raspunse = date.intrebari.filter(q => q.raspunsa).length
  return <section className="tv-cutie" aria-label="Simularea curentă"><h2>{final ? 'Rezultatul simulării' : 'Simulare în desfășurare'}</h2>
    {!final && <><p role="timer">Timp rămas: {Math.floor(secunde / 60)}:{String(secunde % 60).padStart(2, '0')}</p><p>{raspunse}/{date.total} răspunsuri salvate. Cronometrul continuă dacă închizi pagina.</p></>}
    {final && <div role="status"><h3>{date.corecte}/{date.total} corecte · Nota orientativă: {date.notaOrientativa.toLocaleString('ro-RO')}</h3><p>Estimare din întrebările aplicației; nu este o notă oficială sau o reproducere a subiectului de examen.</p></div>}
    {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    {date.intrebari.map(q => <Intrebare key={q.id} q={q} final={final} ocupat={ocupat || secunde === 0} onRaspunde={trimite} />)}
    {!final && <><p>{date.total - raspunse} întrebări fără răspuns vor conta ca greșite dacă închei acum.</p><button className="tv-btn" disabled={ocupat} onClick={() => trimite(null, true)}>Încheie simularea</button></>}
    {final && <><h3>Lecții de recapitulat</h3>{date.deRecapitulat.length === 0 ? <p>Ai răspuns corect la toate întrebările.</p> : <ul>{date.deRecapitulat.map(l => <li key={l.parcursId}><Link href={rute.parcurs(l.parcursId)}>{l.lectie}</Link> · {l.greseli} greșeli</li>)}</ul>}<button className="tv-pastila" onClick={onInchis}>Înapoi la simulări</button></>}
  </section>
}

export default function Simulari() {
  const u = usePagina('elev')
  const [optiuni, setOptiuni] = useState([])
  const [istoric, setIstoric] = useState(null)
  const [pagina, setPagina] = useState(1)
  const [versiune, setVersiune] = useState(0)
  const [tip, setTip] = useState('bac')
  const [clasa, setClasa] = useState('Bac')
  const [materie, setMaterie] = useState('')
  const [numar, setNumar] = useState(10)
  const [durata, setDurata] = useState(30)
  const [examen, setExamen] = useState(null)
  const [ocupat, setOcupat] = useState(false)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    if (!u) return
    let activ = true
    Promise.all([api('/simulari/optiuni'), api(`/simulari?pagina=${pagina}`)]).then(([o, h]) => { if (activ) { setOptiuni(o); setIstoric(h) } }).catch(e => { if (activ) setEroare(e.message) })
    return () => { activ = false }
  }, [u, pagina, versiune])
  const disponibile = optiuni.filter(o => o.clasa === clasa)
  const aleasa = disponibile.find(o => String(o.materieId) === materie)
  async function incepe(e) {
    e.preventDefault(); setOcupat(true); setEroare('')
    try { setExamen(await api('/simulari', { metoda: 'POST', corp: { tip, clasa, materieId: Number(materie), numarIntrebari: numar, durataMinute: durata } })) } catch (e) { setEroare(e.message) } finally { setOcupat(false) }
  }
  async function reia(id) {
    setOcupat(true); setEroare('')
    try { setExamen(await api(`/simulari/${id}`)) } catch (e) { setEroare(e.message) } finally { setOcupat(false) }
  }
  if (!u) return <Incarcare />
  return <main className="tv-pagina"><div className="tv-continut"><section className="tv-erou"><h1>Simulări de examen</h1><p>Bac sau Evaluare Națională, cu întrebări din lecțiile aplicației. Rezultatele sunt orientative.</p></section><div className="tv-coala">
    {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    {examen ? <Examen key={examen.id} initial={examen} onInchis={() => { setExamen(null); setVersiune(v => v + 1) }} /> : <>
      <form className="tv-cutie tv-form" onSubmit={incepe}>
        <label>Examen<select className="tv-camp-text" value={tip} onChange={e => { setTip(e.target.value); setClasa(e.target.value === 'bac' ? 'Bac' : 'VIII'); setMaterie('') }}><option value="bac">Bac</option><option value="evaluare_nationala">Evaluare Națională</option></select></label>
        <label>Clasă<select className="tv-camp-text" value={clasa} onChange={e => { setClasa(e.target.value); setMaterie('') }}>{(tip === 'bac' ? ['IX', 'X', 'XI', 'XII', 'Bac'] : ['VIII']).map(c => <option key={c}>{c}</option>)}</select></label>
        <label>Materie<select className="tv-camp-text" required value={materie} onChange={e => setMaterie(e.target.value)}><option value="">Alege materia</option>{disponibile.map(o => <option value={o.materieId} key={o.materieId}>{o.materie} ({o.disponibile} întrebări)</option>)}</select></label>
        {disponibile.length === 0 && <p>Nu sunt încă întrebări pentru această clasă. Profesorii pot adăuga lecții și întrebări.</p>}
        <label>Număr de întrebări<input className="tv-camp-text" type="number" min={5} max={50} required value={numar} onChange={e => setNumar(Number(e.target.value))} /></label>
        <label>Durata în minute<input className="tv-camp-text" type="number" min={5} max={180} required value={durata} onChange={e => setDurata(Number(e.target.value))} /></label>
        <button className="tv-btn" disabled={ocupat || !aleasa || aleasa.disponibile < numar}>Începe simularea</button>
      </form>
      <section className="tv-cutie"><h2>Istoricul tău</h2>{istoric?.elemente.length === 0 && <p>Nu ai început încă o simulare.</p>}{istoric?.elemente.map(s => <div className="tv-rand" key={s.id}><span>#{s.id} · {s.tip === 'bac' ? 'Bac' : 'Evaluare Națională'} · {s.clasa} · {dataOra(s.inceputLa)}</span><button className="tv-pastila" disabled={ocupat} onClick={() => reia(s.id)}>{s.finalizatLa || new Date(s.expiraLa) <= new Date() ? 'Vezi rezultatul' : 'Reia simularea'}</button></div>)}<Paginare pagina={pagina} totalPagini={Math.ceil((istoric?.total ?? 0) / 10)} onAlege={setPagina} /></section>
    </>}
  </div></div></main>
}
