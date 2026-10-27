'use client'
// Cine e conectat: un context comun pentru tot site-ul.
//   utilizator === undefined → încă verificăm
//   utilizator === null      → vizitator (nu e logat)
//   altfel                   → { id, nume, rol: 'elev' | 'profesor' | 'organizatie' | 'admin', stareProfesor, organizatii, ... }
// „organizatie” = administratorul unei organizații; predă și el, deci poate tot ce poate un profesor.

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'

const Context = createContext(null)
// Lecțiile descărcate și rezultatele offline aparțin unui singur cont: alt cont pe același dispozitiv le golește.
async function verificaOffline(u) {
  const { proprietar, golesteTot } = await import('./offline/depozit')
  const p = await proprietar()
  if (p !== null && p !== u.id) await golesteTot()
}

/** Înainte de ieșire: rezultatele offline netrimise se trimit; dacă nu se poate, elevul alege dacă le pierde. */
async function trimiteInainteDeIesire() {
  const { areDateOffline } = await import('./offline/depozit')
  if (!areDateOffline()) return true
  const { actiuniInAsteptare, sincronizeaza } = await import('./offline/sincronizare')
  if ((await actiuniInAsteptare()).length) await sincronizeaza().catch(() => {})
  const ramase = (await actiuniInAsteptare()).length
  return !ramase || window.confirm(`Ai ${ramase} ${ramase === 1 ? 'rezultat offline netrimis' : 'rezultate offline netrimise'} (teste, simulări). Dacă ieși acum, se pierd. Ieși oricum?`)
}

export function SesiuneProvider({ children }) {
  const [utilizator, setUtilizator] = useState(undefined)

  const reincarca = useCallback(async () => {
    try {
      setUtilizator(await api('/utilizatori/me'))
    } catch {
      setUtilizator(null)
    }
  }, [])

  useEffect(() => {
    api('/utilizatori/me').then(setUtilizator, () => setUtilizator(null))
  }, [])

  const valoare = {
    utilizator,
    reincarca,
    async intra(identificator, parola) {
      const u = await api('/autentificare', { metoda: 'POST', corp: { identificator, parola } })
      await verificaOffline(u)
      setUtilizator(u)
      return u
    },
    // Înregistrarea nu intră în cont: întoarce { email, mesaj }, iar contul devine activ după confirmaEmail().
    inregistreazaElev(date) {
      return api('/autentificare/inregistrare', { metoda: 'POST', corp: date })
    },
    inregistreazaProfesor(formular) {
      return api('/autentificare/inregistrare-profesor', { metoda: 'POST', formular })
    },
    async confirmaEmail(email, cod) {
      const u = await api('/autentificare/confirma-email', { metoda: 'POST', corp: { email, cod } })
      await verificaOffline(u)
      setUtilizator(u)
      return u
    },
    retrimiteConfirmare(email) {
      return api('/autentificare/retrimite-confirmare', { metoda: 'POST', corp: { email } })
    },
    async iese() {
      if (!(await trimiteInainteDeIesire())) return false
      await (await import('./offline/depozit')).golesteTot().catch(() => {})
      if (typeof navigator !== 'undefined' && 'serviceWorker' in navigator) {
        try {
          const reg = await navigator.serviceWorker.getRegistration('/')
          const s = await reg?.pushManager.getSubscription()
          if (s) { await api('/utilizatori/me/notificari/abonamente', { metoda: 'DELETE', corp: { endpoint: s.endpoint } }); await s.unsubscribe() }
          for (const n of await reg?.getNotifications?.() ?? []) n.close()
        } catch { /* Logout must remain available even if browser push cleanup fails. */ }
      }
      await api('/autentificare/iesire', { metoda: 'POST' }).catch(() => {})
      setUtilizator(null)
      return true
    },
  }
  return <Context.Provider value={valoare}>{children}</Context.Provider>
}

export function useSesiune() {
  return useContext(Context)
}

export const esteProfesor = (u) => u?.rol === 'profesor' || u?.rol === 'organizatie'
export const profesorAprobat = (u) => esteProfesor(u) && u.stareProfesor === 'aprobat'

// Unde se întoarce cineva după ce intră în cont (de ex. linkul unei camere Triviador deschis din codul QR).
// Doar adrese din site: „/joc/ABC234” da, „//alt-site.ro” sau „https://...” nu.
export function adresaInapoi(valoare) {
  return typeof valoare === 'string' && valoare.startsWith('/') && !valoare.startsWith('//') && !valoare.startsWith('/\\') ? valoare : null
}

// Adresa se ține în sessionStorage (tabul curent) înainte de redirecționare: la o navigare în site, pagina de start
// se poate randa înainte ca bara de adrese să arate ?inapoi=. Parametrul din adresă rămâne pentru linkurile deschise direct.
const CHEIE_INAPOI = 'markai-inapoi'

function retineInapoi(cale) {
  try {
    sessionStorage.setItem(CHEIE_INAPOI, cale)
  } catch {}
}

// Citită o singură dată, după intrarea în cont.
export function preiaInapoi() {
  let cale = null
  try {
    cale = sessionStorage.getItem(CHEIE_INAPOI)
    sessionStorage.removeItem(CHEIE_INAPOI)
  } catch {}
  return adresaInapoi(cale ?? new URLSearchParams(window.location.search).get('inapoi'))
}

// Paginile protejate: trimite vizitatorii la pagina de start (care îi readuce aici după intrarea în cont),
// profesorii neaprobați la /in-asteptare și pe cei cu alt rol înapoi acasă.
// Întoarce utilizatorul doar când are voie să vadă pagina.
export function usePagina(...roluri) {
  const { utilizator } = useSesiune()
  const router = useRouter()
  const neaprobat = esteProfesor(utilizator) && !profesorAprobat(utilizator)
  // Paginile de profesor sunt și ale administratorilor de organizații.
  const rolPermis = (rol) => roluri.includes(rol) || (rol === 'organizatie' && roluri.includes('profesor'))
  const permis = utilizator && !neaprobat && (roluri.length === 0 || rolPermis(utilizator.rol))

  useEffect(() => {
    if (utilizator === undefined) return
    if (utilizator === null) {
      const cale = window.location.pathname + window.location.search
      retineInapoi(cale)
      router.replace('/?inapoi=' + encodeURIComponent(cale))
    }
    else if (neaprobat) router.replace('/in-asteptare')
    else if (!permis) router.replace('/')
  }, [utilizator, neaprobat, permis, router])

  return permis ? utilizator : null
}
