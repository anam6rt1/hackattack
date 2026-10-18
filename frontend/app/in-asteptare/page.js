'use client'
// Profesorii care încă nu au fost aprobați (sau au fost respinși) ajung aici după autentificare.

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { profesorAprobat, useSesiune } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'

const DECOR = decorSVG(61)

export default function InAsteptare() {
  const { utilizator, iese, reincarca } = useSesiune()
  const router = useRouter()
  const asteapta = utilizator?.rol === 'profesor' && !profesorAprobat(utilizator)

  useEffect(() => {
    if (utilizator !== undefined && !asteapta) router.replace('/')
  }, [utilizator, asteapta, router])

  if (!asteapta) return <Incarcare />
  const respins = utilizator.stareProfesor === 'respins'

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">{respins ? 'Cerere respinsă' : 'Cerere în verificare'}</span>
          <h1>{respins ? 'Cererea ta nu a fost aprobată.' : `Mulțumim, ${utilizator.nume.split(' ')[0]}! Îți verificăm cererea.`}</h1>
          <p>
            {respins
              ? 'Mai jos găsești motivul. Dacă ai întrebări, contactează echipa platformei.'
              : 'Un administrator verifică dovada calificării și te sună pentru a stabili onorariul. După aprobare, vei putea programa ședințe și adăuga lecții.'}
          </p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>
        <div className="tv-coala">
          {respins && utilizator.motivRespingere && (
            <div className="tv-status"><b>Motivul:</b><span>{utilizator.motivRespingere}</span></div>
          )}
          <div className="tv-butoane">
            {!respins && <button className="tv-btn" onClick={reincarca}>Verifică din nou</button>}
            <button className="tv-btn tv-gri" onClick={async () => { await iese(); router.push('/') }}>Ieși din cont</button>
          </div>
        </div>
      </div>
    </main>
  )
}
