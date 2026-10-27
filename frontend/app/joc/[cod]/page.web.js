'use client'
// Adresa veche, doar pe site (page.web.js nu intră în exportul static al aplicațiilor): duce la camera cu parametru.
import { useParams } from 'next/navigation'
import Redirectioneaza from '@/components/Redirectioneaza'
import { rute } from '@/lib/rute'

export default function AdresaVeche() {
  const { cod } = useParams()
  return <Redirectioneaza catre={rute.camera(cod)} />
}
