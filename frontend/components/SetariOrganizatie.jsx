'use client'
// Setări → „Organizație și loc”: județul și localitatea (pentru filtrele „aproape de mine”), clasa elevului
// și organizațiile: cererile trimise, ieșirea dintr-o organizație și cererea pentru alta.

import { useCallback, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import AlegeLoc from '@/components/AlegeLoc'
import AlegeOrganizatie from '@/components/AlegeOrganizatie'

const STARI = { in_asteptare: 'cerere în așteptare', aprobat: 'membru', respins: 'cerere respinsă', expirat: 'cerere expirată' }

export default function SetariOrganizatie({ utilizator, onSchimbat }) {
  const elev = utilizator.rol === 'elev'
  const [localitate, setLocalitate] = useState(utilizator.localitateId ? undefined : null)
  const [apartenente, setApartenente] = useState(null)
  const [mesajLoc, setMesajLoc] = useState(null)
  const [mesajOrg, setMesajOrg] = useState(null)

  useEffect(() => {
    if (utilizator.localitateId) api(`/localitati/${utilizator.localitateId}`).then(setLocalitate).catch(() => setLocalitate(null))
  }, [utilizator.localitateId])
  const incarca = useCallback(() => {
    api('/utilizatori/me/organizatii').then((r) => setApartenente(r.elemente)).catch(() => setApartenente([]))
  }, [])
  useEffect(() => incarca(), [incarca])

  async function salveazaLoc(e) {
    e.preventDefault()
    const d = Object.fromEntries(new FormData(e.currentTarget))
    try {
      await api('/utilizatori/me/setari/loc', {
        metoda: 'PUT', corp: { judetId: d.judetId ? Number(d.judetId) : null, localitateId: d.localitateId ? Number(d.localitateId) : null, clasa: d.clasa || null },
      })
      setMesajLoc({ ok: true, text: 'Datele au fost salvate.' })
      onSchimbat()
    } catch (err) {
      setMesajLoc({ ok: false, text: err.message })
    }
  }

  async function schimba(fn, ok) {
    setMesajOrg(null)
    try {
      setApartenente((await fn()).elemente)
      setMesajOrg({ ok: true, text: ok })
      onSchimbat()
    } catch (err) {
      setMesajOrg({ ok: false, text: err.message })
    }
  }

  function cere(e) {
    e.preventDefault()
    const id = new FormData(e.currentTarget).get('organizatieId')
    if (id) schimba(() => api('/utilizatori/me/organizatii', { metoda: 'POST', corp: { organizatieId: Number(id) } }),
      elev ? 'Cererea a fost trimisă. Un profesor sau administratorul organizației o va confirma.' : 'Cererea a fost trimisă administratorului organizației.')
  }

  const iese = (a) => window.confirm(a.stare === 'aprobat' ? `Ieși din „${a.organizatie}”?` : `Retragi cererea la „${a.organizatie}”?`)
    && schimba(() => api(`/utilizatori/me/organizatii/${a.organizatieId}`, { metoda: 'DELETE' }), 'Gata.')
  const administrata = utilizator.organizatieAdministrata?.id

  return (
    <div className="tv-form">
      {localitate !== undefined && (
        <form className="tv-form" onSubmit={salveazaLoc}>
          <h3>Unde înveți</h3>
          <AlegeLoc judetInitial={utilizator.judetId} localitateInitiala={localitate} />
          {elev && (
            <label className="tv-eticheta-camp">
              <span>Clasa</span>
              <select className="tv-camp-text" name="clasa" defaultValue={utilizator.clasa ?? ''}>
                <option value="">Nu spun</option>
                {['IX', 'X', 'XI', 'XII'].map((c) => <option key={c} value={c}>a {c}-a</option>)}
              </select>
            </label>
          )}
          <p className="tv-indiciu">Județul și localitatea te ajută să găsești profesori și lecții din apropiere.</p>
          <button className="tv-btn">Salvează</button>
          {mesajLoc && <p className={mesajLoc.ok ? 'tv-succes' : 'tv-eroare'}>{mesajLoc.text}</p>}
        </form>
      )}

      <h3>Organizații</h3>
      {!apartenente ? (
        <p className="tv-asteapta">Se încarcă...</p>
      ) : apartenente.length === 0 ? (
        <p className="tv-indiciu">Nu faci parte din nicio organizație.</p>
      ) : (
        <ul className="tv-lista-simpla">
          {apartenente.map((a) => (
            <li key={a.organizatieId}>
              <b>{a.organizatie}</b> · {STARI[a.stare] ?? a.stare}
              {a.motivRespingere && <small> · Motiv: {a.motivRespingere}</small>}
              {a.organizatieId !== administrata && (a.stare === 'aprobat' || a.stare === 'in_asteptare') && (
                <button type="button" className="tv-scoate" onClick={() => iese(a)}>{a.stare === 'aprobat' ? 'Ieși' : 'Retrage cererea'}</button>
              )}
            </li>
          ))}
        </ul>
      )}
      {(elev ? !apartenente?.some((a) => a.stare === 'aprobat' || a.stare === 'in_asteptare') : true) && (
        <form className="tv-form" onSubmit={cere}>
          <AlegeOrganizatie judetId={utilizator.judetId} obligatoriu />
          <button className="tv-btn tv-aur">Cere să intri</button>
        </form>
      )}
      {elev && <p className="tv-indiciu">Un elev poate face parte dintr-o singură organizație; cererea expiră după 7 zile dacă nu o confirmă nimeni.</p>}
      {mesajOrg && <p className={mesajOrg.ok ? 'tv-succes' : 'tv-eroare'}>{mesajOrg.text}</p>}
    </div>
  )
}
