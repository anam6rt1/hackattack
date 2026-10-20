'use client'
// /admin/mesaje : ultimele emailuri și SMS-uri trimise de aplicație (coduri, avertizări, notificări).
// Conținutul apare doar pentru mesajele preluate de furnizorul „jurnal” (dezvoltare), care nu trimite nimic în realitate:
// așa se pot citi codurile de confirmare până când emailul și SMS-ul sunt configurate pe server.

import { useCallback, useEffect, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'

const CANALE = [
  { id: '', nume: 'Toate' },
  { id: 'email', nume: 'Email' },
  { id: 'sms', nume: 'SMS' },
]

function stare(m) {
  if (m.trimisLa) return m.furnizor === 'jurnal' ? 'Înregistrat (netrimis: furnizor de test)' : `Trimis prin ${m.furnizor}`
  if (m.eroare) return `Eșuat (${m.incercari} încercări): ${m.eroare}`
  return 'În așteptare'
}

export default function PaginaMesaje() {
  const admin = usePagina('admin')
  const [canal, setCanal] = useState('')
  const [mesaje, setMesaje] = useState(null)
  const [eroare, setEroare] = useState('')

  const incarca = useCallback(() => {
    api('/admin/mesaje' + (canal ? `?canal=${canal}` : '')).then((r) => setMesaje(r.elemente), (e) => setEroare(e.message))
  }, [canal])
  useEffect(() => {
    if (admin) incarca()
  }, [admin, incarca])

  if (!admin) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 110 }}>
          <span className="tv-erou-pill">Administrare</span>
          <h1>Mesaje trimise</h1>
          <p>Ultimele 100 de emailuri și SMS-uri. Textul se vede doar pentru mesajele netrimise în realitate (furnizorul de test).</p>
        </section>
        <div className="tv-coala">
          <div className="tv-taburi" role="tablist" style={{ maxWidth: 480 }}>
            {CANALE.map((c) => (
              <button key={c.id} role="tab" aria-selected={canal === c.id} className={canal === c.id ? 'tv-tab-activ' : ''} onClick={() => setCanal(c.id)}>
                {c.nume}
              </button>
            ))}
          </div>
          <button className="tv-btn tv-aur" onClick={incarca} style={{ maxWidth: 200 }}>Reîncarcă</button>
          {eroare && <p className="tv-eroare">{eroare}</p>}
          {!mesaje ? (
            <p className="tv-asteapta">Se încarcă...</p>
          ) : mesaje.length === 0 ? (
            <p className="tv-gol">Niciun mesaj.</p>
          ) : (
            <div className="tv-profesori">
              {mesaje.map((m) => (
                <article key={m.id} className="tv-cutie">
                  <div className="tv-mic">{m.canal.toUpperCase()} · {m.tip} · {dataOra(m.creatLa)}</div>
                  <h3 style={{ overflowWrap: 'anywhere' }}>{m.catre}</h3>
                  {m.subiect && <p>{m.subiect}</p>}
                  <p className={m.eroare && !m.trimisLa ? 'tv-eroare' : 'tv-indiciu'}>{stare(m)}</p>
                  {m.continut && <pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontFamily: 'inherit' }}>{m.continut}</pre>}
                </article>
              ))}
            </div>
          )}
        </div>
      </div>
    </main>
  )
}
