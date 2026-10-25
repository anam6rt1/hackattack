'use client'
import { useEffect, useState } from 'react'
import { api } from '@/lib/api'

const CAMPURI = [['sedinte', 'Ședințe — cu o oră înainte'], ['decizii', 'Decizii privind înscrierea în organizații'], ['temeNoi', 'Teme noi'], ['termeneTeme', 'Teme — termen în următoarele 24 de ore']]
function bytes(base64) { const raw = atob(base64.replace(/-/g, '+').replace(/_/g, '/')); return Uint8Array.from(raw, c => c.charCodeAt(0)) }
export default function SetariNotificari() {
  const [date, setDate] = useState(null)
  const [mesaj, setMesaj] = useState('')
  const [ocupat, setOcupat] = useState(false)
  const [abonat, setAbonat] = useState(false)
  const suport = typeof window !== 'undefined' && window.isSecureContext && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
  useEffect(() => {
    let activ = true
    api('/utilizatori/me/notificari').then(r => { if (activ) setDate(r) }).catch(e => { if (activ) setMesaj(e.message) })
    if (suport) navigator.serviceWorker.getRegistration('/').then(r => r?.pushManager.getSubscription()).then(s => { if (activ) setAbonat(!!s) }).catch(() => {})
    return () => { activ = false }
  }, [suport])
  async function salveaza(e) {
    e.preventDefault(); setOcupat(true); setMesaj('')
    try { await api('/utilizatori/me/notificari', { metoda: 'PUT', corp: Object.fromEntries(CAMPURI.map(([c]) => [c, date[c]])) }); setMesaj('Preferințele au fost salvate.') } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  async function browser(activeaza) {
    setOcupat(true); setMesaj('')
    try {
      if (activeaza) {
        const permisiune = await Notification.requestPermission()
        if (permisiune !== 'granted') { setMesaj('Notificările browserului nu au fost permise. Tipurile activate vor folosi emailul.'); return }
        const reg = await navigator.serviceWorker.register('/sw.js')
        await navigator.serviceWorker.ready
        const s = await reg.pushManager.getSubscription() ?? await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: bytes(date.cheiePublica) })
        await api('/utilizatori/me/notificari/abonamente', { metoda: 'POST', corp: s.toJSON() }); setAbonat(true); setMesaj('Browserul este asociat contului tău.')
      } else {
        const reg = await navigator.serviceWorker.getRegistration('/'); const s = await reg?.pushManager.getSubscription()
        if (s) { await api('/utilizatori/me/notificari/abonamente', { metoda: 'DELETE', corp: { endpoint: s.endpoint } }); await s.unsubscribe() }
        setAbonat(false); setMesaj('Notificările acestui browser au fost oprite.')
      }
    } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <div className="tv-form"><p>Toate notificările sunt dezactivate inițial. Activează doar tipurile pe care le dorești. Fără abonament în browser, trimitem tipurile activate prin email.</p>
    {date && <form className="tv-form" onSubmit={salveaza}>{CAMPURI.map(([c, label]) => <label className="tv-bifa" key={c}><input type="checkbox" checked={date[c]} onChange={e => setDate({ ...date, [c]: e.target.checked })} />{label}</label>)}<button className="tv-btn" disabled={ocupat}>Salvează notificările</button></form>}
    {!date && !mesaj && <p>Se încarcă preferințele…</p>}
    {date && <><h3>Acest browser</h3>{!suport ? <p>Browserul sau conexiunea actuală nu permite notificări push. Emailul rămâne disponibil.</p> : !date.pushDisponibil || !date.cheiePublica ? <p>Notificările browserului nu sunt configurate pe server. Tipurile activate folosesc emailul.</p> : <button className="tv-pastila" disabled={ocupat} onClick={() => browser(!abonat)}>{abonat ? 'Oprește notificările acestui browser' : 'Activează notificările în acest browser'}</button>}</>}
    {mesaj && <p role="status">{mesaj}</p>}
  </div>
}
