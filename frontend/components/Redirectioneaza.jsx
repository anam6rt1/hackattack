'use client'
// O adresă veche a site-ului (din coduri QR, notificări sau semne de carte) duce la cea nouă, păstrând filtrele și ancora.
import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import Incarcare from '@/components/Incarcare'

export default function Redirectioneaza({ catre }) {
  const router = useRouter()
  useEffect(() => {
    const tinta = new URL(catre, window.location.origin)
    for (const [k, v] of new URLSearchParams(window.location.search)) if (!tinta.searchParams.has(k)) tinta.searchParams.set(k, v)
    router.replace(tinta.pathname + tinta.search + window.location.hash)
  }, [catre, router])
  return <Incarcare />
}
