'use client'
import { useEffect, useState } from 'react'
import { api } from '@/lib/api'
export default function StatisticiIntrebari({ parcursId }) {
  const [date, setDate] = useState(null)
  const [pagina, setPagina] = useState(1)
  const [eroare, setEroare] = useState('')
  useEffect(() => {
    let activ = true
    api(`/parcursuri/${parcursId}/statistici-intrebari?pagina=${pagina}`).then(r => { if (activ) setDate(r) }).catch(e => { if (activ) setEroare(e.message) })
    return () => { activ = false }
  }, [parcursId, pagina])
  return <section className="tv-cutie"><h3>Statistica întrebărilor</h3><p className="tv-indiciu">Răspunsurile la testele lecției, de la activarea statisticilor. Întrebările cu cele mai multe dificultăți apar primele. Un elev poate avea mai multe încercări.</p>
    {date?.elemente.map(q => <p key={q.id}><a href={`#intrebare-${q.id}`} onClick={() => window.dispatchEvent(new CustomEvent('editeaza-intrebare', { detail: q.id }))}>{q.text}</a> · {q.raspunsuri} răspunsuri · {q.raspunsuri ? `${Math.round(q.corecte * 100 / q.raspunsuri)}% corecte` : 'Fără răspunsuri'}</p>)}
    {pagina > 1 && <button onClick={() => setPagina(pagina - 1)}>Înapoi</button>}{date && pagina * 20 < date.total && <button onClick={() => setPagina(pagina + 1)}>Înainte</button>}
    {eroare && <p role="alert">{eroare}</p>}
  </section>
}
