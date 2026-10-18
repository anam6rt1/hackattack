'use client'
// Fereastra care apare peste hartă: întrebarea, estimarea, rezultatul și finalul.

import { useEffect, useState } from 'react'
import confetti from 'canvas-confetti'
import Avatar from './Avatar'
import { insula } from './date-harta'

function Cronometru({ durata }) {
  const [ramas, setRamas] = useState(durata)
  useEffect(() => {
    const start = Date.now()
    const t = setInterval(() => setRamas(Math.max(0, durata - (Date.now() - start) / 1000)), 100)
    return () => clearInterval(t)
  }, [durata])
  const lung = 125.6
  return (
    <div className="tv-cronometru">
      <svg viewBox="0 0 48 48">
        <circle cx="24" cy="24" r="20" fill="#fff8dc" stroke="#f0e3c4" strokeWidth="5" />
        <circle
          cx="24" cy="24" r="20" fill="none" strokeWidth="5" strokeLinecap="round"
          stroke={ramas < 5 ? '#e2463b' : '#ffc61a'}
          strokeDasharray={lung}
          strokeDashoffset={lung * (1 - ramas / durata)}
        />
      </svg>
      <span>{Math.ceil(ramas)}</span>
    </div>
  )
}

function Duelisti({ stare }) {
  const juc = (id) => stare.jucatori.find((j) => j.id === id)
  const A = juc(stare.atac.atacator)
  const D = juc(stare.atac.aparator)
  if (!D) return null
  return (
    <div className="tv-vs">
      <div><Avatar nume={A.nume} culoare={A.culoare} marime={46} />{A.nume}</div>
      <em>VS</em>
      <div><Avatar nume={D.nume} culoare={D.culoare} marime={46} />{D.nume}</div>
    </div>
  )
}

export default function Fereastra({ stare, euId, esteGazda, onRaspunde, onEstimare, onJocNou }) {
  const [ales, setAles] = useState(null)
  const [an, setAn] = useState(1900)

  const juc = (id) => stare.jucatori.find((j) => j.id === id)
  const atac = stare.atac
  const ins = atac ? insula(atac.insula, stare.insule) : null
  const participanti = atac ? [atac.atacator, atac.aparator].filter(Boolean) : []
  const particip = participanti.includes(euId)
  const eDuel = atac && atac.aparator

  // Confetti la final
  useEffect(() => {
    if (stare.faza !== 'final') return
    const culori = ['#ffc61a', '#e5483b', '#1b8ad3', '#6cc04a', '#ffffff']
    confetti({ particleCount: 160, spread: 100, origin: { y: 0.6 }, colors: culori })
    const t = setTimeout(() => confetti({ particleCount: 120, spread: 140, origin: { y: 0.5 }, colors: culori }), 600)
    return () => clearTimeout(t)
  }, [stare.faza])

  // ---------- FINAL ----------
  if (stare.faza === 'final') {
    const numar = (id) => Object.values(stare.proprietari).filter((p) => p === id).length
    const clasament = stare.jucatori.slice().sort((a, b) => numar(b.id) - numar(a.id))
    const ordine = [clasament[1], clasament[0], clasament[2]].filter(Boolean)
    const inaltimi = [150, 110, 80]
    const culoriBloc = ['#ffc61a', '#7fb3e0', '#e8a46a']
    return (
      <div className="tv-fundal">
        <div className="tv-fereastra">
          <div className="tv-eticheta-sus">
            <b>Bătălia s-a terminat</b>
            <small>{clasament[0].id === euId ? 'Ai cucerit cele mai multe insule!' : `Câștigă ${clasament[0].nume}`}</small>
          </div>
          <div className="tv-podium">
            {ordine.map((j) => {
              const loc = clasament.indexOf(j)
              return (
                <div key={j.id} className="tv-loc">
                  <Avatar nume={j.nume} culoare={j.culoare} marime={loc === 0 ? 54 : 44} />
                  <b>{j.nume}{j.id === euId && ' (tu)'}</b>
                  <small>{numar(j.id)} insule · {numar(j.id) * 300} p</small>
                  <div className="tv-bloc" style={{ height: inaltimi[loc], background: culoriBloc[loc], animationDelay: (2 - loc) * 0.2 + 's' }}>
                    {loc + 1}
                  </div>
                </div>
              )
            })}
          </div>
          {esteGazda && <button className="tv-btn" onClick={onJocNou}>Joacă din nou</button>}
        </div>
      </div>
    )
  }

  const titlu = eDuel ? `Duel: ${ins.nume}` : `Cucerește: ${ins.nume}`
  const subtitlu = eDuel
    ? atac.aparator === euId
      ? `${juc(atac.atacator).nume} te atacă!`
      : `${juc(atac.atacator).nume} atacă insula lui ${juc(atac.aparator).nume}`
    : `${juc(atac.atacator).nume} încearcă să cucerească insula`

  // ---------- ÎNTREBARE GRILĂ ----------
  if (stare.faza === 'intrebare') {
    const q = stare.intrebare
    const potRaspunde = particip && ales === null && !stare.raspunsuri[euId]
    return (
      <div className="tv-fundal">
        <div className="tv-fereastra">
          <div className="tv-eticheta-sus"><b>{titlu}</b><small>{subtitlu}</small></div>
          <Cronometru durata={q.durata} />
          {eDuel && <Duelisti stare={stare} />}
          <p className="tv-intrebare">{q.t}</p>
          <div className="tv-variante">
            {q.v.map((v, i) => (
              <button
                key={i}
                className={`tv-varianta ${ales === i ? 'tv-aleasa' : ''}`}
                disabled={!potRaspunde}
                onClick={() => { setAles(i); onRaspunde(i) }}
              >
                <span className="tv-litera">{'ABCD'[i]}</span>
                <span>{v}</span>
              </button>
            ))}
          </div>
          <p className="tv-stare">
            {!particip
              ? 'Privești duelul. Nu poți răspunde la această întrebare.'
              : ales !== null || stare.raspunsuri[euId]
                ? 'Ai răspuns! Așteaptă rezultatul...'
                : 'Alege răspunsul corect.'}
          </p>
        </div>
      </div>
    )
  }

  // ---------- ESTIMARE ----------
  if (stare.faza === 'estimare') {
    const q = stare.intrebare
    const trimis = ales !== null || stare.raspunsuri[euId]
    return (
      <div className="tv-fundal">
        <div className="tv-fereastra">
          <div className="tv-eticheta-sus"><b>Estimare: {ins.nume}</b><small>Câștigă cine e mai aproape</small></div>
          <Cronometru durata={q.durata} />
          <Duelisti stare={stare} />
          <p className="tv-intrebare">{q.t}</p>
          {particip ? (
            <>
              <div className="tv-estimare">
                <button onClick={() => setAn(an - 1)} disabled={trimis} aria-label="Scade">−</button>
                <input type="number" inputMode="numeric" value={an} onChange={(e) => setAn(Number(e.target.value))} disabled={trimis} aria-label="Anul estimat" />
                <button onClick={() => setAn(an + 1)} disabled={trimis} aria-label="Crește">+</button>
              </div>
              <button className="tv-btn tv-aur" style={{ marginTop: 14 }} disabled={trimis} onClick={() => { setAles(an); onEstimare(an) }}>
                {trimis ? 'Trimis! Așteaptă...' : 'Trimite răspunsul'}
              </button>
            </>
          ) : (
            <p className="tv-stare">Privești estimarea.</p>
          )}
        </div>
      </div>
    )
  }

  // ---------- REZULTAT ----------
  if (stare.faza === 'rezultat') {
    const r = stare.rezultat
    const castigEu = r.castigator === euId
    const pierdEu = participanti.includes(euId) && !castigEu
    const tip = castigEu ? 'tv-bine' : pierdEu ? 'tv-rau' : 'tv-neutru'

    return (
      <div className="tv-fundal">
        <div className="tv-fereastra">
          <div className="tv-eticheta-sus"><b>{titlu}</b><small>{subtitlu}</small></div>
          {eDuel && <Duelisti stare={stare} />}
          <p className="tv-intrebare">{stare.intrebare.t}</p>

          {r.tip === 'grila' ? (
            <div className="tv-variante">
              {stare.intrebare.v.map((v, i) => {
                const cine = participanti.filter((id) => r.alegeri[id] === i)
                return (
                  <div key={i} className={`tv-varianta ${i === r.corect ? 'tv-corect' : cine.length ? 'tv-gresit' : ''}`}>
                    <span className="tv-litera">{'ABCD'[i]}</span>
                    <span style={{ flex: 1 }}>{v}</span>
                    {cine.map((id) => <Avatar key={id} nume={juc(id).nume} culoare={juc(id).culoare} marime={24} />)}
                  </div>
                )
              })}
            </div>
          ) : (
            <ul className="tv-estimari">
              <li className="tv-corect"><b>Răspunsul corect</b><span>{r.corect}</span></li>
              {participanti.map((id) => (
                <li key={id}>
                  <Avatar nume={juc(id).nume} culoare={juc(id).culoare} marime={26} />
                  <b>{juc(id).nume}</b>
                  <span>{r.alegeri[id] ?? '—'}</span>
                </li>
              ))}
            </ul>
          )}

          <div className={`tv-verdict ${tip}`}>{r.text}</div>
        </div>
      </div>
    )
  }

  return null
}
