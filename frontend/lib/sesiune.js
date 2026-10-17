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

// Paginile protejate: trimite vizitatorii la pagina de start, profesorii neaprobați la /in-asteptare
// și pe cei cu alt rol înapoi acasă. Întoarce utilizatorul doar când are voie să vadă pagina.
export function usePagina(...roluri) {
  const { utilizator } = useSesiune()
  const router = useRouter()
  const neaprobat = utilizator?.rol === 'profesor' && !profesorAprobat(utilizator)
  const permis = utilizator && !neaprobat && (roluri.length === 0 || roluri.includes(utilizator.rol))

  useEffect(() => {
    if (utilizator === undefined) return
    if (utilizator === null) router.replace('/')
    else if (neaprobat) router.replace('/in-asteptare')
    else if (!permis) router.replace('/')
  }, [utilizator, neaprobat, permis, router])

  return permis ? utilizator : null
}
