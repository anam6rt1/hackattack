// Desenele hărții: insule, copaci, clădiri, valuri, bărci.
// Totul e generat din cod (SVG), deci nu ai nevoie de imagini.
// Același „seed” dă mereu aceeași formă de insulă.

function rng(seed) {
  return function () {
    seed |= 0
    seed = (seed + 0x6d2b79f5) | 0
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

// O formă rotunjită, neregulată (marginea insulei)
export function blob(cx, cy, rx, ry, seed, n = 14) {
  const r = rng(seed)
  const p = []
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2
    const k = 0.86 + r() * 0.24
    p.push([cx + Math.cos(a) * rx * k, cy + Math.sin(a) * ry * k])
  }
  const mid = (a, b) => [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2]
  const m = mid(p[0], p[1])
  let d = `M${m[0].toFixed(1)} ${m[1].toFixed(1)}`
  for (let i = 1; i <= n; i++) {
    const a = p[i % n]
    const q = mid(a, p[(i + 1) % n])
    d += ` Q${a[0].toFixed(1)} ${a[1].toFixed(1)} ${q[0].toFixed(1)} ${q[1].toFixed(1)}`
  }
  return d + 'Z'
}

function copac(x, y, s) {
  return `<g><rect x="${x - 1.5 * s}" y="${y}" width="${3 * s}" height="${7 * s}" fill="#7a4a22"/><circle cx="${x}" cy="${y - 2 * s}" r="${8 * s}" fill="#2f8a3a"/><circle cx="${x - 2 * s}" cy="${y - 4 * s}" r="${4 * s}" fill="#45a84c"/></g>`
}
function brad(x, y, s) {
  return `<g><rect x="${x - 1.5 * s}" y="${y}" width="${3 * s}" height="${5 * s}" fill="#7a4a22"/><path d="M${x} ${y - 20 * s}L${x + 9 * s} ${y + 1}L${x - 9 * s} ${y + 1}Z" fill="#256f36"/><path d="M${x} ${y - 20 * s}L${x + 4 * s} ${y - 8 * s}L${x - 5 * s} ${y - 6 * s}Z" fill="#368a44"/></g>`
}

const SEMNE = {
  moara: (x, y) => `<g><path d="M${x - 10} ${y + 8}L${x - 7} ${y - 16}L${x + 7} ${y - 16}L${x + 10} ${y + 8}Z" fill="#f4ead6" stroke="#8a5a2b" stroke-width="2"/><path d="M${x - 9} ${y - 16}L${x} ${y - 25}L${x + 9} ${y - 16}Z" fill="#c4473a"/><g stroke="#6b3410" stroke-width="3" stroke-linecap="round"><path d="M${x - 16} ${y - 34}L${x + 16} ${y - 2}"/><path d="M${x + 16} ${y - 34}L${x - 16} ${y - 2}"/></g><circle cx="${x}" cy="${y - 18}" r="3" fill="#6b3410"/></g>`,
  castel: (x, y) => `<g><rect x="${x - 18}" y="${y - 18}" width="36" height="26" fill="#ece3d2" stroke="#9b8d77" stroke-width="2"/><rect x="${x - 24}" y="${y - 28}" width="12" height="36" fill="#f4ecdd" stroke="#9b8d77" stroke-width="2"/><rect x="${x + 12}" y="${y - 28}" width="12" height="36" fill="#f4ecdd" stroke="#9b8d77" stroke-width="2"/><path d="M${x - 26} ${y - 28}L${x - 18} ${y - 40}L${x - 10} ${y - 28}ZM${x + 10} ${y - 28}L${x + 18} ${y - 40}L${x + 26} ${y - 28}Z" fill="#c4473a"/><rect x="${x - 5}" y="${y - 6}" width="10" height="14" rx="5" fill="#8a5a2b"/></g>`,
  turn: (x, y) => `<g><rect x="${x - 8}" y="${y - 30}" width="16" height="38" fill="#7d78a8" stroke="#4e4a75" stroke-width="2"/><path d="M${x - 11} ${y - 30}L${x} ${y - 44}L${x + 11} ${y - 30}Z" fill="#4e4a75"/><rect x="${x - 3}" y="${y - 20}" width="6" height="8" fill="#ffd25e"/><path d="M${x + 18} ${y - 46}l2 5 5 .5-4 3 1.2 5-4.2-2.7-4.2 2.7 1.2-5-4-3 5-.5z" fill="#ffd25e"/></g>`,
  casa: (x, y) => `<g><rect x="${x - 15}" y="${y - 12}" width="30" height="20" fill="#f6efe2" stroke="#8a5a2b" stroke-width="2"/><path d="M${x - 19} ${y - 12}L${x} ${y - 27}L${x + 19} ${y - 12}Z" fill="#c4473a"/><rect x="${x - 4}" y="${y - 3}" width="8" height="11" fill="#8a5a2b"/><rect x="${x + 7}" y="${y - 8}" width="6" height="6" fill="#7cc4ff"/></g>`,
  conac: (x, y) => `<g><rect x="${x - 24}" y="${y - 14}" width="48" height="22" fill="#f6efe2" stroke="#8a5a2b" stroke-width="2"/><path d="M${x - 28} ${y - 14}L${x} ${y - 30}L${x + 28} ${y - 14}Z" fill="#5a6fa8"/>${[-16, -6, 6, 16].map((d) => `<rect x="${x + d - 3}" y="${y - 9}" width="6" height="8" fill="#7cc4ff"/>`).join('')}</g>`,
  clopot: (x, y) => `<g><rect x="${x - 10}" y="${y - 22}" width="20" height="30" fill="#b9b3a6" stroke="#6f6a60" stroke-width="2"/><path d="M${x - 13} ${y - 22}L${x} ${y - 36}L${x + 13} ${y - 22}Z" fill="#6f6a60"/><path d="M${x - 5} ${y - 6}q5 -14 10 0z" fill="#7a6a48"/></g>`,
  far: (x, y) => `<g><path d="M${x - 9} ${y + 8}L${x - 6} ${y - 30}L${x + 6} ${y - 30}L${x + 9} ${y + 8}Z" fill="#fff" stroke="#c4473a" stroke-width="2"/><rect x="${x - 7}" y="${y - 18}" width="14" height="7" fill="#c4473a"/><rect x="${x - 7}" y="${y - 3}" width="14" height="7" fill="#c4473a"/><rect x="${x - 7}" y="${y - 38}" width="14" height="8" fill="#ffd25e" stroke="#6b3410" stroke-width="1.5"/></g>`,
}

export const marimeInsula = (mare) => ({ rx: 92 * (mare ? 1.1 : 1), ry: 58 * (mare ? 1.1 : 1) })

// O insulă completă. „mare” = cetate (puțin mai mare).
export function insulaSVG(ins, mare) {
  const { rx, ry } = marimeInsula(mare)
  const r = rng(ins.seed)
  let copaci = ''
  for (let i = 0; i < 4; i++) {
    const a = r() * Math.PI * 2
    const d = 0.45 + r() * 0.3
    const x = ins.x + Math.cos(a) * rx * d * 0.8
    const y = ins.y - 6 + Math.sin(a) * ry * d * 0.55
    const s = 0.8 + r() * 0.35
    if (Math.abs(x - ins.x) < 30 && Math.abs(y - ins.y) < 22) continue
    copaci += (ins.brazi ? brad : copac)(x, y, s)
  }
  return `
    <path d="${blob(ins.x, ins.y + 12, rx * 1.1, ry * 1.12, ins.seed)}" fill="#0b5c9c" opacity=".35"/>
    <path d="${blob(ins.x, ins.y, rx * 1.12, ry * 1.14, ins.seed + 3)}" fill="#7fd0f2" opacity=".45"/>
    <path class="tv-nisip" d="${blob(ins.x, ins.y, rx, ry, ins.seed)}" fill="#f1d9a0"/>
    <path d="${blob(ins.x, ins.y - 6, rx * 0.84, ry * 0.74, ins.seed + 7)}" fill="#6cc04a"/>
    <ellipse cx="${ins.x + 10}" cy="${ins.y + 4}" rx="${rx * 0.34}" ry="${ry * 0.2}" fill="#5aae3e" opacity=".8"/>
    <path d="M${ins.x - rx * 0.5} ${ins.y + ry * 0.45} Q${ins.x} ${ins.y + 4} ${ins.x + rx * 0.1} ${ins.y - 4}" fill="none" stroke="#e8c98a" stroke-width="5" stroke-linecap="round"/>
    ${copaci}
    ${SEMNE[ins.semn](ins.x + 4, ins.y - 2)}
    <path class="tv-inel" d="${blob(ins.x, ins.y, rx * 1.05, ry * 1.07, ins.seed)}" fill="none" stroke="transparent" stroke-width="6"/>`
}

// Marginea colorată + steagul proprietarului
export function proprietarSVG(ins, mare, culoare) {
  const { rx, ry } = marimeInsula(mare)
  return `
    <path d="${blob(ins.x, ins.y, rx * 1.07, ry * 1.1, ins.seed)}" fill="none" stroke="${culoare}" stroke-width="5" opacity=".95"/>
    <g class="tv-steag"><path d="M${ins.x + 46} ${ins.y - 4}V${ins.y - 50}" stroke="#5b3a1a" stroke-width="3"/><path d="M${ins.x + 47} ${ins.y - 50}l24 8-24 8z" fill="${culoare}" stroke="#fff" stroke-width="1.5"/></g>`
}

export function valuriSVG(w, h, n, seed) {
  const r = rng(seed)
  let s = ''
  for (let i = 0; i < n; i++) {
    const x = r() * w
    const y = r() * h
    s += `<path class="tv-val" style="animation-delay:-${(r() * 6).toFixed(1)}s" d="M${x.toFixed(0)} ${y.toFixed(0)} q8 -7 16 0 q8 -7 16 0" fill="none" stroke="#fff" stroke-opacity=".45" stroke-width="2.4" stroke-linecap="round"/>`
  }
  return s
}

export function barcaSVG(x, y, s, intarziere) {
  return `<g class="tv-barca" style="animation-delay:${intarziere}s"><path d="M${x - 22 * s} ${y}h${44 * s}l-${8 * s} ${10 * s}h-${28 * s}z" fill="#8a5a2b"/><path d="M${x} ${y - 2}V${y - 40 * s}" stroke="#6b3410" stroke-width="2.4"/><path d="M${x + 2} ${y - 38 * s}L${x + 2} ${y - 6 * s}L${x + 22 * s} ${y - 6 * s}Z" fill="#fff"/><path d="M${x - 2} ${y - 32 * s}L${x - 2} ${y - 6 * s}L${x - 16 * s} ${y - 6 * s}Z" fill="#f0f0f0"/><path d="M${x} ${y - 40 * s}l${8 * s} ${3 * s}-${8 * s} ${3 * s}z" fill="#e2463b"/></g>`
}

// Două insule mici, pentru antetul paginilor Profesori și Linia AI
export function decorSVG(seed) {
  return (
    valuriSVG(520, 220, 10, seed) +
    barcaSVG(80, 80, 0.8, 0) +
    insulaSVG({ x: 170, y: 150, seed, semn: 'casa' }) +
    insulaSVG({ x: 390, y: 110, seed: seed + 9, semn: 'far' })
  )
}
