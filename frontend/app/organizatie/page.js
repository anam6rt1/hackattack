'use client'
// /organizatie
// - Administratorul organizației: cererile profesorilor și elevilor, membrii (căutare după nume, email sau utilizator și filtre după
//   stare, clasă, materie), cine poate publica lecții public, datele organizației; poate aproba din nou un profesor căruia
//   administratorul platformei i-a retras aprobarea.
// - Profesorii aprobați: cererile elevilor din organizațiile în care predau (le pot aproba sau respinge).

import { useCallback, useEffect, useState } from 'react'
import { API, api, dataOra } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { CLASE, numeClasa } from '@/lib/clase'
import Incarcare from '@/components/Incarcare'
import Paginare from '@/components/Paginare'
import {
  CERERI_PE_PAGINA_ORGANIZATIE, ELEVI_DE_CONFIRMAT_PE_PAGINA_PROFESOR, ELEVI_PE_PAGINA_ORGANIZATIE, PROFESORI_PE_PAGINA_ORGANIZATIE, cuPagina,
} from '@/lib/pagini'
import AlegeLoc from '@/components/AlegeLoc'
import { useConfirmare } from '@/components/Confirmare'

const ROL = { profesor: 'Profesor', elev: 'Elev' }
const STARE = { aprobat: 'Membru', in_asteptare: 'Cerere în așteptare', respins: 'Cerere respinsă', expirat: 'Cerere expirată' }

// Un membru sau o cerere. cale = de unde se decide (organizația mea sau elevii unei organizații).
function Membru({ m, cale, admin, onSchimbat }) {
  const [respinge, setRespinge] = useState(false)
  const [motiv, setMotiv] = useState('')
  const [eroare, setEroare] = useState('')
  const [confirma, fereastra] = useConfirmare()

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
      {admin && m.profesorRetras && (
        <p className="tv-eroare">Administratorul platformei i-a retras aprobarea de profesor: nu apare la Profesori și nu poate programa ședințe.</p>
      )}
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
          {m.profesorRetras && (
            <button className="tv-btn" onClick={async () => (await confirma({
              titlu: 'Îl aprobi din nou ca profesor?', mesaj: `${m.nume} va putea programa din nou ședințe și va apărea la Profesori.`, da: 'Aprobă din nou',
            })) && trimite('reaprobare')}>Aprobă din nou</button>
          )}
          {m.rol === 'profesor' && (
            <label className="tv-bifa">
              <input type="checkbox" checked={m.poatePublicaPublic} onChange={(e) => trimite('publicare', 'PUT', { poatePublicaPublic: e.target.checked })} />
              Poate publica lecții vizibile pentru toți (altfel doar pentru organizație)
            </label>
          )}
          <button className="tv-btn tv-gri" onClick={async () => (await confirma({
            titlu: 'Scoți persoana din organizație?', mesaj: `${m.nume} nu va mai face parte din organizație.`, da: 'Scoate', pericol: true,
          })) && trimite('', 'DELETE')}>Scoate din organizație</button>
        </div>
      ) : (
        <p className="tv-indiciu">{STARE[m.stare] ?? m.stare}</p>
      )}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      {fereastra}
    </article>
  )
}

// total = câți se potrivesc; pagina, marime și onAlege conduc bara de pagini.
function Lista({ membri, gol, total, pagina, marime, onAlege, ...rest }) {
  if (!membri) return <p className="tv-asteapta">Se încarcă...</p>
  if (membri.length === 0) return <p className="tv-gol">{gol}</p>
  return (
    <>
      <div className="tv-profesori">{membri.map((m) => <Membru key={`${m.utilizatorId}-${m.stare}`} m={m} {...rest} />)}</div>
      {onAlege && <Paginare pagina={pagina} totalPagini={Math.ceil((total ?? 0) / marime)} onAlege={onAlege} />}
    </>
  )
}

// O listă de membri pe pagini: incarca(url, marime) o pornește de la prima pagină, alege(p) aduce pagina p a aceleiași liste.
function usePagini() {
  const [membri, setMembri] = useState(null)
  const [total, setTotal] = useState(0)
  const [stare, setStare] = useState({ url: null, pagina: 1, marime: 1 })
  const aduce = useCallback((url, pagina, marime) => {
    api(cuPagina(url, pagina, marime))
      .then((r) => { setMembri(r.elemente); setTotal(r.total); setStare({ url, pagina, marime }) })
      .catch(() => { setMembri([]); setTotal(0) })
  }, [])
  const incarca = useCallback((url, marime) => aduce(url, 1, marime), [aduce])
  const alege = (pagina) => {
    aduce(stare.url, pagina, stare.marime)
    window.scrollTo?.({ top: 0, behavior: 'smooth' })
  }
  // După o decizie (aprobare, scoatere): aceeași pagină, din nou.
  const reincarca = () => stare.url && aduce(stare.url, stare.pagina, stare.marime)
  return { membri, setMembri, total, pagina: stare.pagina, marime: stare.marime, incarca, alege, reincarca }
}

// Câți membri arată fiecare tab al administratorului.
const MARIME_TAB = { cereri: CERERI_PE_PAGINA_ORGANIZATIE, profesori: PROFESORI_PE_PAGINA_ORGANIZATIE, elevi: ELEVI_PE_PAGINA_ORGANIZATIE }

const TABURI = [
  { id: 'cereri', nume: 'Cereri' },
  { id: 'profesori', nume: 'Profesori' },
  { id: 'elevi', nume: 'Elevi' },
  { id: 'detalii', nume: 'Organizația' },
]

const FILTRE_GOALE = { q: '', stare: 'aprobat', clasa: '', materieId: '' }

// Căutarea și filtrele unei liste de membri; textul ajunge în filtru după o scurtă pauză.
function FiltreMembri({ tab, filtre, setFiltre, materii }) {
  const [text, setText] = useState(filtre.q ?? '')
  useEffect(() => {
    const t = setTimeout(() => setFiltre((f) => (f.q === text.trim() ? f : { ...f, q: text.trim() })), 300)
    return () => clearTimeout(t)
  }, [text, setFiltre])
  return (
    <div className="tv-bara-filtre">
      <input type="search" className="tv-camp-text" aria-label="Caută un membru" placeholder="Caută după nume, email sau utilizator" maxLength={100}
        value={text} onChange={(e) => setText(e.target.value)} />
      {tab !== 'cereri' && (
        <select className="tv-camp-text" aria-label="Stare" value={filtre.stare} onChange={(e) => setFiltre({ ...filtre, stare: e.target.value })}>
          {Object.entries(STARE).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
          <option value="">Toate stările</option>
        </select>
      )}
      {tab === 'elevi' && (
        <select className="tv-camp-text" aria-label="Clasa" value={filtre.clasa} onChange={(e) => setFiltre({ ...filtre, clasa: e.target.value })}>
          <option value="">Toate clasele</option>
          {CLASE.filter((c) => c !== 'Bac').map((c) => <option key={c} value={c}>{numeClasa(c)}</option>)}
        </select>
      )}
      {tab === 'profesori' && (
        <select className="tv-camp-text" aria-label="Materie" value={filtre.materieId} onChange={(e) => setFiltre({ ...filtre, materieId: e.target.value })}>
          <option value="">Toate materiile</option>
          {materii.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
        </select>
      )}
    </div>
  )
}

function PanouAdmin({ org, onSchimbat }) {
  const [tab, setTab] = useState('cereri')
  const { membri, setMembri, total, pagina, marime, incarca: incarcaLista, alege, reincarca } = usePagini()
  const [mesaj, setMesaj] = useState(null)
  const [filtre, setFiltre] = useState(FILTRE_GOALE)
  const [materii, setMaterii] = useState([])

  useEffect(() => {
    api('/materii').then((r) => setMaterii(r.elemente)).catch(() => {})
  }, [])
  const incarca = useCallback(() => {
    if (tab === 'detalii') return
    const q = new URLSearchParams()
    if (tab === 'cereri') q.set('stare', 'in_asteptare')
    else {
      q.set('rol', tab === 'profesori' ? 'profesor' : 'elev')
      if (filtre.stare) q.set('stare', filtre.stare)
      if (tab === 'elevi' && filtre.clasa) q.set('clasa', filtre.clasa)
      if (tab === 'profesori' && filtre.materieId) q.set('materieId', filtre.materieId)
    }
    if (filtre.q) q.set('q', filtre.q)
    incarcaLista('/organizatia-mea/membri?' + q, MARIME_TAB[tab])
  }, [tab, filtre, incarcaLista])
  useEffect(() => incarca(), [incarca])
  const cuFiltre = !!(filtre.q || filtre.clasa || filtre.materieId || filtre.stare !== 'aprobat')

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

  const schimbat = () => { reincarca(); onSchimbat() }
  return (
    <>
      <div className="tv-taburi" role="tablist" style={{ maxWidth: 560 }}>
        {TABURI.map((t) => (
          <button key={t.id} role="tab" aria-selected={tab === t.id} className={tab === t.id ? 'tv-tab-activ' : ''}
            onClick={() => { setMembri(null); setFiltre(FILTRE_GOALE); setTab(t.id) }}>
            {t.nume}{t.id === 'cereri' && org.cereri > 0 ? ` (${org.cereri})` : ''}
          </button>
        ))}
      </div>
      {tab !== 'detalii' && <FiltreMembri key={tab} tab={tab} filtre={filtre} setFiltre={setFiltre} materii={materii} />}
      {tab === 'cereri' && <Lista membri={membri} gol={filtre.q ? 'Nicio cerere nu se potrivește căutării.' : 'Nicio cerere în așteptare. Cererile expiră singure după 7 zile.'} cale="/organizatia-mea/membri" admin onSchimbat={schimbat} total={total} pagina={pagina} marime={marime} onAlege={alege} />}
      {tab === 'profesori' && <Lista membri={membri} gol={cuFiltre ? 'Niciun profesor nu se potrivește căutării și filtrelor.' : 'Niciun profesor aprobat încă.'} cale="/organizatia-mea/membri" admin onSchimbat={schimbat} total={total} pagina={pagina} marime={marime} onAlege={alege} />}
      {tab === 'elevi' && <Lista membri={membri} gol={cuFiltre ? 'Niciun elev nu se potrivește căutării și filtrelor.' : 'Niciun elev în organizație încă.'} cale="/organizatia-mea/membri" admin onSchimbat={schimbat} total={total} pagina={pagina} marime={marime} onAlege={alege} />}
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
  const { membri: cereri, setMembri: setCereri, total, pagina, marime, incarca: incarcaLista, alege, reincarca } = usePagini()

  useEffect(() => {
    api('/utilizatori/me/organizatii-predate').then((r) => {
      setOrganizatii(r.elemente)
      if (r.elemente.length) setAlesa(r.elemente[0].id)
    }).catch(() => setOrganizatii([]))
  }, [])
  const incarca = useCallback(() => {
    if (alesa) incarcaLista(`/organizatii/${alesa}/elevi?stare=in_asteptare`, ELEVI_DE_CONFIRMAT_PE_PAGINA_PROFESOR)
  }, [alesa, incarcaLista])
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
      <Lista membri={cereri} gol="Niciun elev nu așteaptă confirmarea." cale={`/organizatii/${alesa}/elevi`} onSchimbat={reincarca} total={total} pagina={pagina} marime={marime} onAlege={alege} />
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
