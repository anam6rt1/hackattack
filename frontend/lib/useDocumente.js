'use client'
import { useEffect, useState } from 'react'
import { optimizeazaDocument } from './optimizare-document.mjs'

export function useDocumente(selectate, original) {
  const [rezultat, setRezultat] = useState(null)
  useEffect(() => {
    const controller = new AbortController()
    async function pregateste() {
      const items = []
      // One worker at a time bounds memory usage even for a large selection.
      for (const f of selectate) {
        items.push(await optimizeazaDocument(f, { original, signal: controller.signal }))
        if (controller.signal.aborted) return
        setRezultat({ sursa: selectate, original, items: [...items] })
      }
    }
    pregateste().catch(() => {})
    return () => controller.abort()
  }, [selectate, original])
  const items = rezultat?.sursa === selectate && rezultat?.original === original ? rezultat.items : []
  return { items, inLucru: items.length < selectate.length }
}
