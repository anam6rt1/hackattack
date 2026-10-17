'use client'
// Pagina /joc : creezi o cameră Triviador sau intri într-una cu un cod.

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Harta from './Harta'

// Fără I și O, ca să nu fie confundate cu 1 și 0
const LITERE = 'ABCDEFGHJKLMNPQRSTUVWXYZ'

function codNou() {
  let cod = ''
  for (let i = 0; i < 4; i++) cod += LITERE[Math.floor(Math.random() * LITERE.length)]
  return cod
}

export default function PaginaJoc() {
  const router = useRouter()
  const [cod, setCod] = useState('')

  function creeazaCamera() {
    const c = codNou()
    sessionStorage.setItem('gazda-' + c, 'da') // acest tab e gazda camerei
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
            <h3>Triviador literar</h3>
            <p className="tv-indiciu">
              Cucerește insulele operelor de bac. Până la 3 jucători, fiecare de pe telefonul lui.
            </p>
            <button className="tv-btn" onClick={creeazaCamera}>Creează o cameră</button>
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
