'use client'
// Cine așteaptă aprobarea ajunge aici după autentificare:
// - profesorii, până când îi aprobă administratorul unei organizații în care au cerut să intre;
// - administratorii de organizații, până când un administrator al platformei aprobă organizația.

import { useCallback, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { esteProfesor, profesorAprobat, useSesiune } from '@/lib/sesiune'
import { decorSVG } from '@/app/joc/arta'
import Incarcare from '@/components/Incarcare'
import AlegeOrganizatie from '@/components/AlegeOrganizatie'

const DECOR = decorSVG(61)

const STARI = { in_asteptare: 'în așteptare', aprobat: 'aprobat', respins: 'respins', expirat: 'expirată' }

function CereriProfesor() {
  const [cereri, setCereri] = useState(null)
  const [mesaj, setMesaj] = useState(null)

  const incarca = useCallback(() => {
    api('/utilizatori/me/organizatii').then((r) => setCereri(r.elemente)).catch(() => setCereri([]))
  }, [])
  useEffect(() => incarca(), [incarca])

  async function cere(e) {
    e.preventDefault()
    const id = new FormData(e.currentTarget).get('organizatieId')
    if (!id) return
    try {
      setCereri((await api('/utilizatori/me/organizatii', { metoda: 'POST', corp: { organizatieId: Number(id) } })).elemente)
      setMesaj({ ok: true, text: 'Cererea a fost trimisă administratorului organizației.' })
    } catch (err) {
      setMesaj({ ok: false, text: err.message })
    }
  }

  return (
    <>
      <h3>Cererile tale</h3>
      {!cereri ? (
        <p className="tv-asteapta">Se încarcă...</p>
      ) : (
        <ul className="tv-lista-simpla">
          {cereri.map((c) => (
            <li key={c.organizatieId}>
              <b>{c.organizatie}</b> · {STARI[c.stare] ?? c.stare}
              {c.motivRespingere && <small> · Motiv: {c.motivRespingere}</small>}
            </li>
          ))}
        </ul>
      )}
      <form className="tv-form" onSubmit={cere}>
        <AlegeOrganizatie />
        <button className="tv-btn tv-aur">Cere să intri și în această organizație</button>
        {mesaj && <p className={mesaj.ok ? 'tv-succes' : 'tv-eroare'}>{mesaj.text}</p>}
      </form>
    </>
  )
}

export default function InAsteptare() {
  const { utilizator, iese, reincarca } = useSesiune()
  const router = useRouter()
  const asteapta = esteProfesor(utilizator) && !profesorAprobat(utilizator)

  useEffect(() => {
    if (utilizator !== undefined && !asteapta) router.replace('/')
  }, [utilizator, asteapta, router])

  if (!asteapta) return <Incarcare />
  const org = utilizator.organizatieAdministrata
  const respins = org ? org.stare === 'respinsa' : utilizator.stareProfesor === 'respins'
  const motiv = org ? org.motivRespingere : utilizator.motivRespingere

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">{respins ? 'Cerere neaprobată' : 'Cerere în verificare'}</span>
          <h1>{respins ? 'Cererea ta nu a fost aprobată.' : `Mulțumim, ${utilizator.nume.split(' ')[0]}! Îți verificăm cererea.`}</h1>
          <p>
            {org
              ? respins
                ? `Organizația „${org.nume}” nu a fost aprobată. Motivul e mai jos.`
                : `Un administrator al platformei verifică organizația „${org.nume}”. După aprobare, vei putea primi profesori și elevi și vei putea preda.`
              : respins
                ? 'Nicio organizație nu ți-a aprobat încă cererea. Poți cere să intri în altă organizație.'
                : 'Administratorul organizației alese îți verifică cererea și dovada calificării. După aprobare, vei putea programa ședințe și adăuga lecții.'}
          </p>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: DECOR }} />
        </section>
        <div className="tv-coala">
          {respins && motiv && (
            <div className="tv-status"><b>Motivul:</b><span>{motiv}</span></div>
          )}
          <p className="tv-indiciu">Dacă cererea nu e aprobată în 7 zile de la înregistrare, contul se șterge automat (primești un email cu o zi înainte).</p>
          {!org && <CereriProfesor />}
          <div className="tv-butoane">
            <button className="tv-btn" onClick={reincarca}>Verifică din nou</button>
            <button className="tv-btn tv-gri" onClick={async () => { await iese(); router.push('/') }}>Ieși din cont</button>
          </div>
        </div>
      </div>
    </main>
  )
}
