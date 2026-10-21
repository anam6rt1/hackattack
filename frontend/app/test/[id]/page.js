'use client'
// /test/7 : testul Lecției 7 (întrebări din toate capitolele și fișierele ei)

import { useParams } from 'next/navigation'
import { usePagina } from '@/lib/sesiune'
import IntrebareNivel from '@/components/IntrebareNivel'
import Incarcare from '@/components/Incarcare'

export default function PaginaTest() {
  const { id } = useParams()
  const elev = usePagina('elev')
  if (!elev) return <Incarcare />
  return <IntrebareNivel parcursId={Number(id)} />
}
