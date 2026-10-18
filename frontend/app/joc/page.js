'use client'
// Pagina /joc : creezi o cameră Triviador pentru o materie sau intri într-una cu un cod.

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import Harta from './Harta'

// Fără I și O, ca să nu fie confundate cu 1 și 0
const LITERE = 'ABCDEFGHJKLMNPQRSTUVWXYZ'

function codNou() {
  let cod = ''
  for (let i = 0; i < 4; i++) cod += LITERE[Math.floor(Math.random() * LITERE.length)]
  return cod
}

export default function PaginaJoc() {
  const elev = usePagina('elev')
  const router = useRouter()
  const [cod, setCod] = useState('')
  const [materii, setMaterii] = useState([])
  const [materieId, setMaterieId] = useState('')

  useEffect(() => {
    if (!elev) return
    api('/materii').then((r) => {
      setMaterii(r.elemente)
      if (r.elemente.length) setMaterieId(String(r.elemente[0].id))
    })
  }, [elev])

  if (!elev) return <Incarcare />

  function creeazaCamera() {
    const c = codNou()
    // acest tab e gazda camerei și ține minte materia aleasă
    sessionStorage.setItem('gazda-' + c, JSON.stringify({ materieId: Number(materieId) }))
    router.push('/joc/' + c)
  }

  function intraInCamera(e) {
    e.preventDefault()
    const c = cod.trim().toUpperCase()
    if (c.length === 4) router.push('/joc/' + c)
  }

  return (
    <main className="tv-pagina">
      <div className="tv-joc">
        <Harta />
        <aside className="tv-lateral">
          <div className="tv-card tv-formular">
            <h3>Triviador</h3>
            <p className="tv-indiciu">
              Cucerește insulele răspunzând la întrebări din lecții. Până la 3 jucători, fiecare de pe telefonul lui.
            </p>
            <label className="tv-eticheta-camp">
              <span>Materia jocului</span>
              <select className="tv-camp-text" value={materieId} onChange={(e) => setMaterieId(e.target.value)}>
                {materii.map((m) => <option key={m.id} value={m.id}>{m.denumire}</option>)}
              </select>
            </label>
            <button className="tv-btn" onClick={creeazaCamera} disabled={!materieId}>Creează o cameră</button>
            <div className="tv-mic" style={{ textAlign: 'center' }}>sau intră cu un cod</div>
            <form onSubmit={intraInCamera} className="tv-formular">
              <input
                className="tv-camp tv-camp-cod"
                value={cod}
                onChange={(e) => setCod(e.target.value.toUpperCase())}
                maxLength={4}
                placeholder="COD"
                aria-label="Codul camerei"
              />
              <button type="submit" className="tv-btn tv-aur">Intră în cameră</button>
            </form>
          </div>
        </aside>
      </div>
    </main>
  )
}
