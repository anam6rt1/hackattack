'use client'
import { useEffect, useRef, useState } from 'react'
import { incarcaFormular } from './incarcare'

export function useIncarcare() {
  const controller = useRef(null)
  const [progres, setProgres] = useState(null)
  useEffect(() => () => controller.current?.abort(), [])
  async function trimite(cale, formular) {
    if (controller.current) throw new Error('O încărcare este deja în curs.')
    const c = new AbortController()
    controller.current = c
    setProgres({ procent: 0 })
    try { return await incarcaFormular(cale, formular, { signal: c.signal, onProgress: setProgres }) }
    finally { if (controller.current === c) { controller.current = null; setProgres(null) } }
  }
  return { trimite, progres, anuleaza: () => controller.current?.abort() }
}
