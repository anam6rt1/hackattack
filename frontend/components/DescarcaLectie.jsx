'use client'
// Pe pagina lecției: descărcarea ei pentru lucru fără internet (capitole, fișiere, test), actualizarea și ștergerea copiei.
import { useEffect, useRef, useState } from 'react'
import Link from 'next/link'
import { marimeFisier } from '@/lib/marime-fisier'
import { useOnline } from '@/lib/useOnline'

export default function DescarcaLectie({ lectie, uid }) {
  const online = useOnline()
  const [copie, setCopie] = useState(undefined)
  const [progres, setProgres] = useState(null)
  const [mesaj, setMesaj] = useState('')
  const oprire = useRef(null)

  useEffect(() => {
    let activ = true
    import('@/lib/offline/pachete').then((m) => m.lectieDescarcata(lectie.id)).then((p) => { if (activ) setCopie(p) }, () => setCopie(null))
    return () => { activ = false; oprire.current?.abort() }
  }, [lectie.id])

  async function descarca() {
    const c = new AbortController(); oprire.current = c
    setMesaj(''); setProgres({ pas: 0, din: 1 })
    try {
      const { descarcaLectie, pregatesteSiteul } = await import('@/lib/offline/pachete')
      const copie = await descarcaLectie(lectie.id, uid, { semnal: c.signal, onProgres: (pas, din) => setProgres({ pas, din }) })
      // Waited for (at most ~10 s for the service worker): offline right after the message, the test page must already be cached.
      await pregatesteSiteul().catch(() => {})
      setCopie(copie)
      setMesaj('Lecția e pe acest dispozitiv: o poți citi și îi poți da testul fără internet.')
    } catch (e) {
      setMesaj(e.name === 'AbortError' ? 'Descărcare oprită.' : e.message)
    } finally { setProgres(null); oprire.current = null }
  }

  async function sterge() {
    const { stergeLectie } = await import('@/lib/offline/pachete')
    await stergeLectie(lectie.id)
    setCopie(null); setMesaj('Copia de pe acest dispozitiv a fost ștearsă.')
  }

  if (copie === undefined) return null
  return (
    <section className="tv-cutie">
      <h3>Fără internet</h3>
      {copie ? (
        <p>
          Descărcată pe {new Date(copie.descarcataLa).toLocaleDateString('ro-RO')} · {marimeFisier(copie.marime)}. Testul se notează pe loc;
          serverul îl verifică și acordă XP când revii online.
        </p>
      ) : (
        <p>Descarcă lecția cu capitolele, fișierele și testul ei. Evită descărcarea pe un dispozitiv folosit în comun; copiile se șterg la ieșirea din cont.</p>
      )}
      {progres ? (
        <>
          <progress max={progres.din} value={progres.pas} aria-label="Descărcarea lecției" /> <button type="button" className="tv-pastila" onClick={() => oprire.current?.abort()}>Oprește</button>
        </>
      ) : (
        <>
          {online && <button type="button" className="tv-btn" onClick={descarca}>{copie ? 'Actualizează copia' : 'Descarcă pentru offline'}</button>}{' '}
          {copie && <button type="button" className="tv-pastila" onClick={sterge}>Șterge de pe dispozitiv</button>}{' '}
          <Link href="/descarcate">Toate lecțiile descărcate</Link>
        </>
      )}
      {mesaj && <p role="status">{mesaj}</p>}
    </section>
  )
}
