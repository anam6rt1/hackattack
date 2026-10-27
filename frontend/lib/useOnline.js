'use client'
// Dacă aplicația poate ajunge la server: rețea în browser (navigator.onLine) ȘI ultima cerere nu a eșuat din cauza rețelei.
// Al doilea contează pentru Wi-Fi fără internet sau un server oprit: dispozitivul pare online, dar lucrează ca offline
// (lib/api.js trece oricum pe lecțiile descărcate) — iar elevul trebuie să știe de ce.
import { useSyncExternalStore } from 'react'

let serverCazut = false
const ascultatori = new Set()

/** Apelat de lib/api.js după fiecare cerere: false = eroare de rețea, true = serverul a răspuns. */
export function raporteazaServer(raspunde) {
  if (serverCazut === !raspunde) return
  serverCazut = !raspunde
  for (const a of ascultatori) a()
}

const abonare = (anunta) => {
  ascultatori.add(anunta)
  window.addEventListener('online', anunta)
  window.addEventListener('offline', anunta)
  return () => { ascultatori.delete(anunta); window.removeEventListener('online', anunta); window.removeEventListener('offline', anunta) }
}

const stare = () => (!navigator.onLine ? 'offline' : serverCazut ? 'server' : 'online')

/** 'online' | 'offline' (fără rețea) | 'server' (rețea, dar serverul nu răspunde). */
export const useRetea = () => useSyncExternalStore(abonare, stare, () => 'online')
export const useOnline = () => useRetea() === 'online'
