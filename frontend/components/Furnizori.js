'use client'
// Contextele comune tuturor paginilor: sesiunea și tema.

import { Fragment, useState } from 'react'
import { SesiuneProvider, useSesiune } from '@/lib/sesiune'
import { TemaProvider } from '@/components/Tema'

// Next.js (cacheComponents) nu demontează paginile la navigare, doar le ascunde, cu tot cu starea lor. Când se schimbă contul
// (ieșire, intrare cu alt cont), paginile sunt construite din nou, ca nimic din contul anterior (lecția deschisă, filtre,
// limite) să nu rămână pe ecran. Prima încărcare a sesiunii nu le reconstruiește.
function PeCont({ children }) {
  const { utilizator } = useSesiune()
  const id = utilizator === undefined ? undefined : (utilizator?.id ?? null)
  const [cont, setCont] = useState({ id: undefined, cheie: 0 })
  if (id !== undefined && id !== cont.id) setCont({ id, cheie: cont.id === undefined ? cont.cheie : cont.cheie + 1 })
  return <Fragment key={cont.cheie}>{children}</Fragment>
}

export default function Furnizori({ children }) {
  return (
    <SesiuneProvider>
      <TemaProvider>
        <PeCont>{children}</PeCont>
      </TemaProvider>
    </SesiuneProvider>
  )
}
