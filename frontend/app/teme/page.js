'use client'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import TemeleMele from '@/components/TemeleMele'
import TemeProfesor from '@/components/TemeProfesor'

export default function PaginaTeme() {
  const u = usePagina('elev', 'profesor')
  if (!u) return <Incarcare />
  return <main className="tv-pagina"><div className="tv-continut"><section className="tv-erou"><h1>Teme</h1><p>Lecții de parcurs, cu un termen clar.</p></section><div className="tv-coala">{u.rol === 'elev' ? <TemeleMele /> : <TemeProfesor />}</div></div></main>
}
