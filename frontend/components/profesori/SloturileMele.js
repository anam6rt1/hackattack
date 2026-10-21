'use client'
// Ședințele profesorului: programează una nouă (plătită la tariful fix al platformei sau pro bono; online, față în față
// sau hibrid) și vezi cine s-a înscris la fiecare și cum participă.

import { useEffect, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import { PARTICIPARE, TIPURI_SEDINTA, textLoc } from '@/lib/sedinte'

// Orele din lista de alegere: din 30 în 30 de minute, 07:00–21:30
const ORE = Array.from({ length: 30 }, (_, i) => `${String(7 + Math.floor(i / 2)).padStart(2, '0')}:${i % 2 ? '30' : '00'}`)
const ziLocala = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

function SlotNou({ panou, onCreat }) {
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)
  const [tip, setTip] = useState('online')
  const [locuri, setLocuri] = useState(null) // locul propriu + organizațiile profesorului
  const pret = panou.pret
  const azi = ziLocala(new Date())

  useEffect(() => {
    api('/profesori/me/loc').then(setLocuri).catch(() => setLocuri(null))
  }, [])

  async function trimiteFormular(e) {
    e.preventDefault()
    const f = Object.fromEntries(new FormData(e.currentTarget))
    // data + ora alese sunt în fusul orar al profesorului; API-ul primește momentul în UTC
    const inceput = new Date(`${f.data}T${f.ora}:00`)
    if (Number.isNaN(inceput.getTime())) return setEroare('Alege data și ora ședinței.')
    if (inceput.getTime() < Date.now() + 30 * 60 * 1000) return setEroare('Ședința trebuie programată cu cel puțin 30 de minute înainte.')
    setTrimite(true)
    setEroare('')
    try {
      const nou = await api('/profesori/me/sloturi', {
        metoda: 'POST',
        corp: {
          materieId: Number(f.materieId), titlu: f.titlu, descriere: f.descriere || null,
          inceputLa: inceput.toISOString(), capacitate: Number(f.capacitate), proBono: f.proBono === 'da', linkIntalnire: f.linkIntalnire || null,
          tip, locOrganizatieId: tip !== 'online' && f.loc ? Number(f.loc) : null, notaLoc: tip !== 'online' ? f.notaLoc || null : null,
        },
      })
      e.target.reset()
      setTip('online')
      onCreat(nou)
    } catch (err) {
      setEroare(err.message)
    } finally {
      setTrimite(false)
    }
  }

  return (
    <form className="tv-form" onSubmit={trimiteFormular}>
      <label className="tv-eticheta-camp">
        <span>Titlu</span>
        <input className="tv-camp-text" name="titlu" required maxLength={200} placeholder="ex. Recapitulare: Moara cu noroc" />
      </label>
      <label className="tv-eticheta-camp">
        <span>Materie</span>
        <select className="tv-camp-text" name="materieId" required>
          {panou.profil.materii.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
        </select>
      </label>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp">
          <span>Data</span>
          <input className="tv-camp-text" name="data" type="date" min={azi} defaultValue={azi} required />
        </label>
        <label className="tv-eticheta-camp">
          <span>Ora</span>
          <select className="tv-camp-text" name="ora" defaultValue="17:00" required>
            {ORE.map((o) => <option key={o} value={o}>{o}</option>)}
          </select>
        </label>
      </div>
      <div className="tv-grid-2">
        <label className="tv-eticheta-camp">
          <span>Locuri</span>
          <input className="tv-camp-text" name="capacitate" type="number" min={1} max={30} defaultValue={1} required />
        </label>
        <fieldset className="tv-eticheta-camp">
          <span>Tarif</span>
          <div className="tv-optiuni">
            <label className="tv-optiune"><input type="radio" name="proBono" value="nu" defaultChecked /> {pret ? `${pret.suma} ${pret.moneda}` : 'Plătită'}</label>
            <label className="tv-optiune"><input type="radio" name="proBono" value="da" /> Pro bono</label>
          </div>
        </fieldset>
      </div>
      <label className="tv-eticheta-camp">
        <span>Descriere (opțional)</span>
        <textarea className="tv-camp-text" name="descriere" rows={2} maxLength={1000} />
      </label>
      <fieldset className="tv-eticheta-camp">
        <span>Cum se ține</span>
        <div className="tv-optiuni">
          {Object.entries(TIPURI_SEDINTA).map(([v, t]) => (
            <label key={v} className="tv-optiune">
              <input type="radio" name="tip" value={v} checked={tip === v} onChange={() => setTip(v)} /> {t}
            </label>
          ))}
        </div>
      </fieldset>
      {tip === 'hibrid' && <p className="tv-indiciu">La o ședință hibridă, fiecare elev alege la rezervare dacă vine la sală sau participă online.</p>}
      {tip !== 'online' && (
        <>
          <label className="tv-eticheta-camp">
            <span>Locul</span>
            <select className="tv-camp-text" name="loc" defaultValue="">
              {(locuri?.optiuni ?? [{ organizatieId: null, eticheta: 'Locul meu' }]).map((o) => (
                <option key={o.organizatieId ?? ''} value={o.organizatieId ?? ''}>
                  {o.organizatieId ? `${o.eticheta} (${textLoc({ ...o.loc, organizatie: null })})` : o.loc ? `${o.eticheta}: ${textLoc(o.loc)}` : o.eticheta}
                </option>
              ))}
            </select>
          </label>
          <label className="tv-eticheta-camp">
            <span>Detalii despre loc (opțional, de ex. „Sala 12, etajul 2”)</span>
            <input className="tv-camp-text" name="notaLoc" maxLength={200} />
          </label>
        </>
      )}
      {tip !== 'fata_in_fata' && (
        <label className="tv-eticheta-camp">
          <span>Link ședință (opțional, îl poți adăuga și mai târziu)</span>
          <input className="tv-camp-text" name="linkIntalnire" type="url" maxLength={500} placeholder="https://meet.google.com/..." />
        </label>
      )}
      <p className="tv-indiciu">
        {tip === 'online' ? 'Linkul îl văd' : tip === 'hibrid' ? 'Linkul și detaliile locului le văd' : 'Detaliile locului le văd'} doar elevii care și-au rezervat un loc
        {tip === 'online' ? '.' : '; localitatea și județul le vede oricine.'}
      </p>
      {pret && (
        <p className="tv-indiciu">
          Elevul plătește tariful fix al platformei ({pret.suma} {pret.moneda} / {pret.durataMinute} min). Tu primești onorariul negociat:{' '}
          <b>{panou.onorariuNegociat} {panou.moneda}</b> pe elev.
        </p>
      )}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      <button className="tv-btn" disabled={trimite}>{trimite ? 'Se programează...' : 'Programează ședința'}</button>
    </form>
  )
}

// Adaugă sau schimbă detaliile locului („Sala 12”) direct din listă
function NotaLoc({ slot, onActualizat }) {
  const [editare, setEditare] = useState(false)
  const [nota, setNota] = useState(slot.notaLoc ?? '')
  const [eroare, setEroare] = useState('')

  async function salveaza(e) {
    e.preventDefault()
    setEroare('')
    try {
      onActualizat(await api(`/profesori/me/sloturi/${slot.id}/nota`, { metoda: 'PUT', corp: { notaLoc: nota.trim() || null } }))
      setEditare(false)
    } catch (err) {
      setEroare(err.message)
    }
  }

  if (!editare) return <button className="tv-pastila" onClick={() => setEditare(true)}>{slot.notaLoc ? 'Schimbă detaliile locului' : 'Adaugă detalii loc'}</button>
  return (
    <form className="tv-link-editor" onSubmit={salveaza}>
      <input className="tv-camp-text" value={nota} maxLength={200} onChange={(e) => setNota(e.target.value)} placeholder="Sala 12, etajul 2" aria-label="Detalii despre loc" autoFocus />
      <button className="tv-pastila tv-pastila-aur">Salvează</button>
      <button type="button" className="tv-pastila" onClick={() => { setEditare(false); setNota(slot.notaLoc ?? ''); setEroare('') }}>Renunț</button>
      {eroare && <span className="tv-pastila tv-pastila-rosu" role="alert">{eroare}</span>}
    </form>
  )
}

// Adaugă sau schimbă linkul ședinței (Meet, Zoom, Teams...) direct din listă
function LinkSedinta({ slot, onActualizat }) {
  const [editare, setEditare] = useState(false)
  const [link, setLink] = useState(slot.linkIntalnire ?? '')
  const [eroare, setEroare] = useState('')

  async function salveaza(e) {
    e.preventDefault()
    setEroare('')
    try {
      onActualizat(await api(`/profesori/me/sloturi/${slot.id}/link`, { metoda: 'PUT', corp: { linkIntalnire: link.trim() || null } }))
      setEditare(false)
    } catch (err) {
      setEroare(err.message)
    }
  }

  if (!editare)
    return (
      <span className="tv-butoane">
        {slot.linkIntalnire && <a className="tv-pastila tv-pastila-verde" href={slot.linkIntalnire} target="_blank" rel="noreferrer">Deschide linkul</a>}
        <button className="tv-pastila" onClick={() => setEditare(true)}>{slot.linkIntalnire ? 'Schimbă linkul' : 'Adaugă link'}</button>
      </span>
    )
  return (
    <form className="tv-link-editor" onSubmit={salveaza}>
      <input className="tv-camp-text" type="url" value={link} onChange={(e) => setLink(e.target.value)} placeholder="https://..." aria-label="Linkul ședinței" autoFocus />
      <button className="tv-pastila tv-pastila-aur">Salvează</button>
      <button type="button" className="tv-pastila" onClick={() => { setEditare(false); setLink(slot.linkIntalnire ?? ''); setEroare('') }}>Renunț</button>
      {eroare && <span className="tv-pastila tv-pastila-rosu" role="alert">{eroare}</span>}
    </form>
  )
}

export default function SloturileMele({ panou, onActualizat }) {
  const [anulare, setAnulare] = useState(null)
  const [eroareAnulare, setEroareAnulare] = useState('')
  const acum = new Date()
  const viitoare = panou.sloturi.filter((s) => s.slot.status === 'deschisa' && new Date(s.slot.inceputLa) > acum)
  const trecute = panou.sloturi.filter((s) => !viitoare.includes(s))

  async function anuleaza(id) {
    setEroareAnulare('')
    try {
      await api(`/profesori/me/sloturi/${id}`, { metoda: 'DELETE' })
      setAnulare(null)
      onActualizat(await api('/profesori/me'))
    } catch (e) {
      setEroareAnulare(e.message)
    }
  }

  // funcție simplă (nu componentă), ca editorul de link să nu se remonteze la fiecare randare
  const rand = (s, poateAnula) => {
    const inscrisi = s.elevi.filter((e) => e.status === 'confirmata')
    return (
      <li key={s.slot.id} className="tv-rand tv-rand-sedinta">
        <div>
          <b>{s.slot.titlu}</b>
          <small>
            {dataOra(s.slot.inceputLa)} · {s.slot.materie} · {TIPURI_SEDINTA[s.slot.tip]}
            {s.slot.loc && ` · ${textLoc(s.slot.loc)}`}
            {s.slot.notaLoc && ` (${s.slot.notaLoc})`} · {s.slot.capacitate - s.slot.locuriLibere}/{s.slot.capacitate} înscriși
            {inscrisi.length > 0 && ` · ${inscrisi.map((e) => (s.slot.tip === 'hibrid' ? `${e.nume} (${PARTICIPARE[e.participare]})` : e.nume)).join(', ')}`}
          </small>
        </div>
        {s.slot.proBono && <span className="tv-pastila tv-pastila-verde">Pro bono</span>}
        {s.slot.status === 'anulata' && <span className="tv-pastila tv-pastila-rosu">Anulată</span>}
        {poateAnula && s.slot.tip !== 'fata_in_fata' && <LinkSedinta key={`${s.slot.id}-${s.slot.linkIntalnire ?? ''}`} slot={s.slot} onActualizat={onActualizat} />}
        {poateAnula && s.slot.tip !== 'online' && <NotaLoc key={`n-${s.slot.id}-${s.slot.notaLoc ?? ''}`} slot={s.slot} onActualizat={onActualizat} />}
        {poateAnula &&
          (anulare === s.slot.id ? (
            <span className="tv-butoane">
              <button className="tv-pastila tv-pastila-rosu" onClick={() => anuleaza(s.slot.id)}>Confirm anularea</button>
              <button className="tv-pastila" onClick={() => { setAnulare(null); setEroareAnulare('') }}>Renunț</button>
              {eroareAnulare && <span className="tv-pastila tv-pastila-rosu" role="alert">{eroareAnulare}</span>}
            </span>
          ) : (
            <button className="tv-pastila" onClick={() => setAnulare(s.slot.id)}>Anulează</button>
          ))}
      </li>
    )
  }

  return (
    <div className="tv-panou">
      <div className="tv-cutie">
        <h3>Programează o ședință</h3>
        <SlotNou panou={panou} onCreat={onActualizat} />
      </div>
      <div className="tv-cutie tv-panou-lat">
        <h3>Ședințe viitoare</h3>
        {viitoare.length ? <ul className="tv-lista">{viitoare.map((s) => rand(s, true))}</ul> : <p className="tv-gol">Nicio ședință programată.</p>}
        {trecute.length > 0 && (
          <>
            <h3 style={{ marginTop: 10 }}>Istoric</h3>
            <ul className="tv-lista">{trecute.slice(0, 10).map((s) => rand(s, false))}</ul>
          </>
        )}
      </div>
    </div>
  )
}
