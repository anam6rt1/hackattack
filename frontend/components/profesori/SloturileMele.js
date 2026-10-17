'use client'
// Ședințele profesorului: programează una nouă (plătită la tariful fix al platformei sau pro bono)
// și vezi cine s-a înscris la fiecare.

import { useState } from 'react'
import { api, dataOra } from '@/lib/api'

// Orele din lista de alegere: din 30 în 30 de minute, 07:00–21:30
const ORE = Array.from({ length: 30 }, (_, i) => `${String(7 + Math.floor(i / 2)).padStart(2, '0')}:${i % 2 ? '30' : '00'}`)
const ziLocala = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

function SlotNou({ panou, onCreat }) {
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)
  const pret = panou.pret
  const azi = ziLocala(new Date())

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
          inceputLa: inceput.toISOString(), capacitate: Number(f.capacitate), proBono: f.proBono === 'da',
        },
      })
      e.target.reset()
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

export default function SloturileMele({ panou, onActualizat }) {
  const [anulare, setAnulare] = useState(null)
  const acum = new Date()
  const viitoare = panou.sloturi.filter((s) => s.slot.status === 'deschisa' && new Date(s.slot.inceputLa) > acum)
  const trecute = panou.sloturi.filter((s) => !viitoare.includes(s))

  async function anuleaza(id) {
    await api(`/profesori/me/sloturi/${id}`, { metoda: 'DELETE' })
    setAnulare(null)
    onActualizat(await api('/profesori/me'))
  }

  const Rand = ({ s, poateAnula }) => (
    <li className="tv-rand">
      <div>
        <b>{s.slot.titlu}</b>
        <small>
          {dataOra(s.slot.inceputLa)} · {s.slot.materie} · {s.slot.capacitate - s.slot.locuriLibere}/{s.slot.capacitate} înscriși
          {s.elevi.filter((e) => e.status === 'confirmata').length > 0 && ` · ${s.elevi.filter((e) => e.status === 'confirmata').map((e) => e.nume).join(', ')}`}
        </small>
      </div>
      {s.slot.proBono && <span className="tv-pastila tv-pastila-verde">Pro bono</span>}
      {s.slot.status === 'anulata' && <span className="tv-pastila tv-pastila-rosu">Anulată</span>}
      {poateAnula &&
        (anulare === s.slot.id ? (
          <span className="tv-butoane">
            <button className="tv-pastila tv-pastila-rosu" onClick={() => anuleaza(s.slot.id)}>Confirm anularea</button>
            <button className="tv-pastila" onClick={() => setAnulare(null)}>Renunț</button>
          </span>
        ) : (
          <button className="tv-pastila" onClick={() => setAnulare(s.slot.id)}>Anulează</button>
        ))}
    </li>
  )

  return (
    <div className="tv-panou">
      <div className="tv-cutie">
        <h3>Programează o ședință</h3>
        <SlotNou panou={panou} onCreat={onActualizat} />
      </div>
      <div className="tv-cutie tv-panou-lat">
        <h3>Ședințe viitoare</h3>
        {viitoare.length ? <ul className="tv-lista">{viitoare.map((s) => <Rand key={s.slot.id} s={s} poateAnula />)}</ul> : <p className="tv-gol">Nicio ședință programată.</p>}
        {trecute.length > 0 && (
          <>
            <h3 style={{ marginTop: 10 }}>Istoric</h3>
            <ul className="tv-lista">{trecute.slice(0, 10).map((s) => <Rand key={s.slot.id} s={s} />)}</ul>
          </>
        )}
      </div>
    </div>
  )
}
