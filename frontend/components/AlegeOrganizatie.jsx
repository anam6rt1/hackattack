'use client'
// Organizațiile aprobate (școli, centre), căutate pe server după nume, județ sau localitate (fără diacritice merge și „barabant”).
// Elevii aleg cel mult una, dintr-o listă cu căutare (AlegeOrganizatie); profesorii aleg până la ORGANIZATII_PROFESOR, dintr-o
// fereastră cu bife (AlegeOrganizatii). Dacă e ales un județ, apar întâi organizațiile din el; „Toate județele” le arată pe toate.

import { useEffect, useId, useRef, useState } from 'react'
import { api } from '@/lib/api'
import { ORGANIZATII_PROFESOR, urlOrganizatii } from '@/lib/alegeri'
import AlegeMultiplu from './AlegeMultiplu'

const element = (o) => ({ id: o.id, nume: o.nume, detaliu: o.localitate ?? o.judet })

export default function AlegeOrganizatie({ judetId = null, obligatoriu = false }) {
  const [toate, setToate] = useState(false)
  const [text, setText] = useState('')
  const [ales, setAles] = useState(null) // { id, nume, detaliu }
  const [deschis, setDeschis] = useState(false)
  const [rezultat, setRezultat] = useState(null) // { elemente, total }
  const camp = useRef(null)
  const idTitlu = useId()
  const idLista = useId()
  const judet = judetId && !toate ? judetId : null
  const cautare = ales ? '' : text

  useEffect(() => {
    let activ = true
    const t = setTimeout(() => {
      api(urlOrganizatii(cautare, judet)).then((r) => activ && setRezultat(r)).catch(() => activ && setRezultat({ elemente: [], total: 0 }))
    }, cautare ? 250 : 0)
    return () => { activ = false; clearTimeout(t) }
  }, [cautare, judet])

  // Textul scris fără o organizație aleasă din listă nu e o alegere: formularul cere una (dacă e obligatorie) sau nimic scris.
  useEffect(() => {
    camp.current?.setCustomValidity(ales || (!obligatoriu && !text.trim()) ? '' : 'Alege organizația din listă.')
  }, [ales, text, obligatoriu])

  function alege(o) {
    setAles(o)
    setText(o ? o.nume : '')
    setDeschis(false)
  }

  const elemente = rezultat?.elemente ?? []
  const fara = rezultat !== null && elemente.length === 0 && !cautare
  return (
    <fieldset className="tv-eticheta-camp" aria-labelledby={idTitlu}>
      <span id={idTitlu}>Organizația ta{obligatoriu ? '' : ' (opțional)'}</span>
      {fara ? (
        <small className="tv-indiciu">Nicio organizație aprobată {judet ? 'în acest județ' : 'încă'}.</small>
      ) : (
        <div className="tv-cautare-loc">
          <input ref={camp} className="tv-camp-text" type="search" role="combobox" aria-expanded={deschis} aria-controls={idLista} aria-labelledby={idTitlu}
            aria-autocomplete="list" autoComplete="off" maxLength={100} value={text}
            placeholder={rezultat === null ? 'Se încarcă...' : 'Caută după nume sau localitate'}
            onChange={(e) => { setText(e.target.value); setAles(null); setDeschis(true) }}
            // Enter alege prima organizație găsită, în loc să trimită formularul cu textul neales.
            onKeyDown={(e) => { if (e.key === 'Enter' && deschis && !ales && elemente.length) { e.preventDefault(); alege(element(elemente[0])) } }}
            onFocus={() => setDeschis(true)} onBlur={() => setTimeout(() => setDeschis(false), 150)} />
          {deschis && rezultat !== null && (
            <ul id={idLista} className="tv-rezultate-loc" role="listbox" aria-labelledby={idTitlu}>
              {!obligatoriu && !cautare && (
                <li><button type="button" role="option" aria-selected={!ales} onMouseDown={(e) => e.preventDefault()} onClick={() => alege(null)}>Fără organizație</button></li>
              )}
              {elemente.length === 0 && <li className="tv-indiciu" style={{ padding: '8px 10px' }}>Nicio organizație nu se potrivește.</li>}
              {elemente.map((o) => (
                <li key={o.id}>
                  <button type="button" role="option" aria-selected={ales?.id === o.id} onMouseDown={(e) => e.preventDefault()} onClick={() => alege(element(o))}>
                    {o.nume} <small>· {o.localitate ?? o.judet}</small>
                  </button>
                </li>
              ))}
              {rezultat.total > elemente.length && (
                <li className="tv-indiciu" style={{ padding: '8px 10px' }}>Se văd primele {elemente.length} din {rezultat.total}. Scrie ca să le restrângi.</li>
              )}
            </ul>
          )}
        </div>
      )}
      <input type="hidden" name="organizatieId" value={ales?.id ?? ''} />
      {judetId && <button type="button" className="tv-scoate" onClick={() => setToate(!toate)}>{toate ? 'Doar județul ales' : 'Toate județele'}</button>}
      <small className="tv-indiciu">Un profesor sau administratorul organizației îți confirmă cererea; până atunci vezi lecțiile publice.</small>
    </fieldset>
  )
}

/** Profesorul: organizațiile în care predă, până la ORGANIZATII_PROFESOR, alese într-o fereastră cu căutare. */
export function AlegeOrganizatii({ judetId = null, alese, onChange }) {
  const [toate, setToate] = useState(false)
  const judet = judetId && !toate ? judetId : null
  return (
    <AlegeMultiplu eticheta="Organizațiile în care predai" buton="Adaugă organizație" titlu="Organizațiile în care predai"
      etichetaCautare="Caută după nume, județ sau localitate" maxim={ORGANIZATII_PROFESOR} alese={alese} onChange={onChange} name="organizatieIds"
      cauta={(q) => api(urlOrganizatii(q, judet)).then((r) => ({ elemente: r.elemente.map(element), total: r.total }))} cheieCautare={judet}
      indiciu="Administratorul fiecărei organizații îți aprobă cererea."
      inFereastra={judetId && (
        <button type="button" className="tv-scoate" style={{ marginLeft: 0, justifySelf: 'start' }} onClick={() => setToate(!toate)}>
          {toate ? 'Doar județul ales' : 'Toate județele'}
        </button>
      )} />
  )
}
