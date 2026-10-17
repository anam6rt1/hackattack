'use client'
// Pagina de start: vizitatorii văd prezentarea cu intrarea în cont,
// iar cei logați își văd panoul (elev, profesor sau administrator).

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { profesorAprobat, useSesiune } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import Aterizare from '@/components/acasa/Aterizare'
import AcasaElev from '@/components/acasa/AcasaElev'
import AcasaProfesor from '@/components/acasa/AcasaProfesor'
import AcasaAdmin from '@/components/acasa/AcasaAdmin'

export default function Acasa() {
  const { utilizator } = useSesiune()
  const router = useRouter()
  const neaprobat = utilizator?.rol === 'profesor' && !profesorAprobat(utilizator)

  useEffect(() => {
    if (neaprobat) router.replace('/in-asteptare')
  }, [neaprobat, router])

  if (utilizator === undefined || neaprobat) return <Incarcare />
  if (utilizator === null) return <Aterizare />
  if (utilizator.rol === 'admin') return <AcasaAdmin utilizator={utilizator} />
  if (utilizator.rol === 'profesor') return <AcasaProfesor utilizator={utilizator} />
  return <AcasaElev utilizator={utilizator} />
}
