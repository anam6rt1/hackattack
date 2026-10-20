'use client'
// /admin/continut : Lecțiile (parcursurile) și capitolele lor, pentru profesori aprobați, administratori de organizații
// și administratorii platformei. Fiecare vede doar ce poate edita: lecțiile proprii, cele ale organizației pe care o
// administrează și (adminul platformei) toate.
// Lecția are o clasă, o organizație de bază (al cărei administrator o poate edita), vizibilitate publică sau doar pentru
// organizație, și poate fi arătată (doar vizualizare) și în celelalte organizații ale autorului.
// La salvarea unui capitol nou sau la schimbarea materialului, serverul generează întrebări cu AI; fișierele capitolului
// (PDF, DOCX, PPTX) au propria secțiune, iar textul lor intră și el în întrebări.

import { useCallback, useEffect, useState } from 'react'
import { api, marime, urlApi } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { CLASE, numeClasa } from '@/lib/clase'
import Incarcare from '@/components/Incarcare'

const CURS_GOL = { titlu: '', descriere: '', capitol: '', ordine: '', videoUrl: '', continut: '', publicat: true, materiale: [{ titlu: '', text: '' }] }
const LECTIE_GOALA = { titlu: '', descriere: '', clasa: 'Bac', vizibilitate: 'public', organizatieId: null, partajari: [], publicat: true }

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
  if (!deschis) return <button className="tv-pastila" style={{ justifySelf: 'start' }} onClick={() => setDeschis(true)}>+ Materie nouă</button>
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

function EditorLectie({ lectie, materii, materieImplicita, organizatiiNoi, esteAdmin, onSalvata, onStearsa, onAnulat }) {
  const nou = !lectie?.id
  const [date, setDate] = useState(() => (nou
    ? { ...LECTIE_GOALA, materieId: materieImplicita ?? materii[0]?.id, organizatieId: esteAdmin ? null : organizatiiNoi[0]?.id ?? null }
    : { ...lectie }))
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)
  const posibile = nou ? organizatiiNoi : lectie.organizatiiPosibile
  const poateMuta = nou || lectie.poateMuta
  const aleasa = posibile.find((o) => o.id === date.organizatieId)
  // Organizația poate opri publicarea publică pentru autor; fără organizație (conținut de platformă) e mereu permisă.
  const publicPermis = date.organizatieId == null || (aleasa ? aleasa.poatePublicaPublic : lectie?.poatePublicaPublic ?? true)
  const camp = (k) => ({ value: date[k] ?? '', onChange: (e) => setDate({ ...date, [k]: e.target.value }) })
  // Cu publicarea publică oprită, lecția poate fi doar a organizației; fără organizație, doar publică.
  const vizibilitate = !publicPermis ? 'organizatie' : date.organizatieId == null ? 'public' : date.vizibilitate

  async function salveaza(e) {
    e.preventDefault()
    setTrimite(true)
    setEroare('')
    const corp = {
      materieId: Number(date.materieId), titlu: date.titlu, descriere: date.descriere || null, clasa: date.clasa, vizibilitate,
      organizatieId: date.organizatieId, partajari: date.partajari.filter((id) => id !== date.organizatieId), publicat: date.publicat, ordine: null,
    }
    try {
      onSalvata(nou ? await api('/parcursuri', { metoda: 'POST', corp }) : await api(`/parcursuri/${lectie.id}`, { metoda: 'PUT', corp: { ...corp, ordine: lectie.ordine } }))
    } catch (err) {
      setEroare(err.message)
    } finally {
      setTrimite(false)
    }
  }

  async function sterge() {
    const capitole = lectie.numarCapitole === 1 ? '1 capitol' : `${lectie.numarCapitole} capitole`
    if (!window.confirm(`Ștergi definitiv lecția „${lectie.titlu}”, cu ${capitole}? Se șterg întrebările și progresul elevilor la ele; XP-ul deja câștigat rămâne.`)) return
    try {
      await api(`/parcursuri/${lectie.id}`, { metoda: 'DELETE' })
      onStearsa()
    } catch (err) {
      setEroare(err.message)
    }
  }

  const comutaPartajare = (id) => setDate({ ...date, partajari: date.partajari.includes(id) ? date.partajari.filter((x) => x !== id) : [...date.partajari, id] })
  const dePartajat = posibile.filter((o) => o.id !== date.organizatieId)

  return (
    <form className="tv-form" onSubmit={salveaza}>
      <h3 style={{ fontSize: 21, color: 'var(--navy)' }}>{nou ? 'Lecție nouă' : 'Lecția'}</h3>
      {!nou && lectie.autor && <p className="tv-indiciu">Autor: {lectie.autor}</p>}
      <label className="tv-eticheta-camp"><span>Titlul lecției</span><input className="tv-camp-text" required maxLength={200} placeholder="ex. Basmul cult: Harap-Alb" {...camp('titlu')} /></label>
      <label className="tv-eticheta-camp"><span>Descriere scurtă (opțional)</span><input className="tv-camp-text" maxLength={1000} {...camp('descriere')} /></label>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp">
          <span>Materia</span>
          <select className="tv-camp-text" value={date.materieId ?? ''} disabled={!nou} onChange={(e) => setDate({ ...date, materieId: Number(e.target.value) })}>
            {(nou ? materii : [{ id: lectie.materieId, denumire: lectie.materie }]).map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
          </select>
        </label>
        <label className="tv-eticheta-camp">
          <span>Clasa</span>
          <select className="tv-camp-text" {...camp('clasa')}>
            {CLASE.map((c) => <option key={c} value={c}>{numeClasa(c)}</option>)}
          </select>
        </label>
      </div>
      <label className="tv-eticheta-camp">
        <span>Organizația lecției</span>
        <select className="tv-camp-text" value={date.organizatieId ?? ''} disabled={!poateMuta}
          onChange={(e) => setDate({ ...date, organizatieId: e.target.value === '' ? null : Number(e.target.value) })}>
          {esteAdmin && <option value="">Fără organizație (conținut al platformei)</option>}
          {!poateMuta && !aleasa && date.organizatieId != null && <option value={date.organizatieId}>{lectie.organizatie}</option>}
          {posibile.map((o) => <option key={o.id} value={o.id}>{o.nume}</option>)}
        </select>
        {!poateMuta && <small className="tv-indiciu">Doar autorul poate muta lecția în altă organizație.</small>}
        {nou && !esteAdmin && posibile.length === 0 && <small className="tv-eroare">Nu ești încă profesor aprobat în nicio organizație.</small>}
      </label>
      <fieldset className="tv-eticheta-camp">
        <span>Cine o vede</span>
        <div className="tv-butoane">
          <label className="tv-optiune" style={publicPermis ? undefined : { opacity: 0.5, cursor: 'not-allowed' }}>
            <input type="radio" name="vizibilitate" value="public" checked={vizibilitate === 'public'} disabled={!publicPermis}
              onChange={() => setDate({ ...date, vizibilitate: 'public' })} /> Toți elevii
          </label>
          <label className="tv-optiune" style={date.organizatieId == null ? { opacity: 0.5, cursor: 'not-allowed' } : undefined}>
            <input type="radio" name="vizibilitate" value="organizatie" checked={vizibilitate === 'organizatie'} disabled={date.organizatieId == null}
              onChange={() => setDate({ ...date, vizibilitate: 'organizatie' })} /> Doar organizația
          </label>
        </div>
        {!publicPermis && <small className="tv-indiciu">Organizația nu permite autorului să publice lecții pentru toți, așa că lecția rămâne doar pentru organizație.</small>}
      </fieldset>
      {dePartajat.length > 0 && (
        <fieldset className="tv-eticheta-camp">
          <span>Arată lecția și în (doar vizualizare)</span>
          {dePartajat.map((o) => (
            <label key={o.id} className="tv-bifa">
              <input type="checkbox" checked={date.partajari.includes(o.id)} onChange={() => comutaPartajare(o.id)} /> {o.nume}
            </label>
          ))}
          <small className="tv-indiciu">Elevii acestor organizații o văd; administratorii lor nu o pot edita.</small>
        </fieldset>
      )}
      <label className="tv-optiune" style={{ justifySelf: 'start' }}>
        <input type="checkbox" checked={date.publicat} onChange={(e) => setDate({ ...date, publicat: e.target.checked })} /> Publicată
      </label>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-gri" onClick={onAnulat}>Renunț</button>
        <button className="tv-btn" disabled={trimite || (nou && !esteAdmin && posibile.length === 0)}>{trimite ? 'Se salvează...' : nou ? 'Creează lecția' : 'Salvează lecția'}</button>
      </div>
      {!nou && <button type="button" className="tv-pastila tv-pastila-rosu" style={{ justifySelf: 'start' }} onClick={sterge}>Șterge lecția</button>}
    </form>
  )
}

// Capitolele unei lecții, în ordinea recomandată; săgețile schimbă ordinea.
function Capitole({ lectie, ales, onAlege, versiune }) {
  const [capitole, setCapitole] = useState(null)
  const [eroare, setEroare] = useState('')
  const incarca = useCallback(() => api(`/continut/cursuri?parcursId=${lectie.id}`).then((r) => setCapitole(r.elemente)), [lectie.id])
  useEffect(() => { incarca() }, [incarca, versiune])

  async function muta(i, d) {
    const ids = capitole.map((c) => c.id)
    ;[ids[i], ids[i + d]] = [ids[i + d], ids[i]]
    try {
      await api(`/parcursuri/${lectie.id}/ordine`, { metoda: 'PUT', corp: { cursIds: ids } })
      incarca()
    } catch (err) {
      setEroare(err.message)
    }
  }

  if (!capitole) return null
  return (
    <div className="tv-form">
      <div className="tv-butoane" style={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ fontSize: 21, color: 'var(--navy)' }}>Capitole (insulele hărții)</h3>
        <button className="tv-pastila tv-pastila-aur" onClick={() => onAlege('nou')}>+ Capitol nou</button>
      </div>
      <p className="tv-indiciu">Elevii pot deschide orice capitol; ordinea de aici e cea recomandată pe hartă.</p>
      {eroare && <p className="tv-eroare">{eroare}</p>}
      <ol className="tv-lista">
        {capitole.map((c, i) => (
          <li key={c.id} className="tv-butoane" style={{ alignItems: 'center', flexWrap: 'nowrap' }}>
            <button className={`tv-rand tv-rand-buton ${ales === c.id ? 'tv-rand-activ' : ''}`} style={{ flex: 1 }} onClick={() => onAlege(c.id)}>
              <div>
                <b>{i + 1}. {c.titlu}</b>
                <small>{c.capitol ? `${c.capitol} · ` : ''}{c.intrebari} întrebări</small>
              </div>
              {!c.publicat && <span className="tv-pastila">Ciornă</span>}
            </button>
            <button className="tv-pastila" disabled={i === 0} onClick={() => muta(i, -1)} aria-label={`Mută „${c.titlu}” mai sus`}>↑</button>
            <button className="tv-pastila" disabled={i === capitole.length - 1} onClick={() => muta(i, 1)} aria-label={`Mută „${c.titlu}” mai jos`}>↓</button>
          </li>
        ))}
        {capitole.length === 0 && <p className="tv-indiciu">Lecția nu are încă niciun capitol.</p>}
      </ol>
    </div>
  )
}

function EditorCurs({ parcursId, curs, onSalvat, onAnulat, onSters }) {
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
      parcursId, titlu: date.titlu, descriere: date.descriere || null, capitol: date.capitol || null,
      ordine: null, videoUrl: date.videoUrl || null, continut: date.continut || null,
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

  async function sterge() {
    if (!window.confirm(`Ștergi definitiv capitolul „${curs.titlu}”? Se șterg și întrebările lui și progresul elevilor la el; XP-ul deja câștigat rămâne.`)) return
    try {
      await api(`/cursuri/${curs.id}`, { metoda: 'DELETE' })
      onSters()
    } catch (err) {
      setEroare(err.message)
    }
  }

  return (
    <form className="tv-form" onSubmit={salveaza}>
      <label className="tv-eticheta-camp"><span>Titlul capitolului (numele insulei)</span><input className="tv-camp-text" required maxLength={200} {...camp('titlu')} /></label>
      <label className="tv-eticheta-camp"><span>Etichetă (opțional, apare sub numele insulei)</span><input className="tv-camp-text" maxLength={120} placeholder="ex. Basmul cult" {...camp('capitol')} /></label>
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
        <input type="checkbox" checked={date.publicat} onChange={(e) => setDate({ ...date, publicat: e.target.checked })} /> Publicat (vizibil elevilor care văd lecția)
      </label>
      <p className="tv-indiciu">După salvare, întrebările se generează automat din notițe, materialul suplimentar și fișierele capitolului.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-gri" onClick={onAnulat}>Renunț</button>
        <button className="tv-btn" disabled={trimite}>{trimite ? 'Se salvează...' : 'Salvează capitolul'}</button>
      </div>
      {curs?.id && (
        <button type="button" className="tv-pastila tv-pastila-rosu" style={{ justifySelf: 'start' }} onClick={sterge}>Șterge capitolul</button>
      )}
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

const TIPURI_FISIER = '.pdf,.docx,.pptx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.openxmlformats-officedocument.presentationml.presentation'
const STARE_TEXT = {
  in_asteptare: 'se citește textul...',
  gata: null,
  fara_text: 'fără text (de ex. un PDF scanat): elevii îl văd, dar nu ajută la întrebări',
  eroare: 'textul nu a putut fi citit: elevii îl văd, dar nu ajută la întrebări',
}

// Fișierele capitolului (PDF, DOCX, PPTX): se încarcă pe rând; textul lor intră în materialul pentru întrebările AI,
// iar serverul le regenerează o singură dată, la un minut după ultimul fișier.
function Fisiere({ cursId }) {
  const [date, setDate] = useState(null)
  const [incarcare, setIncarcare] = useState(null) // { nume, ramase }
  const [erori, setErori] = useState([])

  const incarca = useCallback(() => api(`/continut/cursuri/${cursId}/fisiere`).then(setDate), [cursId])

  // Cât timp se citește textul unui fișier sau așteaptă regenerarea, lista se reîmprospătează.
  useEffect(() => {
    incarca()
  }, [incarca])
  const asteapta = !!date && (date.regenerareProgramata || date.elemente.some((f) => f.stareText === 'in_asteptare'))
  useEffect(() => {
    if (!asteapta) return
    const t = setInterval(incarca, 4000)
    return () => clearInterval(t)
  }, [asteapta, incarca])

  async function adauga(e) {
    const alese = [...e.target.files]
    e.target.value = ''
    const noi = []
    for (const [i, f] of alese.entries()) {
      setIncarcare({ nume: f.name, ramase: alese.length - i })
      // Verificat și aici: peste limită serverul închide conexiunea, iar browserul ar arăta doar o eroare de rețea.
      if (f.size > date.maxMb * 1024 * 1024) {
        noi.push(`${f.name}: are ${marime(f.size)}; limita este ${date.maxMb} MB.`)
        continue
      }
      const formular = new FormData()
      formular.append('fisier', f)
      try {
        await api(`/cursuri/${cursId}/fisiere`, { metoda: 'POST', formular })
      } catch (err) {
        noi.push(`${f.name}: ${err.message}`)
      }
    }
    setIncarcare(null)
    setErori(noi)
    incarca()
  }

  async function sterge(f) {
    if (!confirm(`Ștergi fișierul „${f.nume}”? Elevii nu îl vor mai vedea.`)) return
    try {
      await api(`/fisiere/${f.id}`, { metoda: 'DELETE' })
      setErori([])
    } catch (err) {
      setErori([err.message])
    }
    incarca()
  }

  if (!date) return null
  const plin = date.elemente.length >= date.maxim
  return (
    <div className="tv-form">
      <h3 style={{ fontSize: 21, color: 'var(--navy)' }}>Fișiere</h3>
      <p className="tv-indiciu">
        PDF, Word (.docx) sau PowerPoint (.pptx), cel mult {date.maxMb} MB fiecare și {date.maxim} pe capitol. Elevii le deschid din capitol;
        textul lor e folosit și la întrebările generate cu AI.
      </p>
      <ul className="tv-lista">
        {date.elemente.map((f) => (
          <li key={f.id} className="tv-rand">
            <div>
              <b>{f.nume}</b>
              <small>
                {f.tip.toUpperCase()} · {marime(f.marimeOcteti)}
                {f.incarcatDe ? ` · de ${f.incarcatDe}` : ''}
                {STARE_TEXT[f.stareText] ? ` · ${STARE_TEXT[f.stareText]}` : ''}
              </small>
            </div>
            <a className="tv-pastila" href={urlApi(`/fisiere/${f.id}`)} target="_blank" rel="noreferrer">Deschide</a>
            <button type="button" className="tv-pastila tv-pastila-rosu" onClick={() => sterge(f)}>Șterge</button>
          </li>
        ))}
        {date.elemente.length === 0 && <p className="tv-gol">Capitolul nu are fișiere.</p>}
      </ul>
      {incarcare && (
        <p className="tv-status">
          <b>Se încarcă „{incarcare.nume}”...</b>
          {incarcare.ramase > 1 && <span>Mai sunt {incarcare.ramase - 1} după el.</span>}
        </p>
      )}
      {date.regenerareProgramata && !incarcare && (
        <p className="tv-status">
          <b>Întrebările vor fi regenerate din fișiere.</b>
          <span>Pornește la un minut după ultimul fișier adăugat sau șters.</span>
        </p>
      )}
      {erori.map((e) => <p key={e} className="tv-eroare">{e}</p>)}
      <label className="tv-eticheta-camp">
        <span>{plin ? `Ai ajuns la ${date.maxim} fișiere: șterge unul ca să adaugi altul.` : 'Adaugă fișiere'}</span>
        <input className="tv-fisier" type="file" multiple accept={TIPURI_FISIER} onChange={adauga} disabled={plin || !!incarcare} />
      </label>
    </div>
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
      {intrebari?.length === 0 && !lucreaza && <p className="tv-gol">Capitolul nu are încă întrebări.</p>}
      <ul className="tv-lista">{intrebari?.map((q) => <Intrebare key={q.id} q={q} onSalvat={incarca} />)}</ul>
    </div>
  )
}

export default function AdminContinut() {
  const utilizator = usePagina('admin', 'profesor')
  const esteAdmin = utilizator?.rol === 'admin'
  const [materii, setMaterii] = useState(null) // materiile în care utilizatorul poate crea lecții
  const [organizatii, setOrganizatii] = useState([]) // organizațiile posibile pentru o lecție nouă
  const [filtruMaterie, setFiltruMaterie] = useState('')
  const [lectii, setLectii] = useState([])
  const [lectie, setLectie] = useState(null) // null | 'noua' | ParcursEditabil
  const [capitol, setCapitol] = useState(null) // null | 'nou' | id
  const [detaliu, setDetaliu] = useState(null)
  const [versiune, setVersiune] = useState(0) // crește când se schimbă capitolele, ca lista să se reîncarce

  // profesorii creează lecții doar la materiile pe care le predau
  const materiiPermise = useCallback(async () => {
    const toate = (await api('/materii')).elemente
    if (esteAdmin) return toate
    return (await api('/profesori/me')).profil.materii.map((m) => toate.find((x) => x.id === m.id)).filter(Boolean)
  }, [esteAdmin])

  const incarcaLectii = useCallback(() => api('/continut/parcursuri').then((r) => setLectii(r.elemente)), [])

  useEffect(() => {
    if (!utilizator) return
    materiiPermise().then(setMaterii)
    api('/continut/organizatii').then((r) => setOrganizatii(r.elemente)).catch(() => setOrganizatii([]))
    incarcaLectii()
  }, [utilizator, materiiPermise, incarcaLectii])

  function deschideLectie(l) {
    setLectie(l)
    setCapitol(null)
    setDetaliu(null)
  }
  function deschideCapitol(id) {
    setCapitol(id)
    setDetaliu(null)
    if (typeof id === 'number') api(`/cursuri/${id}`).then(setDetaliu)
  }

  if (!utilizator || !materii) return <Incarcare />
  const vizibile = lectii.filter((l) => !filtruMaterie || l.materieId === Number(filtruMaterie))
  const materiiFiltru = [...new Map([...materii, ...lectii.map((l) => ({ id: l.materieId, denumire: l.materie }))].map((m) => [m.id, m])).values()]

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Conținut</span>
          <h1>Lecții, capitole și întrebări</h1>
          <p>O lecție e o hartă pentru elevi; fiecare capitol al ei e o insulă, cu notițe, un video opțional și întrebări.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-editor">
            <aside className="tv-cutie">
              <label className="tv-eticheta-camp">
                <span>Materia</span>
                <select className="tv-camp-text" value={filtruMaterie} onChange={(e) => setFiltruMaterie(e.target.value)}>
                  <option value="">Toate materiile</option>
                  {materiiFiltru.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
                </select>
              </label>
              {esteAdmin && <MaterieNoua onCreata={async (m) => { setMaterii(await materiiPermise()); setFiltruMaterie(String(m.id)) }} />}
              <button className="tv-btn" onClick={() => deschideLectie('noua')} disabled={materii.length === 0}>+ Lecție nouă</button>
              {materii.length === 0 && <p className="tv-indiciu">Adaugă în profilul de profesor materiile pe care le predai, ca să poți crea lecții.</p>}
              <ul className="tv-lista">
                {vizibile.map((l) => (
                  <li key={l.id}>
                    <button className={`tv-rand tv-rand-buton ${lectie?.id === l.id ? 'tv-rand-activ' : ''}`} onClick={() => deschideLectie(l)}>
                      <div>
                        <b>{l.titlu}</b>
                        <small>
                          {l.materie} · {numeClasa(l.clasa)} · {l.organizatie ?? 'platformă'}
                          {l.autorId !== utilizator.id && l.autor ? ` · de ${l.autor}` : ''}
                        </small>
                      </div>
                      <span className="tv-butoane" style={{ gap: 4 }}>
                        {l.vizibilitate === 'organizatie' && <span className="tv-pastila">Organizație</span>}
                        {!l.publicat && <span className="tv-pastila">Ciornă</span>}
                      </span>
                    </button>
                  </li>
                ))}
                {vizibile.length === 0 && <p className="tv-indiciu">Nu ai încă lecții aici.</p>}
              </ul>
            </aside>

            <section className="tv-cutie">
              {lectie === null && <p className="tv-gol">Alege o lecție din listă sau creează una nouă.</p>}
              {lectie !== null && (
                <EditorLectie
                  key={lectie === 'noua' ? 'noua' : `${lectie.id}-${lectie.vizibilitate}-${lectie.organizatieId}`}
                  lectie={lectie === 'noua' ? null : lectie}
                  materii={materii}
                  materieImplicita={filtruMaterie ? Number(filtruMaterie) : undefined}
                  organizatiiNoi={organizatii}
                  esteAdmin={esteAdmin}
                  onSalvata={(l) => { incarcaLectii(); deschideLectie(l) }}
                  onStearsa={() => { incarcaLectii(); deschideLectie(null) }}
                  onAnulat={() => deschideLectie(null)}
                />
              )}
              {lectie?.id && (
                <>
                  <hr className="tv-linie" />
                  <Capitole lectie={lectie} ales={capitol} onAlege={deschideCapitol} versiune={versiune} />
                </>
              )}
              {lectie?.id && capitol === 'nou' && (
                <>
                  <hr className="tv-linie" />
                  <EditorCurs key="nou" parcursId={lectie.id} curs={null} onAnulat={() => deschideCapitol(null)}
                    onSalvat={(c) => { setVersiune((v) => v + 1); incarcaLectii(); setCapitol(c.id); setDetaliu(c) }} />
                </>
              )}
              {lectie?.id && typeof capitol === 'number' && detaliu && (
                <>
                  <hr className="tv-linie" />
                  <EditorCurs key={detaliu.id} parcursId={lectie.id} curs={detaliu} onAnulat={() => deschideCapitol(null)}
                    onSalvat={(c) => { setVersiune((v) => v + 1); setDetaliu(c) }}
                    onSters={() => { setVersiune((v) => v + 1); incarcaLectii(); deschideCapitol(null) }} />
                  <hr className="tv-linie" />
                  <Fisiere key={`f-${detaliu.id}`} cursId={detaliu.id} />
                  <hr className="tv-linie" />
                  <Intrebari key={`q-${detaliu.id}-${detaliu.titlu}`} cursId={detaliu.id} />
                </>
              )}
            </section>
          </div>
        </div>
      </div>
    </main>
  )
}
