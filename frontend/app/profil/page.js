'use client'

import ProfilTeme from '@/components/ProfilTeme'
import { usePagina } from '@/lib/sesiune'
import Incarcare from '@/components/Incarcare'

export default function Profil() {
  const utilizator = usePagina('elev', 'profesor', 'admin')
  if (!utilizator) return <Incarcare />
  return <ProfilTeme nume={utilizator.nume} />
}
