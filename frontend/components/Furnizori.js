'use client'
// Contextele comune tuturor paginilor: sesiunea și tema.

import { SesiuneProvider } from '@/lib/sesiune'
import { TemaProvider } from '@/components/Tema'

export default function Furnizori({ children }) {
  return (
    <SesiuneProvider>
      <TemaProvider>{children}</TemaProvider>
    </SesiuneProvider>
  )
}
