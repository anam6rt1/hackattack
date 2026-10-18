'use client'
// Ține tema la zi: în modul „Automat” o recalculează la fiecare 5 minute (zi → noapte),
// iar pentru utilizatorii logați sincronizează alegerea cu profilul (merge pe orice dispozitiv).
// Tot aici stau stilul desenelor (Acvatic / Realist / Pixel) și avatarul din profil, salvate la fel.

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { api } from '@/lib/api'
import { useSesiune } from '@/lib/sesiune'
import { CHEIE, CHEIE_STIL, aplicaStil, aplicaTema, citestePreferinta, citesteStil, interfataDinStil, stilDinInterfata } from '@/lib/tema-sezon'

const Context = createContext(null)
const CHEIE_AVATAR = 'bac_avatar'

function salveazaLocal(cheie, valoare) {
  try {
    localStorage.setItem(cheie, valoare)
  } catch {}
}

function citesteAvatar() {
  try {
    return localStorage.getItem(CHEIE_AVATAR) || null
  } catch {
    return null
  }
}

export function TemaProvider({ children }) {
  const { utilizator } = useSesiune()
  // Tema și stilul sunt deja puse pe <html> de scriptul din <head>; aici doar ținem minte alegerea.
  const [preferinta, setPreferinta] = useState(() => (typeof window === 'undefined' ? 'auto' : citestePreferinta()))
  const [stil, setStil] = useState(() => (typeof window === 'undefined' ? 'acvatic' : citesteStil()))
  const [avatar, setAvatar] = useState(() => (typeof window === 'undefined' ? null : citesteAvatar()))

  const aplica = useCallback((p) => {
    setPreferinta(p)
    aplicaTema(p)
    salveazaLocal(CHEIE, p)
  }, [])

  const aplicaStilul = useCallback((s) => {
    setStil(s)
    aplicaStil(s)
    salveazaLocal(CHEIE_STIL, s)
  }, [])

  const aplicaAvatarul = useCallback((a) => {
    setAvatar(a)
    if (a) salveazaLocal(CHEIE_AVATAR, a)
    else
      try {
        localStorage.removeItem(CHEIE_AVATAR)
      } catch {}
  }, [])

  // Preferințele salvate în profil au prioritate față de cele din browser.
  useEffect(() => {
    if (!utilizator) return
    api('/utilizatori/me/preferinte')
      .then((p) => {
        aplica(p.mod === 'static' && p.tema ? p.tema : 'auto')
        const s = stilDinInterfata(p.interfata)
        if (s) aplicaStilul(s)
        aplicaAvatarul(p.avatar ?? null) // avatarul e al contului, nu al browserului
      })
      .catch(() => {})
  }, [utilizator, aplica, aplicaStilul, aplicaAvatarul])

  useEffect(() => {
    if (preferinta !== 'auto') return
    const t = setInterval(() => aplicaTema('auto'), 5 * 60 * 1000)
    return () => clearInterval(t)
  }, [preferinta])

  async function salveaza(corp) {
    if (utilizator) await api('/utilizatori/me/preferinte', { metoda: 'PUT', corp }).catch(() => {})
  }

  async function alege(p) {
    aplica(p)
    await salveaza(p === 'auto' ? { mod: 'auto' } : { mod: 'static', tema: p })
  }

  async function alegeStil(s) {
    // tranziție lină între stiluri, unde browserul o permite
    if (document.startViewTransition) document.startViewTransition(() => aplicaStilul(s))
    else aplicaStilul(s)
    await salveaza({ interfata: interfataDinStil(s) })
  }

  async function alegeAvatar(a) {
    aplicaAvatarul(a)
    await salveaza({ avatar: a })
  }

  return <Context.Provider value={{ preferinta, alege, stil, alegeStil, avatar, alegeAvatar }}>{children}</Context.Provider>
}

export const useTema = () => useContext(Context)
