'use client'
// Alegerea a câteva elemente dintr-o listă lungă (materii, organizații): cele alese apar ca pastile cu „×”, iar butonul deschide
// o fereastră cu căutare și bife, cel mult `maxim` bifate. Cu `name`, alegerea pleacă și prin câmpuri ascunse (formulare FormData).
//   cauta(q) → Promise<{ elemente: [{ id, nume, detaliu? }], total? }>   (căutarea se face pe server)
//   alese: [{ id, nume, detaliu? }], onChange(alese)

import { useEffect, useId, useRef, useState } from 'react'

export default function AlegeMultiplu({ eticheta, buton, titlu, etichetaCautare, maxim, alese, onChange, cauta, name, indiciu, inFereastra, cheieCautare }) {
  const [deschis, setDeschis] = useState(false)
  const idTitlu = useId()
  const plin = alese.length >= maxim

  return (
    <fieldset className="tv-eticheta-camp" aria-labelledby={idTitlu}>
      <span id={idTitlu}>{eticheta} <small className="tv-indiciu">({alese.length} din {maxim})</small></span>
      {alese.length > 0 && (
        <ul className="tv-alese">
          {alese.map((a) => (
            <li key={a.id} className="tv-alegere">
              <span>{a.nume}{a.detaliu && <small> · {a.detaliu}</small>}</span>
              <button type="button" aria-label={`Scoate ${a.nume}`} onClick={() => onChange(alese.filter((x) => x.id !== a.id))}>×</button>
            </li>
          ))}
        </ul>
      )}
      <button type="button" className="tv-btn tv-gri tv-btn-adauga" onClick={() => setDeschis(true)}>
        {plin ? 'Schimbă alegerea' : `+ ${buton}`}
      </button>
      {indiciu && <small className="tv-indiciu">{indiciu}</small>}
      {name && alese.map((a) => <input key={a.id} type="hidden" name={name} value={a.id} />)}
      {deschis && (
        <Fereastra titlu={titlu} etichetaCautare={etichetaCautare} maxim={maxim} alese={alese} onChange={onChange} cauta={cauta}
          cheieCautare={cheieCautare} onInchide={() => setDeschis(false)}>{inFereastra}</Fereastra>
      )}
    </fieldset>
  )
}

function Fereastra({ titlu, etichetaCautare, maxim, alese, onChange, cauta, cheieCautare, onInchide, children }) {
  const [text, setText] = useState('')
  const [rezultat, setRezultat] = useState(null) // { elemente, total }
  const [eroare, setEroare] = useState(false)
  const camp = useRef(null)
  const idTitlu = useId()
  const plin = alese.length >= maxim

  useEffect(() => {
    camp.current?.focus()
    const tasta = (e) => e.key === 'Escape' && onInchide()
    window.addEventListener('keydown', tasta)
    return () => window.removeEventListener('keydown', tasta)
  }, [onInchide])

  // Căutarea pleacă la o scurtă pauză după ultima tastă; un răspuns vechi nu îl înlocuiește pe cel nou.
  useEffect(() => {
    let activ = true
    const t = setTimeout(() => {
      cauta(text).then((r) => activ && (setRezultat(r), setEroare(false))).catch(() => activ && (setRezultat({ elemente: [] }), setEroare(true)))
    }, text ? 250 : 0)
    return () => { activ = false; clearTimeout(t) }
    // `cauta` se schimbă la fiecare randare a părintelui; `cheieCautare` spune când se schimbă de fapt ce caută.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text, cheieCautare])

  function comuta(e) {
    onChange(alese.some((a) => a.id === e.id) ? alese.filter((a) => a.id !== e.id) : [...alese, e])
  }

  const elemente = rezultat?.elemente ?? []
  return (
    <div className="tv-fundal" onClick={onInchide}>
      <div className="tv-fereastra tv-fereastra-alegere" role="dialog" aria-modal="true" aria-labelledby={idTitlu} onClick={(e) => e.stopPropagation()}>
        <div className="tv-form">
          <h3 id={idTitlu} style={{ color: 'var(--maro)', fontSize: 22 }}>{titlu}</h3>
          <input ref={camp} className="tv-camp-text" type="search" value={text} onChange={(e) => setText(e.target.value)}
            // Fereastra stă în formularul paginii: Enter în căutare nu trebuie să-l trimită.
            onKeyDown={(e) => e.key === 'Enter' && e.preventDefault()}
            placeholder={etichetaCautare} aria-label={etichetaCautare} maxLength={100} />
          {children}
          <p className="tv-indiciu" aria-live="polite">
            {alese.length} din {maxim} alese{plin ? ' · ai ajuns la limită: debifează una ca să alegi alta.' : '.'}
          </p>
          <div className="tv-lista-alegere">
            {rezultat === null ? (
              <small className="tv-indiciu">Se încarcă...</small>
            ) : elemente.length === 0 ? (
              <small className="tv-indiciu">{eroare ? 'Lista nu s-a putut încărca. Încearcă din nou.' : 'Nimic nu se potrivește căutării.'}</small>
            ) : (
              elemente.map((e) => {
                const bifat = alese.some((a) => a.id === e.id)
                return (
                  <label key={e.id} className="tv-bifa" style={!bifat && plin ? { opacity: 0.5, cursor: 'not-allowed' } : undefined}>
                    <input type="checkbox" checked={bifat} disabled={!bifat && plin} onChange={() => comuta(e)} />
                    <span>{e.nume}{e.detaliu && <small className="tv-indiciu"> · {e.detaliu}</small>}</span>
                  </label>
                )
              })
            )}
          </div>
          {rezultat?.total > elemente.length && (
            <small className="tv-indiciu">Se văd primele {elemente.length} din {rezultat.total}. Scrie în căutare ca să le restrângi.</small>
          )}
          <button type="button" className="tv-btn" onClick={onInchide}>Gata</button>
        </div>
      </div>
    </div>
  )
}
