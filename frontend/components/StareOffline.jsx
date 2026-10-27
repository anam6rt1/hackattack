'use client'
// Pe toate paginile: banda „Ești offline” și, după reconectare, ce a confirmat serverul din lucrul făcut fără internet.
// Pornește și trimiterea rezultatelor offline (la deschidere și la fiecare reconectare), pentru elevii cu date pe dispozitiv.
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useOnline, useRetea } from '@/lib/useOnline'
import { useSesiune } from '@/lib/sesiune'

export default function StareOffline() {
  const retea = useRetea(), online = retea === 'online'
  const { utilizator } = useSesiune()
  const [rezultat, setRezultat] = useState(null)

  useEffect(() => {
    if (utilizator?.rol !== 'elev') return
    // Și pentru elevii fără lecții descărcate încă: pot descărca una, lucra offline și reveni fără să reîncarce pagina.
    let oprire = () => {}, activ = true
    import('@/lib/offline/sincronizare').then((m) => { if (activ) oprire = m.pornesteSincronizarea() })
    const asculta = (e) => setRezultat(e.detail)
    window.addEventListener('telebac-sincronizare', asculta)
    return () => { activ = false; oprire(); window.removeEventListener('telebac-sincronizare', asculta) }
  }, [utilizator])

  if (online && !rezultat) return null
  return (
    <div className="tv-cutie" role="status" style={{ margin: '8px auto', maxWidth: 960 }}>
      {!online && <p><b>{retea === 'offline' ? 'Ești offline.' : 'Serverul nu răspunde.'}</b> Lecțiile descărcate, testele și simulările lor merg în continuare; rezultatele se trimit când revine legătura.</p>}
      {online && rezultat && (
        <p>
          Rezultatele offline au ajuns pe server: {rezultat.aplicate} {rezultat.aplicate === 1 ? 'confirmat' : 'confirmate'}
          {rezultat.xp ? `, +${rezultat.xp} XP` : ''}{rezultat.refuzate ? `, ${rezultat.refuzate} ${rezultat.refuzate === 1 ? 'refuzat' : 'refuzate'} (vezi ` : ''}
          {rezultat.refuzate ? <><Link href="/descarcate">Lecții descărcate</Link>)</> : ''}.{' '}
          <button type="button" className="tv-pastila" onClick={() => setRezultat(null)}>Am înțeles</button>
        </p>
      )}
    </div>
  )
}

/** Pentru paginile care au nevoie de server (tutorul AI, tutorul la telefon, jocul live, rezervările). */
export function NecesitaInternet({ ce, children }) {
  const online = useOnline()
  if (online) return children
  return (
    <main className="tv-pagina">
      <div className="tv-cutie" role="status">
        <h2>{ce} are nevoie de internet</h2>
        <p>Între timp poți învăța din <Link href="/descarcate">lecțiile descărcate</Link>: capitolele, fișierele și testele lor merg și offline.</p>
      </div>
    </main>
  )
}
