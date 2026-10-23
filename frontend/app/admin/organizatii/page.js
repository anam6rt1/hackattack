'use client'
// /admin/organizatii : cererile de organizații noi. Aprobarea organizației îl aprobă și pe administratorul ei
// (care predă și el în organizație). Profesorii îi aprobă apoi administratorul fiecărei organizații.

import { useCallback, useEffect, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import Paginare from '@/components/Paginare'

// Câte organizații are o pagină (OrganizatiiEndpoints.OrganizatiiPePagina pe server).
const PE_PAGINA = 20

const STARI = [
  { id: 'in_asteptare', nume: 'În așteptare' },
  { id: 'aprobata', nume: 'Aprobate' },
  { id: 'respinsa', nume: 'Respinse' },
]

function Cerere({ o, onSchimbat }) {
  const [respinge, setRespinge] = useState(false)
  const [motiv, setMotiv] = useState('')
  const [eroare, setEroare] = useState('')

  async function trimite(actiune, corp) {
    setEroare('')
    try {
      await api(`/admin/organizatii/${o.id}/${actiune}`, { metoda: 'POST', corp })
      onSchimbat()
    } catch (e) {
      setEroare(e.message)
    }
  }

  return (
    <article className="tv-cutie">
      <div className="tv-mic">Cerere din {dataOra(o.creatLa)}</div>
      <h3>{o.nume}</h3>
      <p>{o.adresa} · {o.localitate ? `${o.localitate}, ` : ''}{o.judet}</p>
      <p>
        Administrator: <b>{o.adminNume}</b> · {o.adminEmail} · {o.adminTelefon ?? 'fără telefon'}
        {!o.adminEmailConfirmat && <span className="tv-eroare"> · emailul nu e confirmat încă</span>}
      </p>
      {o.stare === 'aprobata' && <p>{o.profesori} profesori · {o.elevi} elevi · {o.cereri} cereri în așteptare</p>}
      {o.stare === 'respinsa' && o.motivRespingere && <p className="tv-eroare">Respinsă: {o.motivRespingere}</p>}
      {o.stare === 'in_asteptare' &&
        (respinge ? (
          <div className="tv-form">
            <textarea className="tv-camp-text" rows={2} value={motiv} onChange={(e) => setMotiv(e.target.value)} placeholder="Motivul (îl primește pe email)" aria-label="Motivul respingerii" />
            <div className="tv-grid-2">
              <button className="tv-btn tv-gri" onClick={() => setRespinge(false)}>Renunț</button>
              <button className="tv-btn" disabled={!motiv.trim()} onClick={() => trimite('respingere', { motiv })}>Respinge</button>
            </div>
          </div>
        ) : (
          <div className="tv-butoane">
            <button className="tv-btn" onClick={() => trimite('aprobare')} disabled={!o.adminEmailConfirmat}>Aprobă organizația</button>
            <button className="tv-btn tv-gri" onClick={() => setRespinge(true)}>Respinge</button>
          </div>
        ))}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    </article>
  )
}

export default function AdminOrganizatii() {
  const admin = usePagina('admin')
  const [stare, setStare] = useState('in_asteptare')
  const [pagina, setPagina] = useState(1)
  const [lista, setLista] = useState(null)
  const [total, setTotal] = useState(0)

  const incarca = useCallback(() => {
    api(`/admin/organizatii?stare=${stare}${pagina > 1 ? `&pagina=${pagina}` : ''}`)
      .then((r) => { setLista(r.elemente); setTotal(r.total) }).catch(() => setLista([]))
  }, [stare, pagina])
  useEffect(() => {
    if (admin) incarca()
  }, [admin, incarca])

  if (!admin) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Administrare</span>
          <h1>Organizații</h1>
          <p>Aprobă școlile și centrele noi. Profesorii lor sunt aprobați apoi de administratorul fiecărei organizații. Cererile neaprobate se șterg după 7 zile.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-taburi" role="tablist" style={{ maxWidth: 480 }}>
            {STARI.map((s) => (
              <button key={s.id} role="tab" aria-selected={stare === s.id} className={stare === s.id ? 'tv-tab-activ' : ''} onClick={() => { setLista(null); setStare(s.id); setPagina(1) }}>
                {s.nume}
              </button>
            ))}
          </div>
          {!lista ? (
            <p className="tv-asteapta">Se încarcă...</p>
          ) : lista.length === 0 ? (
            <p className="tv-gol">Nicio organizație aici.</p>
          ) : (
            <>
              <div className="tv-profesori">{lista.map((o) => <Cerere key={o.id} o={o} onSchimbat={incarca} />)}</div>
              <Paginare pagina={pagina} totalPagini={Math.ceil(total / PE_PAGINA)} onAlege={(p) => { setPagina(p); window.scrollTo?.({ top: 0, behavior: 'smooth' }) }} />
            </>
          )}
        </div>
      </div>
    </main>
  )
}
