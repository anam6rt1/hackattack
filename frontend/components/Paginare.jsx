'use client'
// Bara de pagini: ‹ 1 2 … 4 5 6 … 8 9 › — primele două pagini, ultimele două, cea dinainte și cea de după pagina curentă;
// „…” ține locul celor sărite și, apăsat, devine un câmp în care scrii pagina la care vrei să mergi.

import { useState } from 'react'

/**
 * Numerele de pagină de arătat, cu null unde sunt sărite pagini (acolo apare „…”). margine = câte pagini de la început și de la
 * sfârșit (2 în bara obișnuită, 1 în cea compactă, pentru locuri înguste).
 */
export function paginiVizibile(pagina, total, margine = 2) {
  const capete = [...Array(margine).keys()].flatMap((i) => [1 + i, total - i])
  const alese = new Set([...capete, pagina - 1, pagina, pagina + 1].filter((p) => p >= 1 && p <= total))
  const sortate = [...alese].sort((a, b) => a - b)
  const rezultat = []
  sortate.forEach((p, i) => {
    if (i > 0 && p - sortate[i - 1] > 1) rezultat.push(null)
    rezultat.push(p)
  })
  return rezultat
}

function Salt({ total, onAlege }) {
  const [deschis, setDeschis] = useState(false)
  const [text, setText] = useState('')
  if (!deschis)
    return (
      <button type="button" className="tv-filtru" aria-label="Alege pagina" title="Alege pagina" onClick={() => setDeschis(true)}>
        …
      </button>
    )
  const mergi = () => {
    const p = Math.trunc(Number(text))
    if (Number.isFinite(p) && text.trim() !== '') onAlege(Math.min(Math.max(p, 1), total))
    setDeschis(false)
    setText('')
  }
  return (
    <span className="tv-paginare-salt">
      <input type="number" className="tv-camp-text" min={1} max={total} aria-label={`Pagina (1–${total})`} autoFocus value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') mergi()
          if (e.key === 'Escape') { setDeschis(false); setText('') }
        }} />
      <button type="button" className="tv-filtru" onClick={mergi}>Du-mă</button>
    </span>
  )
}

/**
 * pagina: pagina curentă (de la 1); totalPagini: câte sunt; onAlege(p): cere pagina p. Fără bară când e o singură pagină.
 * compact: butoane mai mici și doar prima și ultima pagină la capete (‹ 1 … 4 5 6 … 9 ›), pentru coloane înguste.
 */
/** eticheta: numele barei pentru cititoarele de ecran, când o pagină are mai multe. */
export default function Paginare({ pagina, totalPagini, onAlege, compact = false, eticheta = 'Pagini' }) {
  // Și fără un total valid (NaN, undefined): nicio bară.
  if (!(totalPagini > 1)) return null
  return (
    <nav className={compact ? 'tv-paginare tv-paginare-compacta' : 'tv-paginare'} aria-label={eticheta}>
      <button type="button" className="tv-filtru" aria-label="Pagina anterioară" disabled={pagina <= 1} onClick={() => onAlege(pagina - 1)}>‹</button>
      {paginiVizibile(pagina, totalPagini, compact ? 1 : 2).map((p, i) =>
        p === null ? (
          <Salt key={`salt-${i}`} total={totalPagini} onAlege={onAlege} />
        ) : (
          <button key={p} type="button" className="tv-filtru" aria-label={`Pagina ${p}`} aria-current={p === pagina ? 'page' : undefined}
            onClick={() => p !== pagina && onAlege(p)}>
            {p}
          </button>
        ),
      )}
      <button type="button" className="tv-filtru" aria-label="Pagina următoare" disabled={pagina >= totalPagini} onClick={() => onAlege(pagina + 1)}>›</button>
    </nav>
  )
}
