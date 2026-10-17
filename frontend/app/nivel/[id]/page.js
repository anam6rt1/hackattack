'use client'
// /nivel/12 : întrebarea de la finalul lecției 12

import { useParams } from 'next/navigation'
import { usePagina } from '@/lib/sesiune'
import IntrebareNivel from '@/components/IntrebareNivel'
import Incarcare from '@/components/Incarcare'

export default function PaginaNivel() {
  const { id } = useParams()
  const elev = usePagina('elev')
  if (!elev) return <Incarcare />
  return <IntrebareNivel cursId={Number(id)} />
}
