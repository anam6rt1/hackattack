'use client'
// /admin/contact : mesajele trimise din pagina de contact. Răspunsul se scrie din clientul de email al administratorului
// (linkul „Răspunde pe email”); serverul nu trimite nimic de aici.

import { useCallback, useEffect, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { numeSubiect } from '@/lib/contact'
import Incarcare from '@/components/Incarcare'
import Paginare from '@/components/Paginare'

const PE_PAGINA = 20
const ROLURI = { elev: 'elev', profesor: 'profesor', organizatie: 'organizație', admin: 'administrator' }

const raspuns = (m) => `mailto:${m.email}?subject=${encodeURIComponent(`Re: TeleBac · ${numeSubiect(m.subiect)}`)}`

export default function PaginaContactAdmin() {
  const admin = usePagina('admin')
  const [rezolvate, setRezolvate] = useState(false)
  const [pagina, setPagina] = useState(1)
  const [date, setDate] = useState(null)
  const [eroare, setEroare] = useState('')

  const incarca = useCallback(() => {
    api(`/admin/contact?pagina=${pagina}&rezolvate=${rezolvate}`).then(setDate, (e) => setEroare(e.message))
  }, [pagina, rezolvate])
  useEffect(() => {
    if (admin) incarca()
  }, [admin, incarca])

  async function marcheaza(m, actiune) {
    setEroare('')
    try {
      await api(`/admin/contact/${m.id}/${actiune}`, { metoda: 'POST' })
      incarca()
    } catch (e) {
      setEroare(e.message)
    }
  }

  if (!admin) return <Incarcare />

  const alegeTab = (r) => { setRezolvate(r); setPagina(1); setDate(null) }
  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Administrare</span>
          <h1>Mesaje de contact</h1>
          <p>Ce scriu elevii, profesorii și vizitatorii din pagina de contact. Răspunsul pleacă din emailul tău; după aceea marchează mesajul rezolvat.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-taburi" role="tablist" style={{ maxWidth: 420 }}>
            <button role="tab" aria-selected={!rezolvate} className={!rezolvate ? 'tv-tab-activ' : ''} onClick={() => alegeTab(false)}>De rezolvat</button>
            <button role="tab" aria-selected={rezolvate} className={rezolvate ? 'tv-tab-activ' : ''} onClick={() => alegeTab(true)}>Rezolvate</button>
          </div>
          {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
          {!date ? (
            <p className="tv-asteapta">Se încarcă...</p>
          ) : date.elemente.length === 0 ? (
            <p className="tv-gol">{rezolvate ? 'Niciun mesaj rezolvat.' : 'Niciun mesaj de rezolvat.'}</p>
          ) : (
            <>
              <p className="tv-indiciu">{date.total} {date.total === 1 ? 'mesaj' : 'mesaje'}</p>
              <div className="tv-profesori">
                {date.elemente.map((m) => (
                  <article key={m.id} className="tv-cutie">
                    <div className="tv-mic">{numeSubiect(m.subiect)} · {dataOra(m.creatLa)}</div>
                    <h3 style={{ overflowWrap: 'anywhere' }}>{m.nume || 'Cont șters'}</h3>
                    <p>
                      {m.cont ? `Cont de ${ROLURI[m.rol] ?? 'utilizator'}` : 'Fără cont'}
                      {m.email && <> · <span style={{ overflowWrap: 'anywhere' }}>{m.email}</span></>}
                    </p>
                    <p style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', color: 'var(--navy)' }}>{m.text}</p>
                    {m.pagina && <p className="tv-indiciu">Trimis de pe <code>{m.pagina}</code></p>}
                    {m.rezolvatLa && <p className="tv-indiciu">Rezolvat la {dataOra(m.rezolvatLa)}</p>}
                    <div className="tv-boti">
                      {m.email && <a className="tv-btn tv-aur" href={raspuns(m)} style={{ textAlign: 'center', textDecoration: 'none', font: 'inherit', fontSize: 13 }}>Răspunde pe email</a>}
                      {m.rezolvatLa
                        ? <button className="tv-btn" onClick={() => marcheaza(m, 'redeschis')}>Redeschide</button>
                        : <button className="tv-btn" onClick={() => marcheaza(m, 'rezolvat')}>Marchează rezolvat</button>}
                    </div>
                  </article>
                ))}
              </div>
              <Paginare pagina={pagina} totalPagini={Math.ceil(date.total / PE_PAGINA)} onAlege={setPagina} />
            </>
          )}
        </div>
      </div>
    </main>
  )
}
