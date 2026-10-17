'use client'
// Harta cu insule. Insulele pe care le poți ataca au marginea aurie și se pot apăsa.

import { INSULE, PODURI, insula } from './date-harta'
import { insulaSVG, proprietarSVG, valuriSVG, barcaSVG } from './arta'

// Fundalul nu se schimbă niciodată, așa că îl desenăm o singură dată
const FUNDAL =
  valuriSVG(1000, 640, 34, 7) +
  '<g opacity=".7"><ellipse cx="660" cy="250" rx="22" ry="11" fill="#7d8a99"/><ellipse cx="655" cy="246" rx="12" ry="6" fill="#9aa7b5"/><ellipse cx="60" cy="330" rx="18" ry="9" fill="#7d8a99"/></g>' +
  barcaSVG(355, 225, 1, 0) +
  barcaSVG(690, 470, 0.85, 1.2) +
  barcaSVG(950, 280, 0.7, 0.6)

const PODURI_SVG = PODURI.map(([a, b]) => {
  const A = insula(a)
  const B = insula(b)
  return `<line x1="${A.x}" y1="${A.y}" x2="${B.x}" y2="${B.y}" stroke="#5b3a1a" stroke-width="15" stroke-linecap="round" opacity=".85"/><line x1="${A.x}" y1="${A.y}" x2="${B.x}" y2="${B.y}" stroke="#b9824a" stroke-width="11" stroke-dasharray="5 4"/>`
}).join('')

export default function Harta({ proprietari = {}, jucatori = [], cetati = [], tinte = [], onAlege, atacata }) {
  const juc = (id) => jucatori.find((j) => j.id === id)

  return (
    <div className="tv-harta">
      <svg viewBox="0 0 1000 640" role="img" aria-label="Harta jocului cu insule">
        <g dangerouslySetInnerHTML={{ __html: FUNDAL + PODURI_SVG }} />
        {INSULE.map((ins) => {
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

      {/* Etichetele: numele operei și cine deține insula */}
      {INSULE.map((ins) => {
        const p = juc(proprietari[ins.id])
        const mare = cetati.includes(ins.id)
        const stanga = ins.x / 10 + '%'
        return (
          <div key={ins.id}>
            <div className="tv-eticheta" style={{ left: stanga, top: (ins.y - (mare ? 92 : 86)) / 6.4 + '%' }}>
              <b>
                {p && <span className="tv-punct" style={{ background: p.culoare }} />}
                {ins.nume}
              </b>
              <small>{ins.autor}</small>
            </div>
            <div className="tv-chip" style={{ left: stanga, top: (ins.y + 74) / 6.4 + '%' }}>
              {mare && p ? `Cetatea lui ${p.nume}` : p ? p.nume : 'Liberă'}
            </div>
          </div>
        )
      })}
    </div>
  )
}
