'use client'
// /test?id=7 : testul Lecției 7 (întrebări din toate capitolele și fișierele ei)

import { useSearchParams } from 'next/navigation'
import { usePagina } from '@/lib/sesiune'
import IntrebareNivel from '@/components/IntrebareNivel'
import Incarcare from '@/components/Incarcare'

export default function PaginaTest() {
  const id = useSearchParams().get('id')
  const elev = usePagina('elev')
  if (!elev) return <Incarcare />
  return <IntrebareNivel parcursId={Number(id)} />
}
