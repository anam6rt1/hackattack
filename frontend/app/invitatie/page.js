'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useHash } from '@/lib/useHash'
import { api } from '@/lib/api'
import { useSesiune } from '@/lib/sesiune'
export default function Invitatie() {
  const { utilizator } = useSesiune()
  const token = useHash()
  const [date, setDate] = useState(null)
  const [mesaj, setMesaj] = useState('')
  const [ocupat, setOcupat] = useState(false)
  const [terminat, setTerminat] = useState(false)
  useEffect(() => {
    if (token) api('/invitatii/detalii', { metoda: 'POST', corp: { token } }).then(setDate).catch(e => setMesaj(e.message))
  }, [token])
  async function accepta(e) {
    e.preventDefault(); const f = new FormData(e.currentTarget); setOcupat(true); setMesaj('')
    try {
      const r = await api('/invitatii/accepta', { metoda: 'POST', corp: { token, username: f.get('username'), parola: f.get('parola'), sub16Ani: f.get('varsta') === 'sub16', acordParinteDeclarat: f.get('acord') === 'on' } })
      setMesaj(r.mesaj); setTerminat(true); window.history.replaceState(null, '', '/invitatie')
    } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <main className="tv-pagina"><h1>Invitație pentru elev</h1>{date && !terminat && <section className="tv-cutie"><h2>{date.organizatie}</h2><p>{date.nume} · {date.email} · {date.clasa}</p>
    <p>Dacă ai deja un cont, intră în el pentru a accepta invitația. Dacă nu ai cont, alegi mai jos numele de utilizator și parola.</p>
    <Link href={`/?inapoi=${encodeURIComponent('/invitatie#' + token)}`}>Intră în contul existent</Link>
    <form onSubmit={accepta}>{!utilizator && <><label>Utilizator nou<input name="username" required maxLength={40} autoComplete="username" /></label><label>Parolă nouă<input name="parola" required type="password" minLength={8} maxLength={1024} autoComplete="new-password" /></label>
      <label>Vârsta<select name="varsta" required defaultValue=""><option value="" disabled>Alege</option><option value="peste16">Am cel puțin 16 ani</option><option value="sub16">Am sub 16 ani</option></select></label><label><input type="checkbox" name="acord" /> Dacă am sub 16 ani, declar că am acordul părintelui sau tutorelui pentru crearea contului.</label></>}
      <label><input type="checkbox" required /> Doresc să mă înscriu în această organizație.</label><button className="tv-btn" disabled={ocupat}>Acceptă invitația</button></form>
  </section>}{mesaj && <p role="status">{mesaj}</p>}{terminat && <Link href="/">Intră în aplicație</Link>}</main>
}
