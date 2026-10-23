'use client'
// Județul și localitatea, din registrul oficial SIRUTA: alegi județul, apoi cauți localitatea (fără diacritice merge și „barabant”).
// Valorile pleacă și prin câmpuri ascunse (judetId, localitateId), ca formularele trimise cu FormData să le includă.

import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

export default function AlegeLoc({ judetInitial = null, localitateInitiala = null, obligatoriu = false, onChange }) {
  const [judete, setJudete] = useState([])
  const [judet, setJudet] = useState(judetInitial ? String(judetInitial) : '')
  const [loc, setLoc] = useState(localitateInitiala) // { id, nume, uatNume }
  const [text, setText] = useState(localitateInitiala?.nume ?? '')
  const [rezultate, setRezultate] = useState([])
  const [deschis, setDeschis] = useState(false)

  useEffect(() => {
    api('/judete').then((r) => setJudete(r.elemente)).catch(() => {})
  }, [])

  // Caută în localitățile județului, la o scurtă pauză după ultima tastă.
  useEffect(() => {
    if (!judet || !deschis) return
    const t = setTimeout(() => {
      api(`/judete/${judet}/localitati?q=${encodeURIComponent(text)}`).then((r) => setRezultate(r.elemente)).catch(() => {})
    }, 250)
    return () => clearTimeout(t)
  }, [judet, text, deschis])

  function alegeJudet(valoare) {
    setJudet(valoare)
    setLoc(null)
    setText('')
    setRezultate([])
    onChange?.({ judetId: valoare ? Number(valoare) : null, localitateId: null })
  }

  function alegeLocalitate(l) {
    setLoc(l)
    setText(l.nume)
    setDeschis(false)
    onChange?.({ judetId: Number(judet), localitateId: l.id })
  }

  return (
    <div className="tv-grid-2">
      <label className="tv-eticheta-camp">
        <span>Județul{obligatoriu ? '' : ' (opțional)'}</span>
        <select className="tv-camp-text" value={judet} onChange={(e) => alegeJudet(e.target.value)} required={obligatoriu}>
          <option value="">Alege județul</option>
          {judete.map((j) => <option key={j.id} value={j.id}>{j.nume}</option>)}
        </select>
      </label>
      <label className="tv-eticheta-camp tv-cautare-loc">
        <span>Localitatea (opțional)</span>
        <input className="tv-camp-text" value={text} disabled={!judet} placeholder={judet ? 'Scrie primele litere' : 'Alege întâi județul'}
          onChange={(e) => {
            setText(e.target.value)
            // Retyped after choosing one: no locality is chosen any more (a filter must know it too).
            if (loc) onChange?.({ judetId: Number(judet), localitateId: null })
            setLoc(null)
            setDeschis(true)
          }} onFocus={() => setDeschis(true)}
          aria-autocomplete="list" autoComplete="off" />
        {deschis && judet && !loc && rezultate.length > 0 && (
          <ul className="tv-rezultate-loc" role="listbox">
            {rezultate.map((l) => (
              <li key={l.id}>
                <button type="button" onClick={() => alegeLocalitate(l)}>
                  {l.nume} <small>{l.uatNume}</small>
                </button>
              </li>
            ))}
          </ul>
        )}
      </label>
      <input type="hidden" name="judetId" value={judet} />
      <input type="hidden" name="localitateId" value={loc?.id ?? ''} />
    </div>
  )
}
