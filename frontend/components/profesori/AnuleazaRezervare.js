'use client'
// Butonul „Anulează” pentru o rezervare a elevului, cu confirmare pe loc (fără fereastră de dialog).

import { useState } from 'react'
import { api } from '@/lib/api'

export default function AnuleazaRezervare({ rezervareId, onAnulat }) {
  const [confirma, setConfirma] = useState(false)
  const [eroare, setEroare] = useState('')
  const [trimite, setTrimite] = useState(false)

  async function anuleaza() {
    setTrimite(true)
    setEroare('')
    try {
      await api(`/rezervari/${rezervareId}`, { metoda: 'DELETE' })
      onAnulat()
    } catch (e) {
      setEroare(e.message)
      setTrimite(false)
    }
  }

  if (eroare) return <span className="tv-pastila tv-pastila-rosu" role="alert">{eroare}</span>
  if (!confirma) return <button className="tv-pastila" onClick={() => setConfirma(true)}>Anulează</button>
  return (
    <span className="tv-butoane">
      <button className="tv-pastila tv-pastila-rosu" onClick={anuleaza} disabled={trimite}>{trimite ? 'Se anulează...' : 'Da, anulează'}</button>
      <button className="tv-pastila" onClick={() => setConfirma(false)} disabled={trimite}>Nu</button>
    </span>
  )
}
