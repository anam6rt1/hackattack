'use client'
// /admin/profesori : cererile profesorilor. Administratorul vede dovada calificării, stabilește
// onorariul negociat (cât primește profesorul pe elev, din tariful fix) și aprobă sau respinge.

import { useCallback, useEffect, useState } from 'react'
import { API, api, urlApi } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'

const STARI = [
  { id: 'in_asteptare', nume: 'În așteptare' },
  { id: 'aprobat', nume: 'Aprobați' },
  { id: 'respins', nume: 'Respinși' },
]

function Cerere({ c, pret, onSchimbat }) {
  const [onorariu, setOnorariu] = useState(c.onorariuNegociat || (pret ? Math.round(pret.suma * 0.7) : 0))
  const [motiv, setMotiv] = useState('')
  const [respinge, setRespinge] = useState(false)
  const [eroare, setEroare] = useState('')

  async function trimite(cale, corp) {
    setEroare('')
    try {
      await api(`/admin/profesori/${c.id}/${cale}`, { metoda: cale === 'onorariu' ? 'PUT' : 'POST', corp })
      onSchimbat()
    } catch (e) {
      setEroare(e.message)
    }
  }

  return (
    <article className="tv-cutie">
      <div className="tv-nivel">
        {c.pozaUrl && (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={urlApi(c.pozaUrl)} alt="" className="tv-poza" width={64} height={64} />
        )}
        <div style={{ minWidth: 0 }}>
          <h3>{c.nume}</h3>
          <p>{c.email} · {c.telefon ?? 'fără telefon'}</p>
          <p>{c.materii.map((m) => m.denumire).join(', ')}</p>
        </div>
      </div>
      {c.descriere && <p>{c.descriere}</p>}
      {c.areDovada && (
        <a className="tv-pastila" href={`${API}/admin/profesori/${c.id}/dovada`} target="_blank" rel="noreferrer">
          Deschide dovada calificării
        </a>
      )}
      {c.stare === 'respins' && c.motivRespingere && <p className="tv-eroare">Respins: {c.motivRespingere}</p>}

      <label className="tv-eticheta-camp">
        <span>Onorariu negociat pe elev ({pret ? `din ${pret.suma} ${pret.moneda}` : 'RON'})</span>
        <input className="tv-camp-text" type="number" min={0} max={pret?.suma} step="0.5" value={onorariu} onChange={(e) => setOnorariu(e.target.value)} />
      </label>

      {respinge ? (
        <div className="tv-form">
          <textarea className="tv-camp-text" rows={2} value={motiv} onChange={(e) => setMotiv(e.target.value)} placeholder="Motivul (îl vede profesorul)" aria-label="Motivul respingerii" />
          <div className="tv-grid-2">
            <button className="tv-btn tv-gri" onClick={() => setRespinge(false)}>Renunț</button>
            <button className="tv-btn" style={{ background: 'var(--rau)', boxShadow: '0 5px 0 #a8322a' }} disabled={!motiv.trim()} onClick={() => trimite('respingere', { motiv })}>
              Respinge
            </button>
          </div>
        </div>
      ) : (
        <div className="tv-butoane">
          {c.stare === 'aprobat' ? (
            <button className="tv-btn" onClick={() => trimite('onorariu', { onorariuNegociat: Number(onorariu) })}>Salvează onorariul</button>
          ) : (
            <button className="tv-btn" onClick={() => trimite('aprobare', { onorariuNegociat: Number(onorariu) })}>Aprobă</button>
          )}
          {c.stare !== 'respins' && <button className="tv-btn tv-gri" onClick={() => setRespinge(true)}>Respinge</button>}
        </div>
      )}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    </article>
  )
}

export default function AdminProfesori() {
  const admin = usePagina('admin')
  const [stare, setStare] = useState('in_asteptare')
  const [cereri, setCereri] = useState(null)
  const [pret, setPret] = useState(null)

  const incarca = useCallback(() => {
    api(`/admin/profesori?stare=${stare}`).then((r) => setCereri(r.elemente)).catch(() => setCereri([]))
  }, [stare])

  useEffect(() => {
    if (admin) api('/tarif').then(setPret).catch(() => {})
  }, [admin])
  useEffect(() => {
    if (admin) incarca()
  }, [admin, incarca])

  if (!admin) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Administrare</span>
          <h1>Cererile profesorilor</h1>
          <p>Verifică dovada calificării, stabilește onorariul după discuția telefonică și aprobă profilul.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-taburi" role="tablist" style={{ maxWidth: 480 }}>
            {STARI.map((s) => (
              <button key={s.id} role="tab" aria-selected={stare === s.id} className={stare === s.id ? 'tv-tab-activ' : ''} onClick={() => setStare(s.id)}>
                {s.nume}
              </button>
            ))}
          </div>
          {!cereri ? (
            <p className="tv-asteapta">Se încarcă...</p>
          ) : cereri.length === 0 ? (
            <p className="tv-gol">Nicio cerere aici.</p>
          ) : (
            <div className="tv-profesori">
              {cereri.map((c) => <Cerere key={c.id} c={c} pret={pret} onSchimbat={incarca} />)}
            </div>
          )}
        </div>
      </div>
    </main>
  )
}
