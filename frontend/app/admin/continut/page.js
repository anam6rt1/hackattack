'use client'
// /admin/continut : materiile și lecțiile (pentru profesori aprobați și administratori).
// La salvarea unei lecții noi sau la schimbarea materialului, serverul generează întrebări cu AI;
// pagina arată progresul generării și permite verificarea și corectarea întrebărilor.

import { useCallback, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'

const CURS_GOL = { titlu: '', descriere: '', capitol: '', ordine: '', videoUrl: '', continut: '', publicat: true, materiale: [{ titlu: '', text: '' }] }

function MaterieNoua({ onCreata }) {
  const [deschis, setDeschis] = useState(false)
  const [eroare, setEroare] = useState('')
  async function trimite(e) {
    e.preventDefault()
    const f = Object.fromEntries(new FormData(e.currentTarget))
    try {
      const m = await api('/materii', { metoda: 'POST', corp: { denumire: f.denumire, descriere: f.descriere || null, cod: f.cod || null } })
      setDeschis(false)
      onCreata(m)
    } catch (err) {
      setEroare(err.message)
    }
  }
  if (!deschis) return <button className="tv-pastila" onClick={() => setDeschis(true)}>+ Materie nouă</button>
  return (
    <form className="tv-form" onSubmit={trimite}>
      <input className="tv-camp-text" name="denumire" placeholder="Denumire" required aria-label="Denumirea materiei" />
      <input className="tv-camp-text" name="cod" placeholder="cod-scurt (opțional)" aria-label="Cod" />
      <textarea className="tv-camp-text" name="descriere" rows={2} placeholder="Descriere (opțional)" aria-label="Descriere" />
      {eroare && <p className="tv-eroare">{eroare}</p>}
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-gri" onClick={() => setDeschis(false)}>Renunț</button>
        <button className="tv-btn">Creează</button>
      </div>
    </form>
  )
}

function EditorCurs({ materieId, curs, onSalvat, onAnulat }) {
  const [date, setDate] = useState(() => ({ ...CURS_GOL, ...curs, ordine: curs?.ordine ?? '', materiale: curs?.materiale?.length ? curs.materiale : CURS_GOL.materiale }))
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)
  const camp = (k) => ({ value: date[k] ?? '', onChange: (e) => setDate({ ...date, [k]: e.target.value }) })
  const sectiune = (i, k, v) => setDate({ ...date, materiale: date.materiale.map((m, j) => (j === i ? { ...m, [k]: v } : m)) })

  async function salveaza(e) {
    e.preventDefault()
    setTrimite(true)
    setEroare('')
    const corp = {
      materieId, titlu: date.titlu, descriere: date.descriere || null, capitol: date.capitol || null,
      ordine: date.ordine === '' ? null : Number(date.ordine), videoUrl: date.videoUrl || null, continut: date.continut || null,
      publicat: date.publicat, materiale: date.materiale.filter((m) => m.titlu.trim() || m.text.trim()),
    }
    try {
      const salvat = curs?.id ? await api(`/cursuri/${curs.id}`, { metoda: 'PUT', corp }) : await api('/cursuri', { metoda: 'POST', corp })
      onSalvat(salvat)
    } catch (err) {
      setEroare(err.message)
    } finally {
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={salveaza}>
      <label className="tv-eticheta-camp"><span>Titlul lecției</span><input className="tv-camp-text" required maxLength={200} {...camp('titlu')} /></label>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp"><span>Capitol (insula de pe hartă)</span><input className="tv-camp-text" maxLength={120} placeholder="ex. Harap-Alb" {...camp('capitol')} /></label>
        <label className="tv-eticheta-camp"><span>Ordine</span><input className="tv-camp-text" type="number" min={0} placeholder="la final" {...camp('ordine')} /></label>
      </div>
      <label className="tv-eticheta-camp"><span>Descriere scurtă</span><input className="tv-camp-text" maxLength={1000} {...camp('descriere')} /></label>
      <label className="tv-eticheta-camp"><span>Link video (YouTube, opțional)</span><input className="tv-camp-text" type="url" placeholder="https://www.youtube.com/watch?v=..." {...camp('videoUrl')} /></label>

      <div className="tv-mic">Notițele lecției</div>
      {date.materiale.map((m, i) => (
        <div key={i} className="tv-sectiune-editor">
          <input className="tv-camp-text" placeholder={`Titlul secțiunii ${i + 1}`} value={m.titlu} onChange={(e) => sectiune(i, 'titlu', e.target.value)} aria-label={`Titlul secțiunii ${i + 1}`} />
          <textarea className="tv-camp-text" rows={3} placeholder="Textul secțiunii" value={m.text} onChange={(e) => sectiune(i, 'text', e.target.value)} aria-label={`Textul secțiunii ${i + 1}`} />
          {date.materiale.length > 1 && (
            <button type="button" className="tv-pastila tv-pastila-rosu" onClick={() => setDate({ ...date, materiale: date.materiale.filter((_, j) => j !== i) })}>Șterge secțiunea</button>
          )}
        </div>
      ))}
      <button type="button" className="tv-pastila" onClick={() => setDate({ ...date, materiale: [...date.materiale, { titlu: '', text: '' }] })}>+ Adaugă o secțiune</button>

      <label className="tv-eticheta-camp">
        <span>Material suplimentar pentru întrebări (opțional: text din manual, rezumat)</span>
        <textarea className="tv-camp-text" rows={5} maxLength={50000} {...camp('continut')} />
      </label>
      <label className="tv-optiune" style={{ justifySelf: 'start' }}>
        <input type="checkbox" checked={date.publicat} onChange={(e) => setDate({ ...date, publicat: e.target.checked })} /> Publicată (vizibilă elevilor)
      </label>
      <p className="tv-indiciu">După salvare, întrebările se generează automat din notițe și materialul suplimentar.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-gri" onClick={onAnulat}>Renunț</button>
        <button className="tv-btn" disabled={trimite}>{trimite ? 'Se salvează...' : 'Salvează lecția'}</button>
      </div>
    </form>
  )
}

function Intrebare({ q, onSalvat }) {
  const [editare, setEditare] = useState(false)
  const [date, setDate] = useState(q)
  const [eroare, setEroare] = useState('')

  async function salveaza(modificari) {
    const urmator = { ...date, ...modificari }
    try {
      const r = await api(`/intrebari/${q.id}`, {
        metoda: 'PUT',
        corp: { text: urmator.text, explicatie: urmator.explicatie, activa: urmator.activa, raspunsNumeric: urmator.raspunsNumeric, variante: q.tip === 'grila' ? urmator.variante.map((v) => ({ text: v.text, corecta: v.corecta })) : null },
      })
      setDate(r)
      setEditare(false)
      onSalvat()
    } catch (e) {
      setEroare(e.message)
    }
  }

  return (
    <li className={`tv-cutie tv-intrebare-editor ${date.activa ? '' : 'tv-inactiva'}`}>
      <div className="tv-butoane" style={{ justifyContent: 'space-between' }}>
        <span className="tv-butoane">
          <span className={`tv-pastila ${date.sursa === 'ai' ? 'tv-pastila-aur' : ''}`}>{date.sursa === 'ai' ? 'Generată AI' : 'Scrisă de om'}</span>
          <span className="tv-pastila">{date.tip === 'grila' ? 'Grilă' : 'Estimare'}</span>
        </span>
        <span className="tv-butoane">
          <button className="tv-pastila" onClick={() => setEditare(!editare)}>{editare ? 'Închide' : 'Editează'}</button>
          <button className="tv-pastila" onClick={() => salveaza({ activa: !date.activa })}>{date.activa ? 'Dezactivează' : 'Activează'}</button>
        </span>
      </div>
      {editare ? (
        <div className="tv-form">
          <textarea className="tv-camp-text" rows={2} value={date.text} onChange={(e) => setDate({ ...date, text: e.target.value })} aria-label="Întrebarea" />
          {date.tip === 'grila' ? (
            date.variante.map((v, i) => (
              <label key={i} className="tv-varianta-editor">
                <input type="radio" name={`corecta-${q.id}`} checked={v.corecta} onChange={() => setDate({ ...date, variante: date.variante.map((x, j) => ({ ...x, corecta: j === i })) })} aria-label="Varianta corectă" />
                <input className="tv-camp-text" value={v.text} onChange={(e) => setDate({ ...date, variante: date.variante.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)) })} aria-label={`Varianta ${i + 1}`} />
              </label>
            ))
          ) : (
            <input className="tv-camp-text" type="number" value={date.raspunsNumeric ?? ''} onChange={(e) => setDate({ ...date, raspunsNumeric: Number(e.target.value) })} aria-label="Răspunsul numeric" />
          )}
          <textarea className="tv-camp-text" rows={2} value={date.explicatie ?? ''} onChange={(e) => setDate({ ...date, explicatie: e.target.value })} placeholder="Explicația" aria-label="Explicația" />
          <button className="tv-btn" onClick={() => salveaza({})}>Salvează întrebarea</button>
        </div>
      ) : (
        <>
          <b>{date.text}</b>
          {date.tip === 'grila' ? (
            <ul className="tv-variante-lista">
              {date.variante.map((v) => <li key={v.id} className={v.corecta ? 'tv-corecta' : ''}>{v.text}</li>)}
            </ul>
          ) : (
            <p>Răspuns: <b>{date.raspunsNumeric}</b></p>
          )}
          {date.explicatie && <p className="tv-indiciu">{date.explicatie}</p>}
        </>
      )}
      {eroare && <p className="tv-eroare">{eroare}</p>}
    </li>
  )
}

function Intrebari({ cursId }) {
  const [generare, setGenerare] = useState(null)
  const [intrebari, setIntrebari] = useState(null)

  const incarca = useCallback(() => api(`/cursuri/${cursId}/intrebari/toate`).then((r) => setIntrebari(r.elemente)), [cursId])

  // Urmărește generarea cât timp e în lucru
  useEffect(() => {
    let oprit = false
    let t
    const verifica = async () => {
      const g = await api(`/cursuri/${cursId}/generare`).catch(() => null)
      if (oprit) return
      setGenerare(g)
      if (g && ['in_asteptare', 'in_lucru'].includes(g.stare)) t = setTimeout(verifica, 2000)
      else incarca()
    }
    verifica()
    return () => {
      oprit = true
      clearTimeout(t)
    }
  }, [cursId, incarca])

  async function regenereaza() {
    setGenerare(await api(`/cursuri/${cursId}/regenereaza-intrebari`, { metoda: 'POST' }))
    const verifica = async () => {
      const g = await api(`/cursuri/${cursId}/generare`)
      setGenerare(g)
      if (['in_asteptare', 'in_lucru'].includes(g.stare)) setTimeout(verifica, 2000)
      else incarca()
    }
    setTimeout(verifica, 1500)
  }

  const lucreaza = generare && ['in_asteptare', 'in_lucru'].includes(generare.stare)
  return (
    <div className="tv-form">
      <div className="tv-butoane" style={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ fontSize: 21, color: 'var(--navy)' }}>Întrebări</h3>
        <button className="tv-pastila tv-pastila-aur" onClick={regenereaza} disabled={lucreaza}>Generează din nou cu AI</button>
      </div>
      {lucreaza && <p className="tv-status"><b>Se generează întrebările...</b><span>Durează de obicei mai puțin de un minut.</span></p>}
      {generare?.stare === 'gata' && <p className="tv-succes">Au fost generate {generare.intrebariGenerate} întrebări noi. Verifică-le mai jos.</p>}
      {generare?.stare === 'eroare' && <p className="tv-eroare">{generare.mesaj}</p>}
      {intrebari?.length === 0 && !lucreaza && <p className="tv-gol">Lecția nu are încă întrebări.</p>}
      <ul className="tv-lista">{intrebari?.map((q) => <Intrebare key={q.id} q={q} onSalvat={incarca} />)}</ul>
    </div>
  )
}

export default function AdminContinut() {
  const utilizator = usePagina('admin', 'profesor')
  const [materii, setMaterii] = useState(null)
  const [materieId, setMaterieId] = useState(null)
  const [cursuri, setCursuri] = useState([])
  const [deschis, setDeschis] = useState(null) // null | 'nou' | id
  const [detaliu, setDetaliu] = useState(null)

  // profesorii lucrează doar la materiile pe care le predau
  const materiiPermise = useCallback(async () => {
    const toate = (await api('/materii')).elemente
    if (utilizator.rol === 'admin') return toate
    return (await api('/profesori/me')).profil.materii.map((m) => toate.find((x) => x.id === m.id)).filter(Boolean)
  }, [utilizator])

  const incarcaCursuri = useCallback(() => {
    if (materieId) api(`/continut/cursuri?materieId=${materieId}`).then((r) => setCursuri(r.elemente))
  }, [materieId])

  useEffect(() => {
    if (!utilizator) return
    materiiPermise().then((permise) => {
      setMaterii(permise)
      const dinLink = Number(new URLSearchParams(window.location.search).get('materie'))
      setMaterieId(permise.some((m) => m.id === dinLink) ? dinLink : permise[0]?.id ?? null)
    })
  }, [utilizator, materiiPermise])
  useEffect(() => {
    if (materieId) api(`/continut/cursuri?materieId=${materieId}`).then((r) => setCursuri(r.elemente))
  }, [materieId])

  function deschide(id) {
    setDeschis(id)
    setDetaliu(null)
    if (typeof id === 'number') api(`/cursuri/${id}`).then(setDetaliu)
  }

  if (!utilizator || !materii) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Conținut</span>
          <h1>Lecții și întrebări</h1>
          <p>Fiecare lecție are notițe, un video opțional și întrebări. Lecțiile cu același capitol formează o insulă pe hartă.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-editor">
            <aside className="tv-cutie">
              <label className="tv-eticheta-camp">
                <span>Materia</span>
                <select className="tv-camp-text" value={materieId ?? ''} onChange={(e) => { setMaterieId(Number(e.target.value)); deschide(null) }}>
                  {materii.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
                </select>
              </label>
              <MaterieNoua onCreata={async (m) => { setMaterii(await materiiPermise()); setMaterieId(m.id); deschide(null) }} />
              <button className="tv-btn" onClick={() => deschide('nou')} disabled={!materieId}>+ Lecție nouă</button>
              <ul className="tv-lista">
                {cursuri.map((c) => (
                  <li key={c.id}>
                    <button className={`tv-rand tv-rand-buton ${deschis === c.id ? 'tv-rand-activ' : ''}`} onClick={() => deschide(c.id)}>
                      <div>
                        <b>{c.titlu}</b>
                        <small>{c.capitol ?? 'Fără capitol'} · {c.intrebari} întrebări</small>
                      </div>
                      {!c.publicat && <span className="tv-pastila">Ciornă</span>}
                    </button>
                  </li>
                ))}
                {cursuri.length === 0 && <p className="tv-indiciu">Materia nu are încă lecții.</p>}
              </ul>
            </aside>

            <section className="tv-cutie">
              {deschis === 'nou' && (
                <EditorCurs key="nou" materieId={materieId} curs={null} onAnulat={() => deschide(null)} onSalvat={(c) => { incarcaCursuri(); setDeschis(c.id); setDetaliu(c) }} />
              )}
              {typeof deschis === 'number' && detaliu && (
                <>
                  <EditorCurs key={detaliu.id} materieId={materieId} curs={detaliu} onAnulat={() => deschide(null)} onSalvat={(c) => { incarcaCursuri(); setDetaliu(c) }} />
                  <hr className="tv-linie" />
                  <Intrebari key={`q-${detaliu.id}-${detaliu.titlu}`} cursId={detaliu.id} />
                </>
              )}
              {deschis === null && <p className="tv-gol">Alege o lecție din listă sau creează una nouă.</p>}
            </section>
          </div>
        </div>
      </div>
    </main>
  )
}
