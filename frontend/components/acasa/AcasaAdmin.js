'use client'
// Panoul administratorului: cereri de profesori în așteptare și acces la conținut.

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { api } from '@/lib/api'
import Incarcare from '@/components/Incarcare'

export default function AcasaAdmin({ utilizator }) {
  const [cereri, setCereri] = useState(null)
  const [materii, setMaterii] = useState([])

  useEffect(() => {
    api('/admin/profesori?stare=in_asteptare').then((r) => setCereri(r.elemente)).catch(() => setCereri([]))
    api('/materii').then((r) => setMaterii(r.elemente)).catch(() => {})
  }, [])

  if (!cereri) return <Incarcare />

  return (
    <main className="tv-pagina">
      <div className="tv-continut">
        <section className="tv-erou">
          <span className="tv-erou-pill">Administrare</span>
          <h1>Salut, {utilizator.nume.split(' ')[0]}.</h1>
          <p>Aprobă profesorii noi și ține materialele la zi.</p>
        </section>
        <div className="tv-coala">
          <div className="tv-panou">
            <div className="tv-cutie">
              <div className="tv-mic">Cereri de profesori</div>
              <div className="tv-pret">{cereri.length}</div>
              <p>{cereri.length ? 'așteaptă verificarea ta.' : 'Nicio cerere nouă.'}</p>
              <Link className="tv-btn" href="/admin/profesori" style={{ textAlign: 'center', textDecoration: 'none' }}>Vezi cererile</Link>
            </div>
            <div className="tv-cutie tv-panou-lat">
              <div className="tv-mic">Materii</div>
              <ul className="tv-lista">
                {materii.map((m) => (
                  <li key={m.id}>
                    <Link className="tv-rand" href={`/admin/continut?materie=${m.id}`}>
                      <div><b>{m.denumire}</b><small>{m.numarCursuri} lecții publicate</small></div>
                    </Link>
                  </li>
                ))}
              </ul>
              <Link className="tv-btn tv-aur" href="/admin/continut" style={{ textAlign: 'center', textDecoration: 'none' }}>Editează conținutul</Link>
            </div>
          </div>
        </div>
      </div>
    </main>
  )
}
