'use client'
// Pagina /joc/ABCD : camera de joc Triviador.
//
// CUM FUNCȚIONEAZĂ:
// - Toți din cameră sunt conectați la același canal Supabase Realtime: camera-ABCD
// - GAZDA (cine a creat camera) joacă și ea, dar în plus e „arbitrul”: ține starea jocului,
//   verifică răspunsurile și trimite tuturor starea nouă după fiecare schimbare.
// - Ceilalți doar trimit acțiuni: „atac” (ce insulă aleg), „raspuns” (varianta), „estimare” (anul)
// - Răspunsul corect NU pleacă la jucători decât după ce se închide întrebarea.

import { useEffect, useRef, useState } from 'react'
import { useParams } from 'next/navigation'
import { supabase } from '@/lib/supabase'
import { INSULE, CETATI, CULORI, tinte, insula } from '../date-harta'
import { GRILA, ESTIMARI, amesteca } from '../intrebari'
import Harta from '../Harta'
import { PanouCamera, PanouJoc } from '../Panou'
import Fereastra from '../Fereastra'

const RUNDE = 4 // câte ture are fiecare jucător
const TIMP = 15 // secunde pentru o întrebare
const PAUZA = 2800 // cât stă rezultatul pe ecran (ms)
const TIMP_ALEGERE = 30000 // dacă nu alegi o insulă în 30 s, se trece la următorul

function idNou() {
  return Math.random().toString(36).slice(2, 10)
}

export default function Camera() {
  const params = useParams()
  const cod = String(params.cod).toUpperCase()

  // ---------- ce se vede pe ecran ----------
  const [pregatit, setPregatit] = useState(false)
  const [eu, setEu] = useState(null) // { id, nume, gazda }
  const [numeScris, setNumeScris] = useState('')
  const [conectat, setConectat] = useState(false)
  const [online, setOnline] = useState([]) // cine e în cameră acum
  const [stare, setStare] = useState(null) // starea jocului, primită de la gazdă
  const [link, setLink] = useState('')

  // ---------- memoria gazdei ----------
  const canalRef = useRef(null)
  const onlineRef = useRef([])
  const S = useRef(null) // starea „oficială” (doar la gazdă)
  const alegeri = useRef({}) // răspunsurile secrete din runda curentă
  const intrebareSecreta = useRef(null) // întrebarea cu răspunsul corect
  const pachete = useRef({ grila: [], estimari: [] })
  const timer = useRef(null)

  // PASUL 1: cine sunt eu
  useEffect(() => {
    setLink(window.location.origin + '/joc/' + cod)
    const salvat = sessionStorage.getItem('jucator')
    const jucator = salvat ? JSON.parse(salvat) : null
    if (sessionStorage.getItem('gazda-' + cod)) {
      setEu({ id: jucator?.id || idNou(), nume: jucator?.nume || '', gazda: true })
    } else if (jucator) {
      setEu(jucator)
    }
    setPregatit(true)
  }, [cod])

  // PASUL 2: conectarea la canal (după ce am un nume)
  useEffect(() => {
    if (!eu || !eu.nume) return

    const canal = supabase.channel('camera-' + cod, {
      config: { broadcast: { self: true }, presence: { key: eu.id } },
    })
    canalRef.current = canal

    canal.on('presence', { event: 'sync' }, () => {
      const lista = Object.values(canal.presenceState())
        .map((x) => x[0])
        .sort((a, b) => a.intrat - b.intrat)
      onlineRef.current = lista
      setOnline(lista)
    })

    // Cine intră după start primește starea curentă de la gazdă
    canal.on('presence', { event: 'join' }, () => {
      if (eu.gazda && S.current) publica()
    })

    canal.on('broadcast', { event: 'stare' }, ({ payload }) => setStare(payload))

    // Acțiunile jucătorilor (contează doar pentru gazdă)
    canal.on('broadcast', { event: 'actiune' }, ({ payload }) => {
      if (eu.gazda) primesteActiune(payload)
    })

    canal.subscribe(async (status) => {
      if (status === 'SUBSCRIBED') {
        await canal.track({ id: eu.id, nume: eu.nume, gazda: !!eu.gazda, intrat: Date.now() })
        setConectat(true)
      }
    })

    return () => {
      clearTimeout(timer.current)
      supabase.removeChannel(canal)
    }
  }, [eu, cod])

  // ---------- trimitere ----------
  function trimite(event, payload) {
    canalRef.current?.send({ type: 'broadcast', event, payload })
  }
  function publica() {
    trimite('stare', JSON.parse(JSON.stringify(S.current)))
  }
  function actiune(tip, date) {
    trimite('actiune', { tip, id: eu.id, ...date })
  }

  // =====================================================================
  //   LOGICA JOCULUI — rulează DOAR la gazdă
  // =====================================================================

  const jurnal = (jucatorId, text) => {
    const j = S.current.jucatori.find((x) => x.id === jucatorId)
    S.current.jurnal = [{ culoare: j ? j.culoare : '#1b8ad3', text }, ...S.current.jurnal].slice(0, 8)
  }
  const cetatiOcupate = () => S.current.jucatori.map((j) => j.cetate)
  const numeDe = (id) => S.current.jucatori.find((j) => j.id === id)?.nume

  function porneste() {
    const lista = onlineRef.current.slice(0, 3)
    const jucatori = lista.map((j, i) => ({ id: j.id, nume: j.nume, culoare: CULORI[i], cetate: CETATI[i] }))
    const proprietari = {}
    INSULE.forEach((ins) => (proprietari[ins.id] = null))
    jucatori.forEach((j) => (proprietari[j.cetate] = j.id))
    pachete.current = { grila: amesteca(GRILA), estimari: amesteca(ESTIMARI) }
    S.current = {
      faza: 'alegere', jucatori, proprietari, tura: 0, runda: 1,
      jurnal: [], atac: null, intrebare: null, raspunsuri: {}, rezultat: null, nr: 0,
    }
    jurnal(jucatori[0].id, 'Bătălia a început!')
    incepeTura()
  }

  function incepeTura() {
    clearTimeout(timer.current)
    const s = S.current
    if (s.runda > RUNDE) {
      s.faza = 'final'
      s.atac = null
      publica()
      return
    }
    s.faza = 'alegere'
    s.atac = null
    s.rezultat = null
    const curent = s.jucatori[s.tura]
    if (tinte(s.proprietari, curent.id, cetatiOcupate()).length === 0) {
      jurnal(curent.id, `${curent.nume} nu are ce ataca`)
      publica()
      timer.current = setTimeout(urmatorul, 1500)
      return
    }
    publica()
    timer.current = setTimeout(() => {
      jurnal(curent.id, `${curent.nume} n-a ales nimic la timp`)
      urmatorul()
    }, TIMP_ALEGERE)
  }

  function urmatorul() {
    const s = S.current
    s.tura++
    if (s.tura >= s.jucatori.length) {
      s.tura = 0
      s.runda++
    }
    incepeTura()
  }

  function primesteActiune(a) {
    const s = S.current
    if (!s) return

    if (a.tip === 'atac' && s.faza === 'alegere') {
      const curent = s.jucatori[s.tura]
      if (a.id !== curent.id) return
      if (!tinte(s.proprietari, curent.id, cetatiOcupate()).includes(a.insula)) return
      s.atac = { atacator: curent.id, aparator: s.proprietari[a.insula], insula: a.insula }
      pornesteGrila()
      return
    }

    const participanti = s.atac ? [s.atac.atacator, s.atac.aparator].filter(Boolean) : []
    if (!participanti.includes(a.id) || alegeri.current[a.id] !== undefined) return

    if (a.tip === 'raspuns' && s.faza === 'intrebare') {
      alegeri.current[a.id] = a.varianta
      s.raspunsuri[a.id] = true
      publica()
      if (participanti.every((id) => alegeri.current[id] !== undefined)) inchideGrila()
    }

    if (a.tip === 'estimare' && s.faza === 'estimare') {
      alegeri.current[a.id] = Number(a.valoare)
      s.raspunsuri[a.id] = true
      publica()
      if (participanti.every((id) => alegeri.current[id] !== undefined)) inchideEstimare()
    }
  }

  function pornesteGrila() {
    clearTimeout(timer.current)
    const s = S.current
    if (!pachete.current.grila.length) pachete.current.grila = amesteca(GRILA)
    const q = pachete.current.grila.pop()
    intrebareSecreta.current = q
    alegeri.current = {}
    s.nr++
    s.intrebare = { nr: s.nr, t: q.t, v: q.v, durata: TIMP } // fără răspunsul corect!
    s.raspunsuri = {}
    s.faza = 'intrebare'
    publica()
    timer.current = setTimeout(inchideGrila, (TIMP + 1) * 1000)
  }

  function inchideGrila() {
    clearTimeout(timer.current)
    const s = S.current
    if (s.faza !== 'intrebare') return
    const q = intrebareSecreta.current
    const { atacator, aparator, insula: insId } = s.atac
    const nume = insula(insId).nume
    const corectA = alegeri.current[atacator] === q.c
    const corectD = aparator ? alegeri.current[aparator] === q.c : false

    let castigator = null
    let text = ''
    let urmeazaEstimare = false

    if (!aparator) {
      if (corectA) {
        castigator = atacator
        text = `Corect! ${numeDe(atacator)} a cucerit ${nume}.`
      } else {
        text = `Greșit. ${nume} rămâne liberă.`
      }
    } else if (corectA && corectD) {
      urmeazaEstimare = true
      text = 'Amândoi ați răspuns corect! Urmează întrebarea de estimare.'
    } else if (corectA) {
      castigator = atacator
      text = `${numeDe(atacator)} câștigă duelul și cucerește ${nume}!`
    } else {
      castigator = aparator
      text = `${numeDe(aparator)} a apărat ${nume}.`
    }

    if (castigator === atacator) {
      s.proprietari[insId] = atacator
      jurnal(atacator, `${numeDe(atacator)} a cucerit ${nume}`)
    } else if (!urmeazaEstimare) {
      jurnal(aparator || atacator, aparator ? `${numeDe(aparator)} a apărat ${nume}` : `${numeDe(atacator)} a greșit la ${nume}`)
    }

    s.rezultat = { tip: 'grila', corect: q.c, alegeri: { ...alegeri.current }, castigator, text }
    s.faza = 'rezultat'
    publica()
    timer.current = setTimeout(urmeazaEstimare ? pornesteEstimare : urmatorul, PAUZA)
  }

  function pornesteEstimare() {
    const s = S.current
    if (!pachete.current.estimari.length) pachete.current.estimari = amesteca(ESTIMARI)
    const e = pachete.current.estimari.pop()
    intrebareSecreta.current = e
    alegeri.current = {}
    s.nr++
    s.intrebare = { nr: s.nr, t: e.t, durata: TIMP }
    s.raspunsuri = {}
    s.rezultat = null
    s.faza = 'estimare'
    publica()
    timer.current = setTimeout(inchideEstimare, (TIMP + 1) * 1000)
  }

  function inchideEstimare() {
    clearTimeout(timer.current)
    const s = S.current
    if (s.faza !== 'estimare') return
    const e = intrebareSecreta.current
    const { atacator, aparator, insula: insId } = s.atac
    const nume = insula(insId).nume
    const dist = (id) => (alegeri.current[id] === undefined ? Infinity : Math.abs(alegeri.current[id] - e.a))
    // La egalitate perfectă, apărătorul păstrează insula
    const castigator = dist(atacator) < dist(aparator) ? atacator : aparator

    let text
    if (castigator === atacator) {
      s.proprietari[insId] = atacator
      jurnal(atacator, `${numeDe(atacator)} a cucerit ${nume} la estimare`)
      text = `${numeDe(atacator)} a fost mai aproape și cucerește ${nume}!`
    } else {
      jurnal(aparator, `${numeDe(aparator)} a apărat ${nume} la estimare`)
      text = `${numeDe(aparator)} a fost mai aproape și păstrează ${nume}.`
    }
    s.rezultat = { tip: 'estimare', corect: e.a, alegeri: { ...alegeri.current }, castigator, text }
    s.faza = 'rezultat'
    publica()
    timer.current = setTimeout(urmatorul, PAUZA + 800)
  }

  // =====================================================================

  function intraCuNume(e) {
    e.preventDefault()
    const nume = numeScris.trim().slice(0, 16)
    if (!nume) return
    const j = { id: eu?.id || idNou(), nume, gazda: !!eu?.gazda }
    sessionStorage.setItem('jucator', JSON.stringify({ id: j.id, nume: j.nume }))
    setEu(j)
  }

  // ---------- ce afișăm ----------

  if (!pregatit) return <main className="tv-pagina" />

  if (!eu || !eu.nume) {
    return (
      <main className="tv-pagina tv-centru">
        <form onSubmit={intraCuNume} className="tv-card tv-formular">
          <div className="tv-mic">Intri în camera</div>
          <div className="tv-cod">{cod}</div>
          <input
            className="tv-camp"
            value={numeScris}
            onChange={(e) => setNumeScris(e.target.value)}
            maxLength={16}
            placeholder="Numele tău"
            autoFocus
          />
          <button type="submit" className="tv-btn">Intră pe hartă</button>
        </form>
      </main>
    )
  }

  if (!conectat) {
    return <main className="tv-pagina tv-centru"><p className="tv-asteapta">Se conectează...</p></main>
  }

  // Înainte de start: arătăm pe hartă ce cetate primește fiecare
  const previzualizare = {}
  online.slice(0, 3).forEach((j, i) => (previzualizare[CETATI[i]] = j.id))
  const jucatoriPrevizualizare = online.slice(0, 3).map((j, i) => ({ ...j, culoare: CULORI[i] }))

  const inJoc = !!stare
  const curent = inJoc ? stare.jucatori[stare.tura] : null
  const tinteleMele =
    inJoc && stare.faza === 'alegere' && curent?.id === eu.id
      ? tinte(stare.proprietari, eu.id, stare.jucatori.map((j) => j.cetate))
      : []

  return (
    <main className="tv-pagina">
      <div className="tv-joc">
        <Harta
          proprietari={inJoc ? stare.proprietari : previzualizare}
          jucatori={inJoc ? stare.jucatori : jucatoriPrevizualizare}
          cetati={inJoc ? stare.jucatori.map((j) => j.cetate) : CETATI.slice(0, online.length)}
          tinte={tinteleMele}
          atacata={inJoc && stare.atac ? stare.atac.insula : null}
          onAlege={(id) => actiune('atac', { insula: id })}
        />
        <aside className="tv-lateral">
          {inJoc ? (
            <PanouJoc stare={stare} euId={eu.id} runde={RUNDE} />
          ) : (
            <PanouCamera cod={cod} link={link} online={online} euId={eu.id} esteGazda={eu.gazda} onStart={porneste} />
          )}
        </aside>
      </div>

      {inJoc && ['intrebare', 'estimare', 'rezultat', 'final'].includes(stare.faza) && (
        <Fereastra
          key={stare.faza + '-' + (stare.intrebare?.nr || 0)}
          stare={stare}
          euId={eu.id}
          esteGazda={eu.gazda}
          onRaspunde={(v) => actiune('raspuns', { varianta: v })}
          onEstimare={(v) => actiune('estimare', { valoare: v })}
          onJocNou={porneste}
        />
      )}
    </main>
  )
}
