'use client'
// /admin/continut : Lecțiile (parcursurile) și capitolele lor, pentru profesori aprobați, administratori de organizații
// și administratorii platformei. Fiecare vede doar ce poate edita: lecțiile proprii, cele ale organizației pe care o
// administrează și (adminul platformei) toate.
// Lecția are o clasă, o organizație de bază (al cărei administrator o poate edita), vizibilitate publică sau doar pentru
// organizație, și poate fi arătată (doar vizualizare) și în celelalte organizații ale autorului.
// Testul și fișierele sunt ale lecției întregi: cel mult un PDF, un DOCX, un PPTX și un XLSX (unul nou de același tip îl înlocuiește),
// iar întrebările testului se generează cu AI doar la cerere (câte vrea profesorul), din notițele tuturor capitolelor și
// textul fișierelor; se adaugă la cele existente și pot fi editate, șterse sau scrise de mână.

import { useCallback, useEffect, useState } from 'react'
import { useIncarcare } from '@/lib/useIncarcare'
import { useDocumente } from '@/lib/useDocumente'
import { marimeFisier } from '@/lib/marime-fisier'
import ProgresIncarcare from '@/components/ProgresIncarcare'
import PrevizualizareFisier from '@/components/PrevizualizareFisier'
import PrevizualizareSalvat from '@/components/PrevizualizareSalvat'
import { api, marime, urlApi } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { CLASE, numeClasa } from '@/lib/clase'
import Incarcare from '@/components/Incarcare'
import Paginare from '@/components/Paginare'
import { LECTII_PE_PAGINA_EDITOR_ADMIN, LECTII_PE_PAGINA_EDITOR_PROFESOR, cuPagina } from '@/lib/pagini'
import { useConfirmare } from '@/components/Confirmare'
import AlegeMultiplu from '@/components/AlegeMultiplu'
import { PARTAJARI_LECTIE, potriveste } from '@/lib/alegeri'

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
  const [confirma, fereastra] = useConfirmare()
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
    if (!(await confirma({
      titlu: 'Ștergi lecția?',
      mesaj: `„${lectie.titlu}”, cu ${capitole}, se șterge definitiv. Se șterg și testul, fișierele și progresul elevilor; XP-ul deja câștigat rămâne.`,
      da: 'Șterge lecția', pericol: true,
    }))) return
    try {
      await api(`/parcursuri/${lectie.id}`, { metoda: 'DELETE' })
      onStearsa()
    } catch (err) {
      setEroare(err.message)
    }
  }

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
      {/* Doar celelalte organizații ale autorului (câteva), venite deja cu lecția: căutarea lor rămâne aici. */}
      {dePartajat.length > 0 && (
        <AlegeMultiplu eticheta="Arată lecția și în (doar vizualizare)" buton="Adaugă organizație" titlu="Arată lecția și în"
          etichetaCautare="Caută o organizație" maxim={PARTAJARI_LECTIE} cheieCautare={dePartajat.length}
          alese={date.partajari.map((id) => dePartajat.find((o) => o.id === id) ?? { id, nume: `Organizația ${id}` }).map(({ id, nume }) => ({ id, nume }))}
          onChange={(alese) => setDate({ ...date, partajari: alese.map((a) => a.id) })}
          cauta={async (q) => ({ elemente: dePartajat.filter((o) => potriveste(o.nume, q)).map(({ id, nume }) => ({ id, nume })) })}
          indiciu="Elevii acestor organizații o văd; administratorii lor nu o pot edita." />
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
      {fereastra}
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
                {c.capitol && <small>{c.capitol}</small>}
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
  const [confirma, fereastra] = useConfirmare()
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
    if (!(await confirma({
      titlu: 'Ștergi capitolul?', mesaj: `„${curs.titlu}” se șterge definitiv. Testul și fișierele lecției rămân.`, da: 'Șterge capitolul', pericol: true,
    }))) return
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

      <div className="tv-mic">Notițele capitolului</div>
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
      <p className="tv-indiciu">Întrebările testului nu se generează singure: le ceri din secțiunea „Testul lecției”, după ce ai scris capitolele.</p>
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-gri" onClick={onAnulat}>Renunț</button>
        <button className="tv-btn" disabled={trimite}>{trimite ? 'Se salvează...' : 'Salvează capitolul'}</button>
      </div>
      {curs?.id && (
        <button type="button" className="tv-pastila tv-pastila-rosu" style={{ justifySelf: 'start' }} onClick={sterge}>Șterge capitolul</button>
      )}
      {fereastra}
    </form>
  )
}

function Intrebare({ q, onSalvat, onStearsa }) {
  const [editare, setEditare] = useState(false)
  const [date, setDate] = useState(q)
  const [eroare, setEroare] = useState('')
  const [confirma, fereastra] = useConfirmare()

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

  async function sterge() {
    if (!(await confirma({ titlu: 'Ștergi întrebarea?', mesaj: `„${date.text}” se șterge definitiv din testul lecției.`, da: 'Șterge', pericol: true }))) return
    try {
      await api(`/intrebari/${q.id}`, { metoda: 'DELETE' })
      onStearsa()
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
          <button className="tv-pastila tv-pastila-rosu" onClick={sterge}>Șterge</button>
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
      {fereastra}
    </li>
  )
}

const TIPURI_FISIER = '.pdf,.docx,.pptx,.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.openxmlformats-officedocument.presentationml.presentation'
const STARE_TEXT = {
  in_asteptare: 'se citește textul...',
  gata: null,
  fara_text: 'fără text (de ex. un PDF scanat): elevii îl văd, dar nu ajută la întrebări',
  eroare: 'textul nu a putut fi citit: elevii îl văd, dar nu ajută la întrebări',
}
const tipFisier = (nume) => nume.split('.').pop().toLowerCase()

// Fișierele lecției: cel mult un PDF, un Word (.docx), un PowerPoint (.pptx) și un Excel (.xlsx). Unul nou de un tip care există deja îl
// înlocuiește, după confirmare. Textul lor intră în materialul din care se generează întrebările (doar la cerere).
function Fisiere({ parcursId }) {
  const [date, setDate] = useState(null)
  const [incarcare, setIncarcare] = useState(null) // { nume, ramase }
  const [selectate, setSelectate] = useState([])
  const documente = useDocumente(selectate, false)
  const upload = useIncarcare()
  const [erori, setErori] = useState([])
  const [confirma, fereastra] = useConfirmare()

  const incarca = useCallback(() => api(`/continut/parcursuri/${parcursId}/fisiere`).then(r => { setDate(r); return r }), [parcursId])

  // Cât timp se citește textul unui fișier, lista se reîmprospătează.
  useEffect(() => {
    incarca()
  }, [incarca])
  const asteapta = !!date && date.elemente.some((f) => f.stareText === 'in_asteptare')
  useEffect(() => {
    if (!asteapta) return
    const t = setInterval(incarca, 4000)
    return () => clearInterval(t)
  }, [asteapta, incarca])

  async function adauga(e) {
    setSelectate([...e.target.files])
    e.target.value = ''
    setErori([])
  }

  async function trimiteSelectate() {
    if (incarcare || documente.inLucru) return
    const alese = selectate
    const pregatite = documente.items
    const noi = []
    const reusite = new Set()
    let lista = date.elemente
    setIncarcare({ nume: 'fișierele selectate', ramase: alese.length })
    for (const [i, sursa] of alese.entries()) {
      const f = pregatite[i].fisier
      if (!['pdf', 'docx', 'pptx', 'xlsx'].includes(tipFisier(f.name)) || !f.size) {
        noi.push(`${f.name}: alege un PDF, DOCX, PPTX sau XLSX nevid.`)
        continue
      }
      // Verificat și aici, ca fișierul să nu fie trimis degeaba.
      if (f.size > date.maxMb * 1024 * 1024) {
        noi.push(`${f.name}: are ${marime(f.size)}; limita este ${date.maxMb} MB.`)
        continue
      }
      const existent = lista.find((x) => x.tip === tipFisier(f.name))
      if (existent && !(await confirma({
        titlu: `Înlocuiești ${existent.tip.toUpperCase()}-ul?`,
        mesaj: `Lecția are deja „${existent.nume}”. Dacă îl înlocuiești cu „${f.name}”, cel vechi se șterge.`,
        da: 'Înlocuiește', nu: 'Păstrează-l pe cel vechi',
      }))) continue
      setIncarcare({ nume: f.name, ramase: alese.length - i })
      const formular = new FormData()
      formular.append('fisier', f)
      try {
        await upload.trimite(`/parcursuri/${parcursId}/fisiere${existent ? '?inlocuieste=true' : ''}`, formular)
        reusite.add(sursa)
        const actualizate = await incarca()
        lista = actualizate.elemente
      } catch (err) {
        if (err.name === 'AbortError') {
          noi.push('Încărcare anulată. Verifică lista înainte de a reîncerca; fișierele deja salvate rămân disponibile.')
          break
        }
        noi.push(`${f.name}: ${err.message}`)
      }
    }
    setIncarcare(null)
    setSelectate(alese.filter(f => !reusite.has(f)))
    setErori(noi)
    incarca().catch(e => setErori(erori => [...erori, e.message]))
  }

  async function sterge(f) {
    if (!(await confirma({ titlu: 'Ștergi fișierul?', mesaj: `„${f.nume}” se șterge, iar elevii nu îl vor mai vedea.`, da: 'Șterge', pericol: true }))) return
    try {
      await api(`/fisiere/${f.id}`, { metoda: 'DELETE' })
      setErori([])
    } catch (err) {
      setErori([err.message])
    }
    incarca()
  }

  if (!date) return null
  return (
    <div className="tv-form">
      <h3 style={{ fontSize: 21, color: 'var(--navy)' }}>Fișierele lecției</h3>
      <p className="tv-indiciu">
        Cel mult un PDF, un Word (.docx), un PowerPoint (.pptx) și un Excel (.xlsx), de până la {date.maxMb} MB fiecare. Elevii le deschid din pagina lecției;
        textul lor intră și în întrebările generate cu AI.
      </p>
      <ul className="tv-lista">
        {date.elemente.map((f) => (
          <li key={f.id} className="tv-rand" style={{ flexWrap: 'wrap' }}>
            <div>
              <b>{f.nume}</b>
              <small>
                {f.tip.toUpperCase()} · {marimeFisier(f.marimeOcteti)}
                {f.incarcatDe ? ` · de ${f.incarcatDe}` : ''}
                {f.stareText === 'gata'
                  ? ` · text citit (${f.caractereText.toLocaleString('ro-RO')} caractere), folosit la întrebările AI`
                  : STARE_TEXT[f.stareText] ? ` · ${STARE_TEXT[f.stareText]}` : ''}
              </small>
            </div>
            <a className="tv-pastila" href={urlApi(`/fisiere/${f.id}`)} target="_blank" rel="noreferrer">Deschide</a>
            <button type="button" className="tv-pastila tv-pastila-rosu" onClick={() => sterge(f)}>Șterge</button>
            <PrevizualizareSalvat key={`${f.id}-${f.marimeOcteti}`} fisier={f} />
          </li>
        ))}
        {date.elemente.length === 0 && <p className="tv-gol">Lecția nu are fișiere.</p>}
      </ul>
      {incarcare && (
        <p className="tv-status">
          <b>Se încarcă „{incarcare.nume}”...</b>
          {incarcare.ramase > 1 && <span>Mai sunt {incarcare.ramase - 1} după el.</span>}
        </p>
      )}
      <ProgresIncarcare progres={upload.progres} onAnuleaza={upload.anuleaza} />
      {erori.map((e) => <p key={e} className="tv-eroare">{e}</p>)}
      <label className="tv-eticheta-camp">
        <span>Adaugă sau înlocuiește fișiere</span>
        <input className="tv-fisier" type="file" multiple accept={TIPURI_FISIER} onChange={adauga} disabled={!!incarcare} />
      </label>
      {selectate.map((f, i) => {
        const pregatit = documente.items[i]?.fisier
        return <div key={`${f.name}-${i}`}>
          {pregatit ? <PrevizualizareFisier fisier={pregatit} /> : <p role="status">Se pregătește „{f.name}”…</p>}
          <button type="button" className="tv-pastila" disabled={!!incarcare} onClick={() => setSelectate(lista => lista.filter((_, j) => j !== i))}>Elimină din selecție</button>
        </div>
      })}
      {selectate.length > 0 && <button type="button" className="tv-btn" disabled={!!incarcare || documente.inLucru} onClick={trimiteSelectate}>Încarcă fișierele selectate</button>}
      {fereastra}
    </div>
  )
}

const INTREBARE_GOALA = { text: '', explicatie: '', variante: [0, 1, 2, 3].map(() => ({ text: '', corecta: false })) }

// O întrebare grilă scrisă de mână (2–6 variante, exact una corectă).
function IntrebareNoua({ parcursId, onAdaugata }) {
  const [deschis, setDeschis] = useState(false)
  const [date, setDate] = useState(INTREBARE_GOALA)
  const [eroare, setEroare] = useState('')

  async function trimite(e) {
    e.preventDefault()
    const variante = date.variante.filter((v) => v.text.trim())
    try {
      await api(`/parcursuri/${parcursId}/intrebari`, { metoda: 'POST', corp: { text: date.text, explicatie: date.explicatie || null, activa: true, variante, tip: 'grila' } })
      setDate(INTREBARE_GOALA)
      setDeschis(false)
      setEroare('')
      onAdaugata()
    } catch (err) {
      setEroare(err.message)
    }
  }

  if (!deschis) return <button className="tv-pastila" style={{ justifySelf: 'start' }} onClick={() => setDeschis(true)}>+ Scrie o întrebare</button>
  return (
    <form className="tv-cutie tv-form" onSubmit={trimite}>
      <textarea className="tv-camp-text" rows={2} required maxLength={1000} placeholder="Întrebarea" value={date.text} onChange={(e) => setDate({ ...date, text: e.target.value })} aria-label="Întrebarea" />
      {date.variante.map((v, i) => (
        <label key={i} className="tv-varianta-editor">
          <input type="radio" name="corecta-noua" checked={v.corecta} onChange={() => setDate({ ...date, variante: date.variante.map((x, j) => ({ ...x, corecta: j === i })) })} aria-label={`Varianta ${i + 1} e corectă`} />
          <input className="tv-camp-text" maxLength={500} placeholder={`Varianta ${i + 1}`} value={v.text} onChange={(e) => setDate({ ...date, variante: date.variante.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)) })} aria-label={`Varianta ${i + 1}`} />
        </label>
      ))}
      <small className="tv-indiciu">Bifează varianta corectă; variantele goale nu se salvează.</small>
      <textarea className="tv-camp-text" rows={2} maxLength={2000} placeholder="Explicația (opțional)" value={date.explicatie} onChange={(e) => setDate({ ...date, explicatie: e.target.value })} aria-label="Explicația" />
      {eroare && <p className="tv-eroare">{eroare}</p>}
      <div className="tv-grid-2">
        <button type="button" className="tv-btn tv-gri" onClick={() => setDeschis(false)}>Renunț</button>
        <button className="tv-btn">Adaugă întrebarea</button>
      </div>
    </form>
  )
}

// Testul lecției: întrebările generate cu AI la cerere (câte alege profesorul, adăugate la cele existente) sau scrise de mână.
function Intrebari({ parcursId }) {
  const [generare, setGenerare] = useState(null)
  const [intrebari, setIntrebari] = useState(null)
  const [numar, setNumar] = useState(5)
  const [eroare, setEroare] = useState('')
  const [buget, setBuget] = useState(null) // { ramase, peZi }: întrebări AI care se mai pot cere în ultimele 24 de ore

  const incarca = useCallback(() => {
    api('/continut/buget-ai').then(setBuget).catch(() => {})
    return api(`/continut/parcursuri/${parcursId}/intrebari`).then((r) => setIntrebari(r.elemente))
  }, [parcursId])

  // Urmărește generarea cât timp e în lucru; la final reîncarcă lista, ca întrebările noi să apară singure.
  const urmareste = useCallback(() => {
    let oprit = false
    let t
    const verifica = async () => {
      const g = await api(`/parcursuri/${parcursId}/generare`).catch(() => null)
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
  }, [parcursId, incarca])
  const [cerere, setCerere] = useState(0)
  useEffect(() => urmareste(), [urmareste, cerere])

  async function genereaza() {
    setEroare('')
    try {
      setGenerare(await api(`/parcursuri/${parcursId}/genereaza-intrebari`, { metoda: 'POST', corp: { numar: Number(numar) } }))
      setCerere((c) => c + 1)
    } catch (err) {
      setEroare(err.message)
      api('/continut/buget-ai').then(setBuget).catch(() => {}) // refuzat (ex. peste limită): arată ce a rămas de fapt
    }
  }

  const lucreaza = generare && ['in_asteptare', 'in_lucru'].includes(generare.stare)
  const maxim = Math.min(20, buget?.ramase ?? 20)
  const active = intrebari?.filter((q) => q.activa && q.tip === 'grila').length ?? 0
  return (
    <div className="tv-form">
      <h3 style={{ fontSize: 21, color: 'var(--navy)' }}>Testul lecției</h3>
      <p className="tv-indiciu">
        Elevii termină lecția când trec testul (80% răspunsuri corecte), cu întrebări alese la întâmplare dintre cele active de mai jos.
        {intrebari && ` Acum: ${active} ${active === 1 ? 'întrebare grilă activă' : 'întrebări grilă active'}.`}
      </p>
      <div className="tv-butoane" style={{ alignItems: 'center' }}>
        <label className="tv-eticheta-camp" style={{ maxWidth: 140 }}>
          <span>Câte întrebări</span>
          <input className="tv-camp-text" type="number" min={1} max={maxim} value={numar} onChange={(e) => setNumar(e.target.value)} />
        </label>
        <button className="tv-pastila tv-pastila-aur" onClick={genereaza} disabled={lucreaza || maxim === 0 || !(numar >= 1 && numar <= maxim)}>Generează întrebări cu AI</button>
      </div>
      <small className="tv-indiciu">
        Se generează din notițele tuturor capitolelor și textul fișierelor; se adaugă la întrebările existente.
        {buget && (buget.ramase > 0
          ? ` Îți mai rămân ${buget.ramase} ${buget.ramase === 1 ? 'întrebare AI' : 'întrebări AI'} în ultimele 24 de ore (cel mult 20 odată).`
          : ' Ai folosit întrebările AI disponibile în ultimele 24 de ore; poți scrie întrebări de mână.')}
      </small>
      {lucreaza && <p className="tv-status"><b>Se generează întrebările...</b><span>Durează de obicei mai puțin de un minut.</span></p>}
      {generare?.stare === 'gata' && (
        <p className="tv-succes">
          {generare.mesaj ?? `Au fost adăugate ${generare.intrebariGenerate} întrebări noi.`} Verifică-le mai jos.
        </p>
      )}
      {generare?.stare === 'eroare' && <p className="tv-eroare">{generare.mesaj}</p>}
      {eroare && <p className="tv-eroare">{eroare}</p>}
      <IntrebareNoua parcursId={parcursId} onAdaugata={incarca} />
      {intrebari?.length === 0 && !lucreaza && <p className="tv-gol">Lecția nu are încă întrebări.</p>}
      <ul className="tv-lista">{intrebari?.map((q) => <Intrebare key={q.id} q={q} onSalvat={incarca} onStearsa={incarca} />)}</ul>
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

  // Lecțiile pe care le poate edita, câte o pagină; căutarea și filtrele le aplică serverul.
  const marimeLectii = esteAdmin ? LECTII_PE_PAGINA_EDITOR_ADMIN : LECTII_PE_PAGINA_EDITOR_PROFESOR
  const [paginaLectii, setPaginaLectii] = useState(1)
  const [totalLectii, setTotalLectii] = useState(0)
  const [toateMateriile, setToateMateriile] = useState([])
  const [filtre, setFiltre] = useState({ clasa: '', vizibilitate: '', ciorne: false })
  const [textLectii, setTextLectii] = useState('')
  const [cautatLectii, setCautatLectii] = useState('')
  useEffect(() => {
    const t = setTimeout(() => {
      if (textLectii.trim() === cautatLectii) return
      setCautatLectii(textLectii.trim())
      setPaginaLectii(1)
    }, 300)
    return () => clearTimeout(t)
  }, [textLectii, cautatLectii])
  const schimbaFiltru = (modificare) => { setFiltre((f) => ({ ...f, ...modificare })); setPaginaLectii(1) }
  const incarcaLectii = useCallback(() => {
    const q = new URLSearchParams()
    if (filtruMaterie) q.set('materieId', filtruMaterie)
    if (filtre.clasa) q.set('clasa', filtre.clasa)
    if (filtre.vizibilitate) q.set('vizibilitate', filtre.vizibilitate)
    if (filtre.ciorne) q.set('publicat', 'false')
    if (cautatLectii) q.set('q', cautatLectii)
    return api(cuPagina(`/continut/parcursuri?${q}`, paginaLectii, marimeLectii)).then((r) => { setLectii(r.elemente); setTotalLectii(r.total) })
  }, [filtruMaterie, filtre, cautatLectii, paginaLectii, marimeLectii])
  const lectiiFiltrate = !!(filtruMaterie || filtre.clasa || filtre.vizibilitate || filtre.ciorne || cautatLectii)

  useEffect(() => {
    if (!utilizator) return
    materiiPermise().then(setMaterii)
    api('/materii').then((r) => setToateMateriile(r.elemente)).catch(() => {})
    api('/continut/organizatii').then((r) => setOrganizatii(r.elemente)).catch(() => setOrganizatii([]))
  }, [utilizator, materiiPermise])
  useEffect(() => {
    if (utilizator) incarcaLectii()
  }, [utilizator, incarcaLectii])

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
  // Materiile din filtru: cele pe care le poate folosi plus toate celelalte (o lecție a lui poate fi la o materie pe care n-o mai predă).
  const materiiFiltru = [...new Map([...materii, ...toateMateriile].map((m) => [m.id, m])).values()]
  const vizibile = lectii

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Conținut</span>
          <h1>Lecții, capitole și întrebări</h1>
          <p>O lecție e o hartă pentru elevi: fiecare capitol e o insulă, cu notițe și un video opțional; lecția are fișiere și un test.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-editor">
            <aside className="tv-cutie">
              <label className="tv-eticheta-camp">
                <span>Materia</span>
                <select className="tv-camp-text" value={filtruMaterie} onChange={(e) => { setFiltruMaterie(e.target.value); setPaginaLectii(1) }}>
                  <option value="">Toate materiile</option>
                  {materiiFiltru.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
                </select>
              </label>
              <label className="tv-eticheta-camp">
                <span>Caută</span>
                <input type="search" className="tv-camp-text" aria-label="Caută o lecție" placeholder="Titlu sau descriere" maxLength={100}
                  value={textLectii} onChange={(e) => setTextLectii(e.target.value)} />
              </label>
              <div className="tv-grid-2">
                <label className="tv-eticheta-camp">
                  <span>Clasa</span>
                  <select className="tv-camp-text" value={filtre.clasa} onChange={(e) => schimbaFiltru({ clasa: e.target.value })}>
                    <option value="">Toate</option>
                    {CLASE.map((c) => <option key={c} value={c}>{numeClasa(c)}</option>)}
                  </select>
                </label>
                <label className="tv-eticheta-camp">
                  <span>Cine o vede</span>
                  <select className="tv-camp-text" value={filtre.vizibilitate} onChange={(e) => schimbaFiltru({ vizibilitate: e.target.value })}>
                    <option value="">Oricine</option>
                    <option value="public">Pentru toți</option>
                    <option value="organizatie">Doar organizația</option>
                  </select>
                </label>
              </div>
              <label className="tv-optiune"><input type="checkbox" checked={filtre.ciorne} onChange={(e) => schimbaFiltru({ ciorne: e.target.checked })} /> Doar ciornele</label>
              {esteAdmin && <MaterieNoua onCreata={async (m) => { setMaterii(await materiiPermise()); setFiltruMaterie(String(m.id)); setPaginaLectii(1) }} />}
              <button className="tv-btn" onClick={() => deschideLectie('noua')} disabled={materii.length === 0}>+ Lecție nouă</button>
              {materii.length === 0 && <p className="tv-indiciu">Adaugă în profilul de profesor materiile pe care le predai, ca să poți crea lecții.</p>}
              <ul className="tv-lista">
                {vizibile.map((l) => (
                  <li key={l.id}>
                    <button className={`tv-rand tv-rand-buton ${lectie?.id === l.id ? 'tv-rand-activ' : ''}`} onClick={() => deschideLectie(l)}>
                      <div>
                        {/* Etichetele stau pe rândul titlului, ca rândul să rămână lat și scund. */}
                        <span className="tv-rand-titlu">
                          <b>{l.titlu}</b>
                          {l.vizibilitate === 'organizatie' && <span className="tv-pastila tv-pastila-mica">Organizație</span>}
                          {!l.publicat && <span className="tv-pastila tv-pastila-mica">Ciornă</span>}
                        </span>
                        <small>
                          {l.materie} · {numeClasa(l.clasa)} · {l.organizatie ?? 'platformă'}
                          {l.autorId !== utilizator.id && l.autor ? ` · de ${l.autor}` : ''}
                        </small>
                      </div>
                    </button>
                  </li>
                ))}
                {vizibile.length === 0 && <p className="tv-indiciu">{lectiiFiltrate ? 'Nicio lecție nu se potrivește căutării și filtrelor.' : 'Nu ai încă lecții aici.'}</p>}
              </ul>
              <Paginare compact pagina={paginaLectii} totalPagini={Math.ceil(totalLectii / marimeLectii)} onAlege={setPaginaLectii} />
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
              {lectie?.id && capitol === null && (
                <>
                  <hr className="tv-linie" />
                  <Fisiere key={`f-${lectie.id}`} parcursId={lectie.id} />
                  <hr className="tv-linie" />
                  <Intrebari key={`q-${lectie.id}`} parcursId={lectie.id} />
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
                </>
              )}
            </section>
          </div>
        </div>
      </div>
    </main>
  )
}
