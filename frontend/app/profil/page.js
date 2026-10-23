'use client'
// /profil : avatarul și stilul site-ului (Acvatic, Realist, Pixel), salvate în cont. Elevii își văd și nivelul.

import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import ProfilTeme from '@/components/ProfilTeme'

const ROLURI = { elev: 'Elev', profesor: 'Profesor', admin: 'Administrator' }

export default function PaginaProfil() {
  const utilizator = usePagina()
  const elev = utilizator?.rol === 'elev'
  const [nivel, setNivel] = useState(undefined)

  useEffect(() => {
    if (elev) api('/progres').then((p) => setNivel(p.nivel)).catch(() => setNivel(null))
  }, [elev])

  if (!utilizator || (elev && nivel === undefined)) return <Incarcare />
  return <ProfilTeme nume={utilizator.nume} rol={ROLURI[utilizator.rol]} nivel={elev ? nivel : null} />
}
