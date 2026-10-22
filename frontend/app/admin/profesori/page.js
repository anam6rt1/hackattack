'use client'
// /admin/profesori : profesorii platformei. Îi aprobă administratorii organizațiilor în care predau și rămân aprobați și după ce
// pleacă din ele; administratorul platformei vede dovada calificării, stabilește onorariul negociat (cât primește profesorul pe
// elev, din tariful fix) și poate retrage aprobarea (cu motiv; ședințele viitoare se anulează) sau o poate da înapoi.

import { useCallback, useEffect, useState } from 'react'
import { API, api, dataOra, urlApi } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import { useConfirmare } from '@/components/Confirmare'

const STARI = [
  { id: 'in_asteptare', nume: 'În așteptare' },
  { id: 'aprobat', nume: 'Aprobați' },
  { id: 'respins', nume: 'Respinși' },
]

function Cerere({ c, pret, onSchimbat }) {
  const [onorariu, setOnorariu] = useState(c.onorariuNegociat || (pret ? Math.round(pret.suma * 0.7) : 0))
  const [eroare, setEroare] = useState('')
  const [retrage, setRetrage] = useState(false)
  const [motiv, setMotiv] = useState('')
  const [confirma, fereastra] = useConfirmare()

  async function trimite(cale, corp, metoda = 'PUT') {
    setEroare('')
    try {
      await api(`/admin/profesori/${c.id}/${cale}`, { metoda, corp })
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
      {c.stare === 'respins' && c.motivRespingere && (
        <p className="tv-eroare">{c.retrasLa ? `Aprobare retrasă pe ${dataOra(c.retrasLa)}` : 'Respins'}: {c.motivRespingere}</p>
      )}

      <label className="tv-eticheta-camp">
        <span>Onorariu negociat pe elev ({pret ? `din ${pret.suma} ${pret.moneda}` : 'RON'})</span>
        <input className="tv-camp-text" type="number" min={0} max={pret?.suma} step="0.5" value={onorariu} onChange={(e) => setOnorariu(e.target.value)} />
      </label>

      {c.stare === 'aprobat' ? (
        <div className="tv-butoane">
          <button className="tv-btn" onClick={() => trimite('onorariu', { onorariuNegociat: Number(onorariu) })}>Salvează onorariul</button>
          {!retrage && <button className="tv-btn tv-gri" onClick={() => setRetrage(true)}>Retrage aprobarea</button>}
        </div>
      ) : c.retrasLa ? (
        <div className="tv-butoane">
          <button className="tv-btn" onClick={async () => (await confirma({
            titlu: 'Îi dai înapoi aprobarea?', mesaj: `${c.nume} va putea programa din nou ședințe și va apărea la Profesori.`, da: 'Aprobă din nou',
          })) && trimite('reaprobare', undefined, 'POST')}>Aprobă din nou</button>
        </div>
      ) : (
        <p className="tv-indiciu">Cererea e decisă de administratorii organizațiilor alese de profesor.</p>
      )}
      {retrage && (
        <div className="tv-form">
          <textarea className="tv-camp-text" rows={2} value={motiv} onChange={(e) => setMotiv(e.target.value)} maxLength={500}
            placeholder="Motivul (îl vede profesorul și îl primește pe email)" aria-label="Motivul retragerii" />
          <div className="tv-grid-2">
            <button className="tv-btn tv-gri" onClick={() => setRetrage(false)}>Renunț</button>
            <button className="tv-btn" disabled={!motiv.trim()} onClick={async () => (await confirma({
              titlu: 'Retragi aprobarea?',
              mesaj: `${c.nume} nu va mai apărea la Profesori și nu va mai putea programa ședințe. Ședințele viitoare se anulează, iar elevii înscriși primesc un email. O organizație din care face parte îl poate aproba din nou.`,
              da: 'Retrage aprobarea', pericol: true,
            })) && trimite('retragere', { motiv: motiv.trim() }, 'POST')}>Retrage</button>
          </div>
        </div>
      )}
      {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
      {fereastra}
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
          <h1>Profesorii</h1>
          <p>Profesorii sunt aprobați de administratorii organizațiilor lor și rămân aprobați și dacă pleacă din ele. Aici vezi dovada calificării, stabilești onorariul după discuția telefonică și, la nevoie, retragi aprobarea.</p>
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
