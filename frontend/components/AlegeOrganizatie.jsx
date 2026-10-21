'use client'
// Organizațiile aprobate (școli, centre). Elevii aleg cel mult una; profesorii aleg una sau mai multe.
// Dacă e ales un județ, apar întâi organizațiile din el; „Toate județele” le arată pe toate.

import { useEffect, useId, useState } from 'react'
import { api } from '@/lib/api'

export default function AlegeOrganizatie({ judetId = null, multiplu = false, obligatoriu = false }) {
  const [toate, setToate] = useState(false)
  const [organizatii, setOrganizatii] = useState(null)
  const idTitlu = useId()

  useEffect(() => {
    api('/organizatii' + (judetId && !toate ? `?judetId=${judetId}` : '')).then((r) => setOrganizatii(r.elemente)).catch(() => setOrganizatii([]))
  }, [judetId, toate])

  const titlu = multiplu ? 'Organizațiile în care predai' : `Organizația ta${obligatoriu ? '' : ' (opțional)'}`
  return (
    <fieldset className="tv-eticheta-camp" aria-labelledby={idTitlu}>
      <span id={idTitlu}>{titlu}</span>
      {organizatii === null ? (
        <small className="tv-indiciu">Se încarcă...</small>
      ) : organizatii.length === 0 ? (
        <small className="tv-indiciu">Nicio organizație aprobată {judetId && !toate ? 'în acest județ' : 'încă'}.</small>
      ) : multiplu ? (
        <div className="tv-optiuni">
          {organizatii.map((o) => (
            <label key={o.id} className="tv-bifa">
              <input type="checkbox" name="organizatieIds" value={o.id} /> {o.nume} <small>· {o.localitate ?? o.judet}</small>
            </label>
          ))}
        </div>
      ) : (
        <select className="tv-camp-text" name="organizatieId" required={obligatoriu} defaultValue="" aria-labelledby={idTitlu}>
          <option value="">{obligatoriu ? 'Alege organizația' : 'Fără organizație'}</option>
          {organizatii.map((o) => <option key={o.id} value={o.id}>{o.nume} · {o.localitate ?? o.judet}</option>)}
        </select>
      )}
      {judetId && <button type="button" className="tv-scoate" onClick={() => setToate(!toate)}>{toate ? 'Doar județul ales' : 'Toate județele'}</button>}
      <small className="tv-indiciu">
        {multiplu ? 'Administratorul fiecărei organizații îți aprobă cererea.' : 'Un profesor sau administratorul organizației îți confirmă cererea; până atunci vezi lecțiile publice.'}
      </small>
    </fieldset>
  )
}
