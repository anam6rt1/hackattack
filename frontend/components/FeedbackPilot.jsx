'use client'
import { useState } from 'react'
import { useSesiune } from '@/lib/sesiune'
import { api } from '@/lib/api'
export default function FeedbackPilot() {
  const { utilizator } = useSesiune()
  const [deschis, setDeschis] = useState(false)
  const [mesaj, setMesaj] = useState('')
  const [ocupat, setOcupat] = useState(false)
  if (!utilizator) return null
  async function trimite(e) {
    e.preventDefault(); setOcupat(true)
    try { await api('/feedback', { metoda: 'POST', corp: { text: new FormData(e.currentTarget).get('text'), pagina: window.location.pathname } }); setMesaj('Mulțumim! Mesajul a ajuns la administrator.'); setDeschis(false) }
    catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <footer className="tv-cutie"><button className="tv-pastila" onClick={() => setDeschis(!deschis)}>Spune-ne ce nu merge</button>{deschis && <form onSubmit={trimite}><label>Ce s-a întâmplat?<textarea name="text" required maxLength={2000} rows={3} /></label><p>Nu include parole sau alte informații sensibile.</p><button disabled={ocupat}>Trimite administratorului</button></form>}{mesaj && <p role="status">{mesaj}</p>}</footer>
}
