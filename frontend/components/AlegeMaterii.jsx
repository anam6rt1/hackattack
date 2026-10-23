'use client'
// Materiile predate de un profesor, până la MATERII_PROFESOR, alese într-o fereastră cu căutare (pe server, după denumire).

import { api } from '@/lib/api'
import { MATERII_PROFESOR } from '@/lib/alegeri'
import AlegeMultiplu from './AlegeMultiplu'

export const materie = (m) => ({ id: m.id, nume: m.denumire })

export default function AlegeMaterii({ alese, onChange, name, indiciu }) {
  return (
    <AlegeMultiplu eticheta="Materii predate" buton="Adaugă materie" titlu="Materiile pe care le predai" etichetaCautare="Caută o materie"
      maxim={MATERII_PROFESOR} alese={alese} onChange={onChange} name={name} indiciu={indiciu}
      cauta={(q) => api('/materii' + (q.trim().length > 1 ? `?q=${encodeURIComponent(q.trim())}` : '')).then((r) => ({ elemente: r.elemente.map(materie) }))} />
  )
}
