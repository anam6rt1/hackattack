'use client'
// Cine e conectat: un context comun pentru tot site-ul.
//   utilizator === undefined → încă verificăm
//   utilizator === null      → vizitator (nu e logat)
//   altfel                   → { id, nume, rol: 'elev' | 'profesor' | 'admin', stareProfesor, ... }

import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'

const Context = createContext(null)

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
      setUtilizator(u)
      return u
    },
    async inregistreazaElev(date) {
      const u = await api('/autentificare/inregistrare', { metoda: 'POST', corp: date })
      setUtilizator(u)
      return u
    },
    async inregistreazaProfesor(formular) {
      const u = await api('/autentificare/inregistrare-profesor', { metoda: 'POST', formular })
      setUtilizator(u)
      return u
    },
    async iese() {
      await api('/autentificare/iesire', { metoda: 'POST' }).catch(() => {})
      setUtilizator(null)
    },
  }
  return <Context.Provider value={valoare}>{children}</Context.Provider>
}

export function useSesiune() {
  return useContext(Context)
}

export const profesorAprobat = (u) => u?.rol === 'profesor' && u.stareProfesor === 'aprobat'

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
  const neaprobat = utilizator?.rol === 'profesor' && !profesorAprobat(utilizator)
  const permis = utilizator && !neaprobat && (roluri.length === 0 || roluri.includes(utilizator.rol))

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
