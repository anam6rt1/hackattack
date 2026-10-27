'use client'
// Aplicațiile desktop și mobilă se conectează la serverul școlii (sau al platformei): la prima pornire îl alege elevul,
// iar în Setări îl poate schimba. Pe site nu apare nimic din acestea.
import { useState, useSyncExternalStore } from 'react'
import { alegeServer, esteAplicatie, serverAles } from '@/lib/platforma'

export function AlegereServer({ actual }) {
  const [adresa, setAdresa] = useState(actual ?? '')
  const [eroare, setEroare] = useState('')
  const [ocupat, setOcupat] = useState(false)
  async function trimite(e) {
    e.preventDefault()
    setEroare(''); setOcupat(true)
    try { await alegeServer(adresa.trim()) } catch (err) { setEroare(err.message); setOcupat(false) }
  }
  return (
    <form onSubmit={trimite} className="tv-cutie">
      <label>
        Adresa serverului (de la profesor sau de pe site, ex. scoala.ro)
        <input className="tv-camp-text" value={adresa} onChange={(e) => setAdresa(e.target.value)} inputMode="url" autoCapitalize="none" autoCorrect="off" required />
      </label>
      <button type="submit" className="tv-btn" disabled={ocupat}>{ocupat ? 'Se verifică…' : 'Conectează'}</button>
      {actual && <p className="tv-cine">Schimbarea serverului șterge lecțiile descărcate de pe acest dispozitiv.</p>}
      {eroare && <p role="alert">{eroare}</p>}
    </form>
  )
}

const nimic = () => () => {}

/** Fără server ales, aplicația arată doar alegerea lui (la hidratare ca pe site: exportul static e randat ca site). */
export default function PoartaServer({ children }) {
  const lipseste = useSyncExternalStore(nimic, () => esteAplicatie() && !serverAles(), () => false)
  if (!lipseste) return children
  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou"><h1>Bun venit în TeleBac</h1><p>Alege serverul la care te conectezi. O singură dată; îl poți schimba din Setări.</p></section>
        <AlegereServer />
      </div>
    </main>
  )
}
