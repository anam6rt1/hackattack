'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'

export default function RaportOrganizatie() {
  const utilizator = usePagina('organizatie')
  const [org, setOrg] = useState(null)
  const [luna, setLuna] = useState(() => new Date().toLocaleDateString('sv-SE', { timeZone: 'Europe/Bucharest' }).slice(0, 7))
  const [raport, setRaport] = useState(null)
  const [lista, setLista] = useState(null)
  const [mesaj, setMesaj] = useState('')
  const [ocupat, setOcupat] = useState(false)
  useEffect(() => { if (utilizator) api('/organizatia-mea').then(setOrg).catch(e => setMesaj(e.message)) }, [utilizator])
  useEffect(() => {
    if (!org || !luna) return
    let activ = true
    api(`/organizatii/${org.id}/raport?luna=${encodeURIComponent(luna)}`).then(r => { if (activ) setRaport(r) }).catch(e => { if (activ) { setRaport(null); setMesaj(e.message) } })
    return () => { activ = false }
  }, [org, luna])
  async function citeste(e) {
    const f = e.target.files?.[0]; setLista(null); setMesaj('')
    if (!f) return
    if (f.size > 150000) { setMesaj('Alege un CSV de cel mult 150 KB.'); return }
    setOcupat(true)
    try { setLista(await api(`/organizatii/${org.id}/invitatii/previzualizare`, { metoda: 'POST', corp: { csv: await f.text() } })) }
    catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  async function trimite() {
    setOcupat(true); setMesaj('')
    try {
      const r = await api(`/organizatii/${org.id}/invitatii/trimite`, { metoda: 'POST', corp: { previzualizare: lista.previzualizare } })
      setMesaj(`${r.trimise} invitații pregătite pentru trimitere. ${r.omise} adrese au deja o invitație valabilă.`); setLista(null)
    } catch (e) { setMesaj(e.message) } finally { setOcupat(false) }
  }
  return <main className="tv-pagina"><style>{`@media print { body * { visibility:hidden } #raport-lunar, #raport-lunar * { visibility:visible } #raport-lunar { position:absolute; inset:0; background:white; color:black } .fara-tipar { display:none!important } }`}</style>
    <Link href="/organizatie">Înapoi la organizație</Link>
    <h1>Raport și invitații</h1>
    <label>Luna raportului <input type="month" value={luna} onChange={e => { setRaport(null); setLuna(e.target.value) }} /></label>
    {raport && <section id="raport-lunar" className="tv-cutie"><h2>{org?.nume} · {raport.luna}</h2>
      <p>Elevi activi: <strong>{raport.eleviActivi}</strong></p><p>Lecții terminate: <strong>{raport.lectiiTerminate}</strong></p>
      <p>Teme predate: <strong>{raport.teme.predate} / {raport.teme.total}</strong></p><p>Ședințe încheiate: <strong>{raport.sedinteTinute}</strong></p>
      <h3>Lecții cu cele mai multe răspunsuri greșite</h3><ol>{raport.lectiiCuGreseli.map((l, i) => <li key={i}>{l.titlu}: {l.gresite}</li>)}</ol>
      {!raport.lectiiCuGreseli.length && <p>Nu există răspunsuri greșite înregistrate pentru această perioadă.</p>}
      <p className="tv-indiciu">Datele elevilor membri în prezent, inclusiv activitatea lor pe lecțiile publice. Temele sunt cele cu termen în luna aleasă, predate până la sfârșitul lunii. Ședințele încheiate în aplicație nu confirmă prezența fizică. Activitatea și greșelile sunt măsurate din datele disponibile, fără istoric de autentificări.</p>
      <p>Actualizat: {new Date(raport.generatLa).toLocaleString('ro-RO')}. Datele se actualizează o dată pe oră.</p>
      <button className="tv-btn fara-tipar" onClick={() => window.print()}>Tipărește / Salvează PDF</button>
    </section>}
    {org && <section className="tv-cutie"><h2>Invită elevi din CSV</h2><p>Cel mult 100 de elevi. Prima linie: <code>nume,email,clasa</code>. Clase: VIII, IX, X, XI, XII, Bac. Verifici lista înainte de trimitere; contul se creează numai după acceptarea invitației.</p>
      <input aria-label="Fișier CSV cu elevi" type="file" accept=".csv,text/csv" disabled={ocupat} onChange={citeste} />
      {lista && <><table><thead><tr><th>Rând</th><th>Nume</th><th>Email</th><th>Clasa</th><th>Verificare</th></tr></thead><tbody>{lista.randuri.map(r => <tr key={r.rand}><td>{r.rand}</td><td>{r.nume}</td><td>{r.email}</td><td>{r.clasa}</td><td>{r.eroare ?? 'Pregătit'}</td></tr>)}</tbody></table>
        {lista.previzualizare ? <button className="tv-btn" disabled={ocupat} onClick={trimite}>Trimite invitațiile</button> : <p>Corectează rândurile semnalate și alege din nou fișierul.</p>}</>}
    </section>}
    {mesaj && <p role="status">{mesaj}</p>}
  </main>
}
