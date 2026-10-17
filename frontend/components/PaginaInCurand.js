// Pagină TEMPORARĂ în stilul cu insule, pentru paginile pe care încă nu le-a făcut nimeni.
// Colegul care face pagina reală înlocuiește fișierul page.js din folderul ei.

import Link from 'next/link'
import { claseFonturi } from '@/lib/fonturi'
import { decorSVG } from '@/app/joc/arta'
import '@/app/insule.css'

export default function PaginaInCurand({ eticheta, titlu, text, seed = 5 }) {
  return (
    <main className={`tv-pagina ${claseFonturi}`}>
      <div className="tv-continut">
        <section className="tv-erou" style={{ paddingBottom: 48 }}>
          <span className="tv-erou-pill">{eticheta}</span>
          <h1>{titlu}</h1>
          <p>{text}</p>
          <div style={{ marginTop: 20, maxWidth: 300 }}>
            <Link href="/joc" className="tv-btn tv-aur" style={{ display: 'block', textAlign: 'center', textDecoration: 'none' }}>
              Până atunci, joacă Triviador
            </Link>
          </div>
          <svg className="tv-decor" viewBox="0 0 520 220" aria-hidden="true" dangerouslySetInnerHTML={{ __html: decorSVG(seed) }} />
        </section>
      </div>
    </main>
  )
}
