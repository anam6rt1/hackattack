'use client'
import { useEffect, useState } from 'react'
import { optimizeazaFotografie } from './optimizare-fisiere.mjs'

export function useFotografie(sursa, original) {
  const [rezultat, setRezultat] = useState(null)
  useEffect(() => {
    if (!sursa) return
    const controller = new AbortController()
    optimizeazaFotografie(sursa, { original, signal: controller.signal }).then(r => {
      if (!controller.signal.aborted) setRezultat({ ...r, sursa, original })
    }).catch(() => {})
    return () => controller.abort()
  }, [sursa, original])
  const gata = rezultat?.sursa === sursa && rezultat?.original === original
  return { fisier: gata ? rezultat.fisier : null, economisiti: gata ? rezultat.economisiti : 0, inLucru: !!sursa && !gata }
}
