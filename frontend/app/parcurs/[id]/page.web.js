'use client'
// Adresa veche, doar pe site (page.web.js nu intră în exportul static al aplicațiilor): duce la parcurs cu parametru.
import { useParams } from 'next/navigation'
import Redirectioneaza from '@/components/Redirectioneaza'
import { rute } from '@/lib/rute'

export default function AdresaVeche() {
  const { id } = useParams()
  return <Redirectioneaza catre={rute.parcurs(id)} />
}
