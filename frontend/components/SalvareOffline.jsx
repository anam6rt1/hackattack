'use client'
import { useState } from 'react'
import { api } from '@/lib/api'
import { offlineClear, offlineSave, offlineRead } from '../public/offline-store'
export default function SalvareOffline({ lectie, uid }) {
  const [mesaj, setMesaj] = useState(''), [ocupat, setOcupat] = useState(false), [test, setTest] = useState(false)
  async function salveaza() {
    setOcupat(true); setMesaj('')
    try {
      if (!('serviceWorker' in navigator)) throw new Error('Acest browser nu permite salvarea offline.')
      await navigator.serviceWorker.register('/sw.js')
      await navigator.serviceWorker.ready
      const existente = await offlineRead()
      if (existente.some(r => r.uid !== uid)) await offlineClear()
      const capitole = []
      for (const c of lectie.capitole) {
        const d = await api(`/cursuri/${c.id}`)
        capitole.push({ titlu: d.titlu, text: [d.continut, ...(d.materiale ?? []).map(m => `${m.titlu}\n${m.text}`)].filter(Boolean).join('\n\n') })
      }
      const quiz = test ? await api(`/parcursuri/${lectie.id}/quiz`, { metoda: 'POST' }) : null
      await offlineSave({ id: lectie.id, uid, titlu: lectie.titlu, capitole, quiz, raspunsuri: {}, salvataLa: new Date().toISOString() })
      setMesaj('Lecția a fost salvată pe acest dispozitiv.')
    } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <section className="tv-cutie"><h3>Învață fără internet</h3><p>Salvează textul capitolelor pe dispozitivul tău. Videoclipurile și fișierele nu sunt incluse. Evită salvarea pe un dispozitiv folosit în comun. Copiile se șterg la ieșirea din cont.</p>
    {lectie.intrebariTest > 0 && <label><input type="checkbox" checked={test} onChange={e => setTest(e.target.checked)} /> Pregătește și testul pentru lucru offline. Răspunsurile se verifică numai după reconectare.</label>}
    <button className="tv-btn" disabled={ocupat} onClick={salveaza}>Salvează pe acest dispozitiv</button> <a href="/offline.html">Deschide lecțiile salvate</a>{mesaj && <p role="status">{mesaj}</p>}
  </section>
}
