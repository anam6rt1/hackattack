'use client'
// Pagina /joc : creezi o cameră Triviador (materii, clasa, mărimea hărții, câți jucători) sau intri într-una cu un cod.
// Camera o creează serverul, care și arbitrează jocul.

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import { CLASE, numeClasa } from '@/lib/clase'
import Harta from './Harta'
import { rute } from '@/lib/rute'

const LUNGIME_COD = 6

export default function PaginaJoc() {
  const elev = usePagina('elev')
  const router = useRouter()
  const [cod, setCod] = useState('')
  const [materii, setMaterii] = useState([])
  const [alese, setAlese] = useState([])
  const [insule, setInsule] = useState(9)
  const [maxJucatori, setMaxJucatori] = useState(3)
  const [clasa, setClasa] = useState('') // '' = toate clasele
  const [eroare, setEroare] = useState('')
  const [seCreeaza, setSeCreeaza] = useState(false)

  useEffect(() => {
    if (!elev) return
    // Nicio materie bifată din oficiu: gazda alege (altfel camera putea porni pe o materie fără întrebări, cu cele demo).
    api('/materii').then((r) => setMaterii(r.elemente))
  }, [elev])

  if (!elev) return <Incarcare />

  const comuta = (id) => setAlese((a) => (a.includes(id) ? a.filter((x) => x !== id) : [...a, id]))

  async function creeazaCamera() {
    setSeCreeaza(true)
    setEroare('')
    try {
      const r = await api('/trivia/camere', { metoda: 'POST', corp: { materieIds: alese, insule, maxJucatori, clasa: clasa || null } })
      router.push(rute.camera(r.cod))
    } catch (e) {
      setEroare(e.message)
      setSeCreeaza(false)
    }
  }

  function intraInCamera(e) {
    e.preventDefault()
    const c = cod.trim().toUpperCase()
    if (c.length === LUNGIME_COD) router.push(rute.camera(c))
  }

  return (
    <main className="tv-pagina">
      <div className="tv-joc">
        <Harta />
        <aside className="tv-lateral">
          <div className="tv-card tv-formular">
            <h3>Triviador</h3>
            <p className="tv-indiciu">
              Cucerește insulele răspunzând la întrebări din lecții. 2–4 jucători, fiecare de pe telefonul lui; locurile libere le pot lua roboți.
            </p>
            <fieldset className="tv-eticheta-camp">
              <span>Materiile jocului{alese.length ? ` (${alese.length} ${alese.length === 1 ? 'aleasă' : 'alese'})` : ''}</span>
              <div className="tv-materii-joc">
                {materii.map((m) => (
                  <label key={m.id} className="tv-bifa">
                    <input type="checkbox" checked={alese.includes(m.id)} onChange={() => comuta(m.id)} /> {m.denumire}
                  </label>
                ))}
              </div>
            </fieldset>
            <label className="tv-eticheta-camp">
              <span>Clasa</span>
              <select className="tv-camp-text" value={clasa} onChange={(e) => setClasa(e.target.value)}>
                <option value="">Toate clasele</option>
                {CLASE.map((c) => <option key={c} value={c}>{numeClasa(c)}</option>)}
              </select>
            </label>
            <label className="tv-eticheta-camp">
              <span>Insule pe hartă: {insule}</span>
              <input type="range" min={9} max={30} value={insule} onChange={(e) => setInsule(Number(e.target.value))} />
            </label>
            <label className="tv-eticheta-camp">
              <span>Jucători</span>
              <select className="tv-camp-text" value={maxJucatori} onChange={(e) => setMaxJucatori(Number(e.target.value))}>
                {[2, 3, 4].map((n) => <option key={n} value={n}>{n} jucători</option>)}
              </select>
            </label>
            {eroare && <p className="tv-mic" role="alert">{eroare}</p>}
            {!alese.length && <p className="tv-mic" id="fara-materie">Bifează cel puțin o materie ca să creezi camera.</p>}
            <button className="tv-btn" onClick={creeazaCamera} disabled={!alese.length || seCreeaza} aria-describedby={alese.length ? undefined : 'fara-materie'}>
              {seCreeaza ? 'Se creează camera…' : 'Creează o cameră'}
            </button>
            <div className="tv-mic" style={{ textAlign: 'center' }}>sau intră cu un cod</div>
            <form onSubmit={intraInCamera} className="tv-formular">
              <input
                className="tv-camp tv-camp-cod"
                value={cod}
                onChange={(e) => setCod(e.target.value.toUpperCase())}
                maxLength={LUNGIME_COD}
                placeholder="COD"
                aria-label="Codul camerei"
              />
              <button type="submit" className="tv-btn tv-aur" disabled={cod.trim().length !== LUNGIME_COD}>Intră în cameră</button>
            </form>
          </div>
        </aside>
      </div>
    </main>
  )
}
