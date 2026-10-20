'use client'
// Pagina de start: vizitatorii văd prezentarea cu intrarea în cont,
// iar cei logați își văd panoul (elev, profesor sau administrator).

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { adresaInapoi, profesorAprobat, useSesiune } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'
import Aterizare from '@/components/acasa/Aterizare'
import AcasaElev from '@/components/acasa/AcasaElev'
import AcasaProfesor from '@/components/acasa/AcasaProfesor'
import AcasaAdmin from '@/components/acasa/AcasaAdmin'

export default function Acasa() {
  const { utilizator } = useSesiune()
  const router = useRouter()
  const neaprobat = utilizator?.rol === 'profesor' && !profesorAprobat(utilizator)

  // Venit dintr-un link (de ex. codul QR al unei camere) înainte de a intra în cont: după intrare, înapoi acolo.
  const [inapoi] = useState(() => (typeof window === 'undefined' ? null : adresaInapoi(new URLSearchParams(window.location.search).get('inapoi'))))

  useEffect(() => {
    if (neaprobat) router.replace('/in-asteptare')
    else if (utilizator && inapoi) router.replace(inapoi)
  }, [neaprobat, utilizator, inapoi, router])

  if (utilizator === undefined || neaprobat || (utilizator && inapoi)) return <Incarcare />
  if (utilizator === null) return <Aterizare />
  if (utilizator.rol === 'admin') return <AcasaAdmin utilizator={utilizator} />
  if (utilizator.rol === 'profesor') return <AcasaProfesor utilizator={utilizator} />
  return <AcasaElev utilizator={utilizator} />
}
