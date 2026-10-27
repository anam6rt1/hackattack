'use client'
// /descarcate : lecțiile de pe acest dispozitiv (merg fără internet), ce așteaptă să ajungă pe server și ce a refuzat serverul.
import { useCallback, useEffect, useState } from 'react'
import Link from 'next/link'
import { usePagina } from '@/lib/sesiune'
import { useOnline } from '@/lib/useOnline'
import { marimeFisier } from '@/lib/marime-fisier'
import { rute } from '@/lib/rute'
import Incarcare from '@/components/Incarcare'

async function citesteStare() {
  const [{ lectiiDescarcate, stergeLectie }, s] = await Promise.all([import('@/lib/offline/pachete'), import('@/lib/offline/sincronizare')])
  const spatiu = await navigator.storage?.estimate?.().catch(() => null)
  return { lectii: await lectiiDescarcate(), asteapta: await s.actiuniInAsteptare(), refuzate: await s.actiuniRefuzate(), spatiu, stergeLectie }
}

const NUME_ACTIUNE = { test: 'Test', simulare: 'Simulare', recapitulare: 'Recapitulare', capitol_deschis: 'Capitol citit' }

export default function PaginaDescarcate() {
  const elev = usePagina('elev')
  const online = useOnline()
  const [stare, setStare] = useState(null)
  const [mesaj, setMesaj] = useState('')

  const incarca = useCallback(() => citesteStare().then(setStare), [])

  useEffect(() => {
    if (!elev) return
    let activ = true
    const reia = () => citesteStare().then((s) => { if (activ) setStare(s) })
    reia()
    window.addEventListener('telebac-sincronizare', reia)
    return () => { activ = false; window.removeEventListener('telebac-sincronizare', reia) }
  }, [elev])

  async function trimite() {
    setMesaj('')
    try {
      const r = await (await import('@/lib/offline/sincronizare')).sincronizeaza()
      setMesaj(r.aplicate || r.refuzate ? '' : 'Nu era nimic de trimis.')
    } catch (e) { setMesaj(e.message) }
    await incarca()
  }

  async function uita() {
    await (await import('@/lib/offline/sincronizare')).uitaRefuzate()
    await incarca()
  }

  if (!elev || !stare) return <Incarcare />
  const total = stare.lectii.reduce((s, l) => s + l.marime, 0)
  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <h1>Lecții descărcate</h1>
          <p>Merg fără internet: capitolele, fișierele, testul și simulările din întrebările lor. Rezultatele ajung pe server la reconectare.</p>
        </section>
        <div className="tv-coala">
          {stare.asteapta.length > 0 && (
            <section className="tv-cutie">
              <h3>De trimis pe server</h3>
              <ul className="tv-lista">{stare.asteapta.map((a) => <li key={a.idClient}>{NUME_ACTIUNE[a.tip] ?? a.tip} · {new Date(a.creataLa).toLocaleString('ro-RO')}</li>)}</ul>
              {online ? <button type="button" className="tv-btn" onClick={trimite}>Trimite acum</button> : <p>Se trimit automat când revine internetul.</p>}
            </section>
          )}
          {stare.refuzate.length > 0 && (
            <section className="tv-cutie" role="alert">
              <h3>Nu au contat</h3>
              <ul className="tv-lista">{stare.refuzate.map((r, i) => <li key={i}>{NUME_ACTIUNE[r.tip] ?? r.tip}: {r.mesaj}</li>)}</ul>
              <button type="button" className="tv-pastila" onClick={uita}>Am înțeles</button>
            </section>
          )}
          {mesaj && <p role="status">{mesaj}</p>}
          {stare.lectii.length ? (
            <>
              <p className="tv-cine">
                {stare.lectii.length} {stare.lectii.length === 1 ? 'lecție' : 'lecții'} · {marimeFisier(total)}
                {stare.spatiu?.quota ? ` · spațiu liber pe dispozitiv pentru aplicație: ${marimeFisier(Math.max(0, stare.spatiu.quota - stare.spatiu.usage))}` : ''}
              </p>
              <ul className="tv-lista">
                {stare.lectii.map((l) => (
                  <li key={l.parcursId} className="tv-rand">
                    <Link href={rute.parcurs(l.parcursId)}><b>{l.titlu}</b></Link>
                    <small>{l.materie} · {l.clasa} · {marimeFisier(l.marime)} · descărcată pe {new Date(l.descarcataLa).toLocaleDateString('ro-RO')}</small>
                    <button type="button" className="tv-pastila" onClick={async () => { await stare.stergeLectie(l.parcursId); await incarca() }}>Șterge</button>
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p className="tv-gol">Nicio lecție descărcată. Deschide o lecție și apasă „Descarcă pentru offline”.</p>
          )}
        </div>
      </div>
    </main>
  )
}
