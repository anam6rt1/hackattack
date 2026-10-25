'use client'
import { useState } from 'react'
import { useHash } from '@/lib/useHash'
import { api } from '@/lib/api'
export default function ConfirmareSumarParinte({ oprire = false }) {
  const token = useHash()
  const [mesaj, setMesaj] = useState('')
  const [ocupat, setOcupat] = useState(false)
  const [gata, setGata] = useState(false)
  async function confirma() {
    setOcupat(true)
    try { const r = await api(oprire ? '/sumar-parinte/oprire' : '/sumar-parinte/confirmare', { metoda: 'POST', corp: { token } }); setMesaj(r.mesaj); setGata(true); window.history.replaceState(null, '', window.location.pathname) }
    catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <main className="tv-pagina"><section className="tv-cutie"><h1>{oprire ? 'Oprește sumarele' : 'Sumar săptămânal pentru părinte'}</h1><p>{oprire ? 'Nu vei mai primi sumarele săptămânale.' : 'Confirm că sunt părintele sau tutorele elevului care a solicitat sumarul și doresc să primesc prin email situația lecțiilor, temelor și ședințelor sale. Pot opri oricând aceste emailuri din linkul inclus în sumar.'}</p>
    {!gata && <button className="tv-btn" disabled={ocupat || !token} onClick={confirma}>{oprire ? 'Oprește sumarele' : 'Confirm și activez sumarele'}</button>}{mesaj && <p role="status">{mesaj}</p>}</section></main>
}
