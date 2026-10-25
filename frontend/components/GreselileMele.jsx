'use client'

import { useEffect, useState } from 'react'
import { api, dataOra } from '@/lib/api'
import Paginare from '@/components/Paginare'

export default function GreselileMele() {
  const [pagina, setPagina] = useState(1)
  const [versiune, setVersiune] = useState(0)
  const [date, setDate] = useState(null)
  const [eroare, setEroare] = useState('')
  const [intrebare, setIntrebare] = useState(null)
  const [rezultat, setRezultat] = useState(null)
  const [ocupat, setOcupat] = useState(false)
  useEffect(() => {
    let activ = true
    api(`/greseli?pagina=${pagina}`).then(r => { if (activ) { setDate(r); setEroare('') } }).catch(e => { if (activ) setEroare(e.message) })
    return () => { activ = false }
  }, [pagina, versiune])
  async function incepe(id) {
    setOcupat(true); setEroare(''); setRezultat(null)
    try { setIntrebare(await api(`/greseli/${id}/incepe`, { metoda: 'POST' })) } catch (e) { setEroare(e.message) } finally { setOcupat(false) }
  }
  async function raspunde(id) {
    setOcupat(true); setEroare('')
    try {
      const r = await api(`/greseli/${intrebare.intrebare.id}/raspuns`, { metoda: 'POST', corp: { token: intrebare.token, variantaId: id } })
      setRezultat(r); setVersiune(v => v + 1)
    } catch (e) { setEroare(e.message) } finally { setOcupat(false) }
  }
  return <section className="tv-cutie" aria-label="Greșelile mele">
    <h2>Greșelile mele</h2>
    <p>Întrebările greșite revin în zilele următoare. După două răspunsuri corecte consecutive ies din listă. Recapitularea nu acordă XP.</p>
    {eroare && <p className="tv-eroare" role="alert">{eroare}</p>}
    {intrebare ? <div className="tv-form">
      <h3>{intrebare.intrebare.text}</h3>
      {intrebare.intrebare.variante.map(v => <button className="tv-btn tv-gri" key={v.id} disabled={ocupat || !!rezultat} onClick={() => raspunde(v.id)}>{v.text}{rezultat?.variantaCorectaId === v.id ? ' ✓' : ''}</button>)}
      {rezultat && <p role="status">{rezultat.corect ? 'Corect!' : 'Mai exersează.'} {rezultat.explicatie} {rezultat.rezolvata ? 'Întrebarea a ieșit din listă.' : `Revine pe ${dataOra(rezultat.scadentaLa)}.`}</p>}
      <button className="tv-pastila" disabled={ocupat} onClick={() => { setIntrebare(null); setRezultat(null) }}>Înapoi la listă</button>
    </div> : <>
      {date && <p>{date.scadente} întrebări pregătite pentru recapitulare.</p>}
      {!date && !eroare && <p>Se încarcă întrebările…</p>}
      {date?.elemente.length === 0 && <p>Nu ai greșeli de recapitulat momentan.</p>}
      <ul className="tv-lista">{date?.elemente.map(g => <li key={g.intrebareId} className="tv-rand"><div><b>{g.text}</b><small>{g.lectie} · {g.corecteConsecutive}/2 răspunsuri corecte consecutive</small></div>
        {new Date(g.scadentaLa) <= new Date() ? <button className="tv-pastila" disabled={ocupat} onClick={() => incepe(g.intrebareId)}>Recapitulează</button> : <span>Revine pe {dataOra(g.scadentaLa)}</span>}
      </li>)}</ul>
      <Paginare pagina={pagina} totalPagini={Math.ceil((date?.total ?? 0) / 10)} onAlege={setPagina} />
    </>}
  </section>
}
