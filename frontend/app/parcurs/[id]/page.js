'use client'
// /parcurs/7 : harta unei Lecții — câte o insulă pentru fiecare capitol, toate deschise; ordinea e doar o recomandare.
// Sub hartă: testul Lecției (singurul care o termină) și fișierele ei.

import { useCallback, useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import PrevizualizareSalvat from '@/components/PrevizualizareSalvat'
import { marimeFisier } from '@/lib/marime-fisier'
import { api, urlApi } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import HartaInsule from '@/components/HartaInsule'
import Incarcare from '@/components/Incarcare'
import { numeClasa } from '@/lib/clase'

export default function PaginaParcurs() {
  const { id } = useParams()
  const elev = usePagina('elev', 'admin')
  const [lectie, setLectie] = useState(null)
  const [fisiere, setFisiere] = useState([])
  const [eroare, setEroare] = useState('')

  const incarca = useCallback(() => {
    api(`/parcursuri/${id}`).then(setLectie).catch((e) => setEroare(e.message))
    api(`/parcursuri/${id}/fisiere`).then((r) => setFisiere(r.elemente)).catch(() => setFisiere([]))
  }, [id])

  useEffect(() => {
    if (elev) incarca()
  }, [elev, incarca])

  if (eroare) return <Incarcare text={eroare} />
  if (!elev || !lectie) return <Incarcare />

  const terminata = !!lectie.progres?.promovatLa
  return (
    <main className="tv-pagina">
      <div className="tv-continut tv-harta-pagina">
        <div className="tv-harta-antet">
          <Link href={`/materiale/${lectie.materieId}`} className="tv-erou-pill">← {lectie.materie}</Link>
          <h1>{lectie.titlu}</h1>
          <span className={`tv-pastila ${terminata ? 'tv-pastila-verde' : 'tv-pastila-aur'}`}>
            {numeClasa(lectie.clasa)} · {lectie.capitole.length} {lectie.capitole.length === 1 ? 'capitol' : 'capitole'}{terminata ? ' · terminată ✓' : ''}
          </span>
        </div>
        <p className="tv-harta-autor">
          {lectie.autor ?? 'Autor necunoscut'}{lectie.organizatie ? ` · ${lectie.organizatie}` : ' · conținut al platformei'}
          {lectie.vizibilitate === 'organizatie' ? ' · doar pentru organizație' : ''}
        </p>
        {lectie.capitole.length ? (
          <HartaInsule key={lectie.id} lectie={lectie} />
        ) : (
          <p className="tv-gol" style={{ background: '#fff' }}>Lecția nu are încă capitole.</p>
        )}

        <div className="tv-grid-2">
          <section className="tv-card">
            <h3>Testul lecției</h3>
            {lectie.intrebariTest > 0 ? (
              <>
                <p className="tv-cine">
                  Întrebări din toate capitolele{fisiere.length ? ' și fișierele' : ''} lecției. Treci cu 80% din răspunsuri corecte.
                  {terminata ? ' Ai trecut deja testul; îl poți relua pentru recapitulare.' : ' Lecția e terminată când treci testul.'}
                </p>
                <Link className="tv-btn" style={{ display: 'block', textAlign: 'center', marginTop: 10 }} href={`/test/${lectie.id}`}>
                  {terminata ? 'Recapitulează' : 'Începe testul'}
                </Link>
              </>
            ) : (
              <p className="tv-gol">Profesorul nu a pregătit încă testul acestei lecții.</p>
            )}
          </section>

          <section className="tv-card">
            <h3>Fișiere</h3>
            {fisiere.length ? (
              <ul className="tv-lista">
                {fisiere.map((f) => (
                  <li key={f.id}>
                    {/* PDF-ul se deschide în browser, într-un tab nou; Word și PowerPoint se descarcă. */}
                    <a
                      className="tv-rand"
                      href={urlApi(`/fisiere/${f.id}`)}
                      {...(f.tip === 'pdf' ? { target: '_blank', rel: 'noreferrer' } : { download: f.nume })}
                    >
                      <span className="tv-pastila tv-pastila-aur">{f.tip.toUpperCase()}</span>
                      <div><b>{f.nume}</b><small>{marimeFisier(f.marimeOcteti)} · {f.tip === 'pdf' ? 'se deschide' : 'se descarcă'}</small></div>
                    </a>
                    <PrevizualizareSalvat fisier={f} />
                  </li>
                ))}
              </ul>
            ) : (
              <p className="tv-gol">Lecția nu are fișiere.</p>
            )}
          </section>
        </div>
      </div>
    </main>
  )
}
