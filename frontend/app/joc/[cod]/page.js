'use client'
// Pagina /joc/ABCD : camera de joc Triviador.
//
// CUM FUNCȚIONEAZĂ:
// - Toți din cameră sunt conectați la hub-ul SignalR al API-ului, în grupul camerei ABCD (lib/canal-joc.js).
// - GAZDA (cine a creat camera) joacă și ea, dar în plus e „arbitrul”: ține starea jocului,
//   verifică răspunsurile și trimite tuturor starea nouă după fiecare schimbare.
// - Insulele sunt capitolele materiei alese, iar întrebările vin din API (generate din lecții).
// - Ceilalți doar trimit acțiuni: „atac” (ce insulă aleg), „raspuns” (varianta), „estimare” (numărul)
// - Răspunsul corect NU pleacă la jucători decât după ce se închide întrebarea.

import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'next/navigation'
import { api } from '@/lib/api'
import { usePagina } from '@/lib/sesiune'
import { deschideCanal } from '@/lib/canal-joc'
import Incarcare from '@/components/Incarcare'
import { INSULE, CETATI, CULORI, tinte, insula, insuleDinCursuri } from '../date-harta'
import { GRILA, ESTIMARI, amesteca } from '../intrebari'
import Harta from '../Harta'
import { PanouCamera, PanouJoc } from '../Panou'
import Fereastra from '../Fereastra'

const RUNDE = 4 // câte ture are fiecare jucător
const TIMP = 15 // secunde pentru o întrebare
const PAUZA = 2800 // cât stă rezultatul pe ecran (ms)
const TIMP_ALEGERE = 30000 // dacă nu alegi o insulă în 30 s, se trece la următorul

function citesteGazda(cod) {
  try {
    const v = sessionStorage.getItem('gazda-' + cod)
    return v ? JSON.parse(v) : null
  } catch {
    return null
  }
}

export default function Camera() {
  const params = useParams()
  const cod = String(params.cod).toUpperCase()
  const utilizator = usePagina('elev')

  // PASUL 1: cine sunt eu (din cont). Gazda e tabul care a creat camera (îi știe materia).
  const eu = useMemo(
    () => (utilizator ? { id: String(utilizator.id), nume: utilizator.nume, gazda: !!citesteGazda(cod) } : null),
    [utilizator, cod]
  )

  // ---------- ce se vede pe ecran ----------
  const [conectat, setConectat] = useState(false)
  const [eroare, setEroare] = useState('')
  const [online, setOnline] = useState([]) // cine e în cameră acum
  const [stare, setStare] = useState(null) // starea jocului, primită de la gazdă
  const [pregatit, setPregatit] = useState(false) // gazda: întrebările și insulele s-au încărcat

  // ---------- memoria gazdei ----------
  const canalRef = useRef(null)
  const onlineRef = useRef([])
  const S = useRef(null) // starea „oficială” (doar la gazdă)
  const lobby = useRef(null) // { faza: 'lobby', insule, materie } trimisă înainte de start
  const sursa = useRef({ grila: [], estimari: [] }) // toate întrebările materiei
  const folosite = useRef(new Set())
  const alegeri = useRef({}) // răspunsurile secrete din runda curentă
  const intrebareSecreta = useRef(null) // întrebarea cu răspunsul corect
  const timer = useRef(null)

  // Gazda încarcă materia: insulele (capitolele) și întrebările
  useEffect(() => {
    const gazda = eu?.gazda && citesteGazda(cod)
    if (!gazda) return

    Promise.all([api(`/materii/${gazda.materieId}`), api(`/materii/${gazda.materieId}/cursuri`), api(`/trivia/intrebari?materieId=${gazda.materieId}`)])
      .then(([materie, cursuri, pachet]) => {
        const grila = pachet.grila.map((q) => ({ id: q.id, cursId: q.cursId, t: q.text, v: q.variante, c: q.corect }))
        const estimari = pachet.estimari.map((q) => ({ id: q.id, cursId: q.cursId, t: q.text, a: q.raspuns }))
        // Dacă materia nu are încă întrebări, jocul folosește setul demonstrativ
        sursa.current = { grila: grila.length ? grila : GRILA.map((q, i) => ({ ...q, id: 'g' + i })), estimari: estimari.length ? estimari : ESTIMARI.map((q, i) => ({ ...q, id: 'e' + i })) }
        lobby.current = { faza: 'lobby', insule: insuleDinCursuri(cursuri.elemente), materie: materie.denumire }
        setStare(lobby.current)
        setPregatit(true)
        publica()
      })
      .catch((e) => setEroare(e.message))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eu, cod])

  // PASUL 2: conectarea la canalul camerei
  useEffect(() => {
    if (!eu) return
    const canal = deschideCanal(cod, eu.gazda, {
      onPrezenta: (lista) => {
        onlineRef.current = lista
        setOnline(lista)
      },
      // Cine intră primește starea curentă de la gazdă
      onIntrat: () => {
        if (eu.gazda) publica()
      },
      onStare: (payload) => setStare(payload),
      // Acțiunile jucătorilor (contează doar pentru gazdă)
      onActiune: (payload) => {
        if (eu.gazda) primesteActiune(payload)
      },
      onConectat: () => {
        setConectat(true)
        if (eu.gazda) publica() // cei intrați înaintea gazdei află materia și insulele
      },
      onDeconectat: () => setConectat(false),
      onEroare: () => setEroare('Nu ne-am putut conecta la cameră. Reîncarcă pagina.'),
    })
    canalRef.current = canal

    return () => {
      clearTimeout(timer.current)
      canal.inchide()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eu, cod])

  // ---------- trimitere ----------
  function trimite(event, payload) {
    canalRef.current?.trimite(event, payload)
  }
  function publica() {
    const s = S.current ?? lobby.current
    if (s) trimite('stare', JSON.parse(JSON.stringify(s)))
  }
  function actiune(tip, date) {
    trimite('actiune', { tip, id: eu.id, ...date })
  }

  // =====================================================================
  //   LOGICA JOCULUI — rulează DOAR la gazdă
  // =====================================================================

  const insuleJoc = () => S.current?.insule ?? lobby.current?.insule ?? INSULE
  const jurnal = (jucatorId, text) => {
    const j = S.current.jucatori.find((x) => x.id === jucatorId)
    S.current.jurnal = [{ culoare: j ? j.culoare : '#1b8ad3', text }, ...S.current.jurnal].slice(0, 8)
  }
  const cetatiOcupate = () => S.current.jucatori.map((j) => j.cetate)
  const numeDe = (id) => S.current.jucatori.find((j) => j.id === id)?.nume

  // O întrebare încă nefolosită, de preferat din lecțiile insulei atacate
  function alegeIntrebare(lista, cursIds) {
    let libere = lista.filter((q) => !folosite.current.has(q.id))
    if (!libere.length) {
      lista.forEach((q) => folosite.current.delete(q.id))
      libere = lista
    }
    const dinInsula = cursIds?.length ? libere.filter((q) => cursIds.includes(q.cursId)) : []
    const q = amesteca(dinInsula.length ? dinInsula : libere)[0]
    folosite.current.add(q.id)
    return q
  }

  function porneste() {
    const lista = onlineRef.current.slice(0, 3)
    const jucatori = lista.map((j, i) => ({ id: j.id, nume: j.nume, culoare: CULORI[i], cetate: CETATI[i] }))
    const proprietari = {}
    INSULE.forEach((ins) => (proprietari[ins.id] = null))
    jucatori.forEach((j) => (proprietari[j.cetate] = j.id))
    folosite.current = new Set()
    S.current = {
      faza: 'alegere', insule: insuleJoc(), materie: lobby.current?.materie, jucatori, proprietari, tura: 0, runda: 1,
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
      actualizeaza()
      return
    }
    s.faza = 'alegere'
    s.atac = null
    s.rezultat = null
    const curent = s.jucatori[s.tura]
    if (tinte(s.proprietari, curent.id, cetatiOcupate()).length === 0) {
      jurnal(curent.id, `${curent.nume} nu are ce ataca`)
      actualizeaza()
      timer.current = setTimeout(urmatorul, 1500)
      return
    }
    actualizeaza()
    timer.current = setTimeout(() => {
      jurnal(curent.id, `${curent.nume} n-a ales nimic la timp`)
      urmatorul()
    }, TIMP_ALEGERE)
  }

  // Gazda își vede starea direct; ceilalți o primesc prin canal
  function actualizeaza() {
    setStare(JSON.parse(JSON.stringify(S.current)))
    publica()
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
      actualizeaza()
      if (participanti.every((id) => alegeri.current[id] !== undefined)) inchideGrila()
    }

    if (a.tip === 'estimare' && s.faza === 'estimare') {
      alegeri.current[a.id] = Number(a.valoare)
      s.raspunsuri[a.id] = true
      actualizeaza()
      if (participanti.every((id) => alegeri.current[id] !== undefined)) inchideEstimare()
    }
  }

  function pornesteGrila() {
    clearTimeout(timer.current)
    const s = S.current
    const q = alegeIntrebare(sursa.current.grila, insula(s.atac.insula, s.insule)?.cursIds)
    intrebareSecreta.current = q
    alegeri.current = {}
    s.nr++
    s.intrebare = { nr: s.nr, t: q.t, v: q.v, durata: TIMP } // fără răspunsul corect!
    s.raspunsuri = {}
    s.faza = 'intrebare'
    actualizeaza()
    timer.current = setTimeout(inchideGrila, (TIMP + 1) * 1000)
  }

  function inchideGrila() {
    clearTimeout(timer.current)
    const s = S.current
    if (s.faza !== 'intrebare') return
    const q = intrebareSecreta.current
    const { atacator, aparator, insula: insId } = s.atac
    const nume = insula(insId, s.insule).nume
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
    actualizeaza()
    timer.current = setTimeout(urmeazaEstimare ? pornesteEstimare : urmatorul, PAUZA)
  }

  function pornesteEstimare() {
    const s = S.current
    const e = alegeIntrebare(sursa.current.estimari, insula(s.atac.insula, s.insule)?.cursIds)
    intrebareSecreta.current = e
    alegeri.current = {}
    s.nr++
    s.intrebare = { nr: s.nr, t: e.t, durata: TIMP }
    s.raspunsuri = {}
    s.rezultat = null
    s.faza = 'estimare'
    actualizeaza()
    timer.current = setTimeout(inchideEstimare, (TIMP + 1) * 1000)
  }

  function inchideEstimare() {
    clearTimeout(timer.current)
    const s = S.current
    if (s.faza !== 'estimare') return
    const e = intrebareSecreta.current
    const { atacator, aparator, insula: insId } = s.atac
    const nume = insula(insId, s.insule).nume
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
    actualizeaza()
    timer.current = setTimeout(urmatorul, PAUZA + 800)
  }

  // ---------- ce afișăm ----------

  if (eroare) return <Incarcare text={eroare} />
  if (!utilizator || !eu) return <Incarcare />
  if (!conectat) return <Incarcare text="Se conectează..." />

  const link = window.location.origin + '/joc/' + cod
  const inJoc = !!stare && stare.faza !== 'lobby'
  const insule = stare?.insule ?? INSULE

  // Înainte de start: arătăm pe hartă ce cetate primește fiecare
  const previzualizare = {}
  online.slice(0, 3).forEach((j, i) => (previzualizare[CETATI[i]] = j.id))
  const jucatoriPrevizualizare = online.slice(0, 3).map((j, i) => ({ ...j, culoare: CULORI[i] }))

  const curent = inJoc ? stare.jucatori[stare.tura] : null
  const tinteleMele =
    inJoc && stare.faza === 'alegere' && curent?.id === eu.id
      ? tinte(stare.proprietari, eu.id, stare.jucatori.map((j) => j.cetate))
      : []

  return (
    <main className="tv-pagina">
      <div className="tv-joc">
        <Harta
          insule={insule}
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
            <PanouCamera
              cod={cod} link={link} online={online} euId={eu.id} esteGazda={eu.gazda} onStart={porneste}
              insule={insule} materie={stare?.materie} gata={!eu.gazda || pregatit}
            />
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
