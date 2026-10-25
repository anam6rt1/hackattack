'use client'
import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
export default function SetariSumarParinte() {
  const [date, setDate] = useState(null)
  const [email, setEmail] = useState('')
  const [mesaj, setMesaj] = useState('')
  const [ocupat, setOcupat] = useState(false)
  async function incarca() { const r = await api('/utilizatori/me/sumar-parinte'); setDate(r); setEmail(r.email ?? '') }
  useEffect(() => { api('/utilizatori/me/sumar-parinte').then(r => { setDate(r); setEmail(r.email ?? '') }).catch(e => setMesaj(e.message)) }, [])
  async function schimba(metoda) {
    setOcupat(true); setMesaj('')
    try { await api('/utilizatori/me/sumar-parinte', { metoda, corp: metoda === 'PUT' ? { email } : undefined }); await incarca(); setMesaj(metoda === 'PUT' ? 'Cererea de confirmare a fost pregătită pentru trimitere.' : 'Sumarele au fost oprite.') }
    catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <section className="tv-cutie"><h3>Sumar săptămânal pentru părinte</h3><p>Opțional. Părintele primește numărul lecțiilor terminate, situația temelor și ședințele încheiate. Trimiterea începe numai după ce confirmă adresa prin email. Poți opri oricând sumarele, iar părintele are un link de oprire în fiecare sumar.</p>
    <p>{date?.confirmatLa ? 'Adresă confirmată' : date?.email ? 'În așteptarea confirmării părintelui' : 'Dezactivat'}</p>
    <form onSubmit={e => { e.preventDefault(); schimba('PUT') }}><label>Email părinte <input type="email" required maxLength={200} value={email} onChange={e => setEmail(e.target.value)} /></label><button disabled={ocupat}>Cere confirmarea</button></form>
    {date?.email && <button disabled={ocupat} onClick={() => schimba('DELETE')}>Oprește sumarele și elimină adresa</button>}{mesaj && <p role="status">{mesaj}</p>}
  </section>
}
