'use client'
// Pagina /joc/ABC234 : camera de joc Triviador.
//
// CUM FUNCȚIONEAZĂ:
// - Jocul rulează pe server (API-ul .NET): el ține starea, alege întrebările, verifică răspunsurile,
//   numără timpul și dă XP-ul la final. Gazda e cine a creat camera.
// - Pagina doar se conectează la camera ABC234 (lib/canal-joc.js), afișează starea primită și trimite
//   intențiile jucătorului: ce insulă atacă, ce variantă alege, ce număr estimează.
// - Răspunsul corect ajunge în browser abia după ce serverul închide întrebarea.

import { useEffect, useRef, useState } from 'react'
import { useParams } from 'next/navigation'
import Link from 'next/link'
import { usePagina } from '@/lib/sesiune'
import { deschideCanal } from '@/lib/canal-joc'
import Incarcare from '@/components/Incarcare'
import Harta from '../Harta'
import { PanouCamera, PanouJoc } from '../Panou'
import Fereastra from '../Fereastra'

export default function Camera() {
  const params = useParams()
  const cod = String(params.cod).toUpperCase()
  const utilizator = usePagina('elev')
  const euId = utilizator ? String(utilizator.id) : null

  const [conectat, setConectat] = useState(false)
  const [eroare, setEroare] = useState('')
  const [mesaj, setMesaj] = useState('') // de ex. „Doar gazda poate face asta.”
  const [stare, setStare] = useState(null)
  // Cât e ceasul serverului față de al nostru: termenele din stare sunt în timpul serverului.
  const [decalaj, setDecalaj] = useState(0)
  const canal = useRef(null)

  useEffect(() => {
    if (!euId) return
    canal.current = deschideCanal(cod, {
      onStare: (s) => {
        setDecalaj(s.acum - Date.now())
        setStare(s)
      },
      onConectat: () => setConectat(true),
      onDeconectat: () => setConectat(false),
      onEroare: (e) => { setConectat(false); setStare(null); setEroare(e?.cod === 'SERVER_RESTART' ? e.message : e?.message?.includes('nu există') ? 'Camera nu există sau s-a închis.' : 'Conexiunea cu jocul s-a închis. Poți începe o partidă nouă.') },
    })
    return () => canal.current?.inchide()
  }, [euId, cod])

  function trimite(metoda, ...argumente) {
    setMesaj('')
    canal.current?.trimite(metoda, ...argumente).catch((e) => setMesaj(e.message))
  }

  if (eroare) return <main className="tv-pagina"><p role="alert">{eroare}</p><Link className="tv-pastila" href="/joc">Înapoi la jocuri</Link></main>
  if (!utilizator || !stare) return <Incarcare />
  if (!conectat) return <Incarcare text="Se conectează..." />

  const link = window.location.origin + '/joc/' + cod
  const inJoc = stare.faza !== 'lobby'
  const curent = stare.faza === 'alegere' ? stare.jucatori[stare.tura] : null
  const cetati = stare.jucatori.map((j) => j.cetate).filter(Boolean)

  return (
    <main className="tv-pagina">
      <div className="tv-joc">
        <Harta
          harta={stare.harta}
          proprietari={stare.proprietari}
          jucatori={stare.jucatori}
          cetati={cetati}
          tinte={curent?.id === euId ? stare.tinte : []}
          atacata={stare.atac?.insula ?? null}
          onAlege={(id) => trimite('Ataca', id)}
        />
        <aside className="tv-lateral">
          {mesaj && <div className="tv-card tv-mic" role="alert">{mesaj}</div>}
          {inJoc ? (
            <PanouJoc stare={stare} euId={euId} />
          ) : (
            <PanouCamera
              stare={stare}
              euId={euId}
              link={link}
              onPorneste={() => trimite('Porneste')}
              onAdaugaBot={(nivel) => trimite('AdaugaBot', nivel)}
              onScoateBot={(id) => trimite('ScoateBot', id)}
            />
          )}
        </aside>
      </div>

      {['intrebare', 'estimare', 'rezultat', 'final'].includes(stare.faza) && (
        <Fereastra
          key={stare.faza + '-' + (stare.intrebare?.nr || 0)}
          stare={stare}
          euId={euId}
          termen={stare.termenLa ? stare.termenLa - decalaj : 0}
          onRaspunde={(v) => trimite('Raspunde', v)}
          onEstimare={(v) => trimite('Estimeaza', v)}
          onJocNou={() => trimite('JocNou')}
        />
      )}
    </main>
  )
}
