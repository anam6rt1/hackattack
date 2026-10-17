'use client'
// Ține tema la zi: în modul „Automat” o recalculează la fiecare 5 minute (zi → noapte),
// iar pentru utilizatorii logați sincronizează alegerea cu profilul (merge pe orice dispozitiv).

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { useSesiune } from '@/lib/sesiune'
import { CHEIE, aplicaTema, citestePreferinta } from '@/lib/tema-sezon'

const Context = createContext(null)

export function TemaProvider({ children }) {
  const { utilizator } = useSesiune()
  // Tema e deja pusă pe <html> de scriptul din <head>; aici doar ținem minte alegerea.
  const [preferinta, setPreferinta] = useState(() => (typeof window === 'undefined' ? 'auto' : citestePreferinta()))

  const aplica = useCallback((p) => {
    setPreferinta(p)
    aplicaTema(p)
    try {
      localStorage.setItem(CHEIE, p)
    } catch {}
  }, [])

  // Preferința salvată în profil are prioritate față de cea din browser.
  useEffect(() => {
    if (!utilizator) return
    api('/utilizatori/me/preferinte')
      .then((p) => aplica(p.mod === 'static' && p.tema ? p.tema : 'auto'))
      .catch(() => {})
  }, [utilizator, aplica])

  useEffect(() => {
    if (preferinta !== 'auto') return
    const t = setInterval(() => aplicaTema('auto'), 5 * 60 * 1000)
    return () => clearInterval(t)
  }, [preferinta])

  async function alege(p) {
    aplica(p)
    if (utilizator) {
      await api('/utilizatori/me/preferinte', { metoda: 'PUT', corp: p === 'auto' ? { mod: 'auto' } : { mod: 'static', tema: p } }).catch(() => {})
    }
  }

  return <Context.Provider value={{ preferinta, alege }}>{children}</Context.Provider>
}

export const useTema = () => useContext(Context)
