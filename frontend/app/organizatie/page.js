'use client'
// /organizatie
// - Administratorul organizației: cererile profesorilor și elevilor, membrii, cine poate publica lecții public, datele organizației.
// - Profesorii aprobați: cererile elevilor din organizațiile în care predau (le pot aproba sau respinge).

import { useCallback, useEffect, useState } from 'react'
import { API, api, dataOra } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import AlegeLoc from '@/components/AlegeLoc'

const ROL = { profesor: 'Profesor', elev: 'Elev' }

// Un membru sau o cerere. cale = de unde se decide (organizația mea sau elevii unei organizații).
function Membru({ m, cale, admin, onSchimbat }) {
  const [respinge, setRespinge] = useState(false)
  const [motiv, setMotiv] = useState('')
  const [eroare, setEroare] = useState('')

  async function trimite(actiune, metoda = 'POST', corp) {
    setEroare('')
    try {
      await api(`${cale}/${m.utilizatorId}${actiune ? '/' + actiune : ''}`, { metoda, corp })
      onSchimbat()
    } catch (e) {
      setEroare(e.message)
    }
  }

  return (
    <article className="tv-cutie">
      <div className="tv-mic">{ROL[m.rol]}{m.clasa ? ` · clasa a ${m.clasa}-a` : ''} · cerere din {dataOra(m.cerutLa)}</div>
      <h3>{m.nume}</h3>
      <p>{m.email} · @{m.username}</p>
      {m.materii?.length > 0 && <p>{m.materii.join(', ')}</p>}
      {m.descriere && <p className="tv-indiciu">{m.descriere}</p>}
      {admin && m.areDovada && (
        <a className="tv-pastila" href={`${API}/organizatia-mea/membri/${m.utilizatorId}/dovada`} target="_blank" rel="noreferrer">Descarcă dovada calificării</a>
      )}
      {m.stare === 'in_asteptare' ? (
        respinge ? (
          <div className="tv-form">
            <textarea className="tv-camp-text" rows={2} value={motiv} onChange={(e) => setMotiv(e.target.value)} placeholder="Motivul (îl primește pe email)" aria-label="Motivul respingerii" />
            <div className="tv-grid-2">
              <button className="tv-btn tv-gri" onClick={() => setRespinge(false)}>Renunț</button>
              <button className="tv-btn" disabled={!motiv.trim()} onClick={() => trimite('respingere', 'POST', { motiv })}>Respinge</button>
            </div>
          </div>
        ) : (
          <div className="tv-butoane">
            <button className="tv-btn" onClick={() => trimite('aprobare')}>Aprobă</button>
            <button className="tv-btn tv-gri" onClick={() => setRespinge(true)}>Respinge</button>
          </div>
        )
      ) : admin && m.stare === 'aprobat' ? (
        <div className="tv-form">
          {m.rol === 'profesor' && (
            <label className="tv-bifa">
              <input type="checkbox" checked={m.poatePublicaPublic} onChange={(e) => trimite('publicare', 'PUT', { poatePublicaPublic: e.target.checked })} />
              Poate publica lecții vizibile pentru toți (altfel doar pentru organizație)
            </label>
          )}
          <button className="tv-btn tv-gri" onClick={() => window.confirm(`Scoți persoana „${m.nume}” din organizație?`) && trimite('', 'DELETE')}>Scoate din organizație</button>
        </div>
      ) : (
        <p className="tv-indiciu">Stare: {m.stare}</p>
      )}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    </article>
  )
}

function Lista({ membri, gol, ...rest }) {
  if (!membri) return <p className="tv-asteapta">Se încarcă...</p>
  if (membri.length === 0) return <p className="tv-gol">{gol}</p>
  return <div className="tv-profesori">{membri.map((m) => <Membru key={`${m.utilizatorId}-${m.stare}`} m={m} {...rest} />)}</div>
}

const TABURI = [
  { id: 'cereri', nume: 'Cereri' },
  { id: 'profesori', nume: 'Profesori' },
  { id: 'elevi', nume: 'Elevi' },
  { id: 'detalii', nume: 'Organizația' },
]

function PanouAdmin({ org, onSchimbat }) {
  const [tab, setTab] = useState('cereri')
  const [membri, setMembri] = useState(null)
  const [mesaj, setMesaj] = useState(null)

  const incarca = useCallback(() => {
    const filtru = { cereri: '?stare=in_asteptare', profesori: '?rol=profesor&stare=aprobat', elevi: '?rol=elev&stare=aprobat' }[tab]
    if (filtru) api('/organizatia-mea/membri' + filtru).then((r) => setMembri(r.elemente)).catch(() => setMembri([]))
  }, [tab])
  useEffect(() => incarca(), [incarca])

  async function salveaza(e) {
    e.preventDefault()
    const d = Object.fromEntries(new FormData(e.currentTarget))
    try {
      await api('/organizatia-mea', { metoda: 'PUT', corp: { nume: d.nume, adresa: d.adresa, judetId: Number(d.judetId), localitateId: d.localitateId ? Number(d.localitateId) : null } })
      setMesaj({ ok: true, text: 'Datele organizației au fost salvate.' })
      onSchimbat()
    } catch (err) {
      setMesaj({ ok: false, text: err.message })
    }
  }

  const schimbat = () => { incarca(); onSchimbat() }
  return (
    <>
      <div className="tv-taburi" role="tablist" style={{ maxWidth: 560 }}>
        {TABURI.map((t) => (
          <button key={t.id} role="tab" aria-selected={tab === t.id} className={tab === t.id ? 'tv-tab-activ' : ''} onClick={() => { setMembri(null); setTab(t.id) }}>
            {t.nume}{t.id === 'cereri' && org.cereri > 0 ? ` (${org.cereri})` : ''}
          </button>
        ))}
      </div>
      {tab === 'cereri' && <Lista membri={membri} gol="Nicio cerere în așteptare. Cererile expiră singure după 7 zile." cale="/organizatia-mea/membri" admin onSchimbat={schimbat} />}
      {tab === 'profesori' && <Lista membri={membri} gol="Niciun profesor aprobat încă." cale="/organizatia-mea/membri" admin onSchimbat={schimbat} />}
      {tab === 'elevi' && <Lista membri={membri} gol="Niciun elev în organizație încă." cale="/organizatia-mea/membri" admin onSchimbat={schimbat} />}
      {tab === 'detalii' && (
        <form className="tv-form tv-cutie" onSubmit={salveaza} style={{ maxWidth: 640 }}>
          <label className="tv-eticheta-camp"><span>Numele organizației</span><input className="tv-camp-text" name="nume" defaultValue={org.nume} required maxLength={200} /></label>
          <label className="tv-eticheta-camp"><span>Adresa</span><input className="tv-camp-text" name="adresa" defaultValue={org.adresa} required maxLength={300} /></label>
          <AlegeLoc obligatoriu judetInitial={org.judetId} localitateInitiala={org.localitateId ? { id: org.localitateId, nume: org.localitate, uatNume: '' } : null} />
          <button className="tv-btn">Salvează</button>
          {mesaj && <p className={mesaj.ok ? 'tv-succes' : 'tv-eroare'}>{mesaj.text}</p>}
        </form>
      )}
    </>
  )
}

function PanouProfesor() {
  const [organizatii, setOrganizatii] = useState(null)
  const [alesa, setAlesa] = useState(null)
  const [cereri, setCereri] = useState(null)

  useEffect(() => {
    api('/utilizatori/me/organizatii-predate').then((r) => {
      setOrganizatii(r.elemente)
      if (r.elemente.length) setAlesa(r.elemente[0].id)
    }).catch(() => setOrganizatii([]))
  }, [])
  const incarca = useCallback(() => {
    if (alesa) api(`/organizatii/${alesa}/elevi?stare=in_asteptare`).then((r) => setCereri(r.elemente)).catch(() => setCereri([]))
  }, [alesa])
  useEffect(() => incarca(), [incarca])

  if (!organizatii) return <p className="tv-asteapta">Se încarcă...</p>
  if (organizatii.length === 0) return <p className="tv-gol">Nu predai încă într-o organizație aprobată.</p>
  return (
    <>
      {organizatii.length > 1 && (
        <label className="tv-eticheta-camp" style={{ maxWidth: 420 }}>
          <span>Organizația</span>
          <select className="tv-camp-text" value={alesa ?? ''} onChange={(e) => { setCereri(null); setAlesa(Number(e.target.value)) }}>
            {organizatii.map((o) => <option key={o.id} value={o.id}>{o.nume}</option>)}
          </select>
        </label>
      )}
      <Lista membri={cereri} gol="Niciun elev nu așteaptă confirmarea." cale={`/organizatii/${alesa}/elevi`} onSchimbat={incarca} />
    </>
  )
}

export default function PaginaOrganizatie() {
  const utilizator = usePagina('profesor')
  const [org, setOrg] = useState(undefined) // null = nu administrează o organizație aprobată

  const incarca = useCallback(() => {
    api('/organizatia-mea').then(setOrg).catch(() => setOrg(null))
  }, [])
  useEffect(() => {
    if (utilizator) incarca()
  }, [utilizator, incarca])

  if (!utilizator || org === undefined) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">{org ? 'Administrarea organizației' : 'Elevii organizațiilor tale'}</span>
          <h1>{org ? org.nume : 'Confirmă elevii'}</h1>
          <p>
            {org
              ? `${org.profesori} profesori și ${org.elevi} elevi. Aprobi profesorii care predau aici; elevii îi poți confirma tu sau profesorii organizației.`
              : 'Elevii care cer să intre în organizațiile în care predai așteaptă confirmarea ta sau a administratorului.'}
          </p>
        </section>
        <div className="tv-coala">{org ? <PanouAdmin org={org} onSchimbat={incarca} /> : <PanouProfesor />}</div>
      </div>
    </main>
  )
}
