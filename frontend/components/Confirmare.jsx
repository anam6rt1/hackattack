'use client'
// Fereastra de confirmare a site-ului, în locul lui window.confirm. Folosire:
//   const [confirma, fereastra] = useConfirmare()
//   if (!(await confirma({ mesaj: 'Ștergi...?', da: 'Șterge', pericol: true }))) return
//   ... și {fereastra} undeva în pagină (în .tv-pagina, ca să aibă culorile temei).

import { useCallback, useEffect, useRef, useState } from 'react'

export function useConfirmare() {
  const [cerere, setCerere] = useState(null) // { mesaj, titlu, da, nu, pericol, raspunde }

  const confirma = useCallback(
    ({ mesaj, titlu = 'Ești sigur?', da = 'Da', nu = 'Anulează', pericol = false }) =>
      new Promise((raspunde) => setCerere({ mesaj, titlu, da, nu, pericol, raspunde })),
    [],
  )

  const inchide = useCallback((raspuns) => {
    setCerere((c) => {
      c?.raspunde(raspuns)
      return null
    })
  }, [])

  const fereastra = cerere && <Fereastra {...cerere} onRaspuns={inchide} />
  return [confirma, fereastra]
}

function Fereastra({ titlu, mesaj, da, nu, pericol, onRaspuns }) {
  const sigur = useRef(null)

  useEffect(() => {
    // La ștergeri, Enter din greșeală nu trebuie să confirme: focusul începe pe „Anulează”.
    sigur.current?.focus()
    const tasta = (e) => e.key === 'Escape' && onRaspuns(false)
    window.addEventListener('keydown', tasta)
    return () => window.removeEventListener('keydown', tasta)
  }, [onRaspuns])

  return (
    <div className="tv-fundal" onClick={() => onRaspuns(false)}>
      <div className="tv-fereastra" role="alertdialog" aria-modal="true" aria-labelledby="tv-confirmare-titlu" aria-describedby="tv-confirmare-mesaj"
        style={{ maxWidth: 440, paddingTop: 28 }} onClick={(e) => e.stopPropagation()}>
        <div className="tv-form">
          <h3 id="tv-confirmare-titlu" style={{ color: 'var(--maro)', fontSize: 24 }}>{titlu}</h3>
          <p id="tv-confirmare-mesaj" className="tv-indiciu" style={{ fontSize: 15 }}>{mesaj}</p>
          <div className="tv-grid-2">
            <button type="button" className="tv-btn tv-gri" ref={pericol ? sigur : null} onClick={() => onRaspuns(false)}>{nu}</button>
            <button type="button" className={`tv-btn${pericol ? ' tv-rosu' : ''}`} ref={pericol ? null : sigur} onClick={() => onRaspuns(true)}>{da}</button>
          </div>
        </div>
      </div>
    </div>
  )
}
