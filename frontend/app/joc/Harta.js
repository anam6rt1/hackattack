'use client'
// Harta cu insule, de orice mărime (vine de la server). Insulele pe care le poți ataca au marginea aurie și se pot apăsa.

import { useMemo } from 'react'
import { HARTA_DEMO } from './date-harta'
import { insulaSVG, proprietarSVG, valuriSVG, barcaSVG } from './arta'

function fundal(latime, inaltime) {
  const k = latime / 1000
  return (
    valuriSVG(latime, inaltime, Math.round(34 * k * (inaltime / 640)), 7) +
    barcaSVG(latime * 0.35, inaltime * 0.36, 1, 0) +
    barcaSVG(latime * 0.69, inaltime * 0.73, 0.85, 1.2)
  )
}

function poduriSVG(harta) {
  const loc = Object.fromEntries(harta.insule.map((i) => [i.id, i]))
  return harta.poduri
    .map(([a, b]) => {
      const A = loc[a]
      const B = loc[b]
      return `<line x1="${A.x}" y1="${A.y}" x2="${B.x}" y2="${B.y}" stroke="#5b3a1a" stroke-width="15" stroke-linecap="round" opacity=".85"/><line x1="${A.x}" y1="${A.y}" x2="${B.x}" y2="${B.y}" stroke="#b9824a" stroke-width="11" stroke-dasharray="5 4"/>`
    })
    .join('')
}

export default function Harta({ harta = HARTA_DEMO, proprietari = {}, jucatori = [], cetati = [], tinte = [], onAlege, atacata }) {
  const juc = (id) => jucatori.find((j) => j.id === id)
  const { latime, inaltime } = harta
  // Fundalul și podurile se schimbă doar cu harta, nu la fiecare mutare
  const decor = useMemo(() => fundal(latime, inaltime) + poduriSVG(harta), [harta, latime, inaltime])
  // Pe hărțile mari etichetele ar acoperi insulele: păstrăm doar numele
  const compact = harta.insule.length > 15

  return (
    <div className="tv-harta" style={{ aspectRatio: `${latime} / ${inaltime}` }}>
      <svg viewBox={`0 0 ${latime} ${inaltime}`} role="img" aria-label="Harta jocului cu insule">
        <g dangerouslySetInnerHTML={{ __html: decor }} />
        {harta.insule.map((ins) => {
          const p = juc(proprietari[ins.id])
          const mare = cetati.includes(ins.id)
          const poate = tinte.includes(ins.id)
          const html = insulaSVG(ins, mare) + (p ? proprietarSVG(ins, mare, p.culoare) : '')
          return (
            <g
              key={ins.id + (p ? p.id : '')}
              className={`tv-insula ${poate ? 'tv-poate' : ''} ${atacata === ins.id ? 'tv-clipeste' : ''}`}
              onClick={poate ? () => onAlege(ins.id) : undefined}
              onKeyDown={poate ? (e) => (e.key === 'Enter' || e.key === ' ') && onAlege(ins.id) : undefined}
              tabIndex={poate ? 0 : undefined}
              role={poate ? 'button' : undefined}
              aria-label={poate ? `Atacă ${ins.nume}` : undefined}
              dangerouslySetInnerHTML={{ __html: html }}
            />
          )
        })}
      </svg>

      {/* Etichetele: numele insulei și cine o deține */}
      {harta.insule.map((ins) => {
        const p = juc(proprietari[ins.id])
        const mare = cetati.includes(ins.id)
        const stanga = (ins.x / latime) * 100 + '%'
        return (
          <div key={ins.id}>
            <div className="tv-eticheta" style={{ left: stanga, top: ((ins.y - (mare ? 92 : 86)) / inaltime) * 100 + '%' }}>
              <b>
                {p && <span className="tv-punct" style={{ background: p.culoare }} />}
                {ins.nume}
              </b>
              {!compact && <small>{ins.autor}</small>}
            </div>
            {!compact && (
              <div className="tv-chip" style={{ left: stanga, top: ((ins.y + 74) / inaltime) * 100 + '%' }}>
                {mare && p ? `Cetatea lui ${p.nume}` : p ? p.nume : 'Liberă'}
              </div>
            )}
          </div>
        )
      })}
    </div>
  )
}
