'use client'
// Meniul de sus, comun pentru TOT site-ul.
// Se pune o singură dată în app/layout.js (vezi ghidul, pasul 8).
// Pe ecrane late arată toate paginile; pe telefon, un buton „Meniu” care deschide lista.

import { useState } from 'react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { claseFonturi } from '@/lib/fonturi'
import './meniu.css'

// Iconițe simple, desenate din cod
const ICON = {
  acasa: <path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z" />,
  antrenament: <><circle cx="12" cy="12" r="8.5" fill="none" strokeWidth="2.2" /><circle cx="12" cy="12" r="4" fill="none" strokeWidth="2.2" /><circle cx="12" cy="12" r="1.3" /></>,
  materiale: <path d="M4 4.5A1.5 1.5 0 0 1 5.5 3H11v17H5.5A1.5 1.5 0 0 1 4 18.5zM13 3h5.5A1.5 1.5 0 0 1 20 4.5v14a1.5 1.5 0 0 1-1.5 1.5H13z" />,
  triviador: <><path d="M6 21V3" fill="none" strokeWidth="2.2" /><path d="M7 4h11l-3 4 3 4H7z" /></>,
  profesori: <><path d="M12 4 2 9l10 5 10-5z" /><path d="M6 11.5V16c0 1.6 2.7 3 6 3s6-1.4 6-3v-4.5l-6 3z" /></>,
  linia: <path d="M6.6 3.5 9 3l1.6 4.4-2 1.4a12 12 0 0 0 6.6 6.6l1.4-2L21 15l-.5 2.4A2.5 2.5 0 0 1 18 19.5 15.5 15.5 0 0 1 4.5 6 2.5 2.5 0 0 1 6.6 3.5z" />,
}

export const PAGINI = [
  { href: '/', nume: 'Acasă', icon: 'acasa' },
  { href: '/antrenament', nume: 'Antrenament', icon: 'antrenament' },
  { href: '/materiale', nume: 'Materiale', icon: 'materiale' },
  { href: '/joc', nume: 'Triviador', icon: 'triviador' },
  { href: '/profesori', nume: 'Profesori', icon: 'profesori' },
  { href: '/linia-ai', nume: 'Linia AI', icon: 'linia', nou: true },
]

function Iconita({ nume }) {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" stroke="currentColor" strokeLinecap="round" aria-hidden="true">
      {ICON[nume]}
    </svg>
  )
}

export default function Meniu() {
  const cale = usePathname() || '/'
  const [deschis, setDeschis] = useState(false)
  const activ = (href) => (href === '/' ? cale === '/' : cale.startsWith(href))

  return (
    <header className={`mn-bara ${claseFonturi}`}>
      <div className="mn-interior">
        <Link href="/" className="mn-brand" onClick={() => setDeschis(false)}>
          <svg viewBox="0 0 40 40" aria-hidden="true">
            <circle cx="20" cy="20" r="19" fill="#1b8ad3" />
            <ellipse cx="20" cy="24" rx="13" ry="8" fill="#f1d9a0" />
            <ellipse cx="20" cy="22" rx="10" ry="6" fill="#6cc04a" />
            <path d="M20 9v12" stroke="#6b3410" strokeWidth="2" />
            <path d="M20 9l8 3-8 3z" fill="#ffc61a" />
          </svg>
          <b>Bac la Română</b>
        </Link>

        <button
          className="mn-buton"
          onClick={() => setDeschis(!deschis)}
          aria-expanded={deschis}
          aria-controls="mn-lista"
        >
          <span className="mn-hamburger" aria-hidden="true"><i /><i /><i /></span>
          Meniu
        </button>

        <nav id="mn-lista" className={`mn-lista ${deschis ? 'mn-deschis' : ''}`} aria-label="Meniul principal">
          {PAGINI.map((p) => (
            <Link
              key={p.href}
              href={p.href}
              className={`mn-link ${activ(p.href) ? 'mn-activ' : ''}`}
              aria-current={activ(p.href) ? 'page' : undefined}
              onClick={() => setDeschis(false)}
            >
              <Iconita nume={p.icon} />
              {p.nume}
              {p.nou && <span className="mn-nou">nou</span>}
            </Link>
          ))}
        </nav>
      </div>
    </header>
  )
}
