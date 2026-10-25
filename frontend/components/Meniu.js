'use client'
// Meniul de sus, comun pentru TOT site-ul (pus o singură dată în app/layout.tsx).
// Paginile din meniu depind de rol: elevii învață, profesorii își gestionează ședințele și conținutul,
// administratorul aprobă profesorii. Pe telefon, butonul „Meniu” deschide lista.

import { useEffect, useRef, useState } from 'react'
import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { esteProfesor, profesorAprobat, useSesiune } from '@/lib/sesiune'
import { useTema } from '@/components/Tema'
import { STILURI, TEME } from '@/lib/tema-sezon'
import { Avatar } from '@/components/ProfilTeme'
import './meniu.css'
import '@/app/setari/setari.css'

// Logo-ul TeleBac (emblema mică), desenat direct aici
const LOGO_SVG = `<svg viewBox="0 0 300 300" width="40" height="40" aria-hidden="true" style="max-width:100%;height:auto;overflow:visible"><defs><linearGradient id="tb-cer" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#172a6e"></stop><stop offset="0.55" stop-color="#3667c4"></stop><stop offset="1" stop-color="#8fd0f5"></stop></linearGradient><linearGradient id="tb-mare" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3aa2e6"></stop><stop offset="1" stop-color="#0d4b8f"></stop></linearGradient><linearGradient id="tb-pag" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fffcf4"></stop><stop offset="1" stop-color="#f0e0bf"></stop></linearGradient><linearGradient id="tb-inel" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1a5aa6"></stop><stop offset="1" stop-color="#0c3466"></stop></linearGradient><linearGradient id="tb-panglica" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#e8513f"></stop><stop offset="1" stop-color="#b8302a"></stop></linearGradient><radialGradient id="tb-aura"><stop offset="0" stop-color="#fff7c8" stop-opacity="0.95"></stop><stop offset="0.4" stop-color="#ffe9a0" stop-opacity="0.35"></stop><stop offset="1" stop-color="#ffe9a0" stop-opacity="0"></stop></radialGradient><clipPath id="tb-cerc"><circle cx="150" cy="150" r="116"></circle></clipPath><path id="tb-text-sus" d="M 27 150 A 123 123 0 0 1 273 150"></path><path id="tb-text-jos" d="M 16 150 A 134 134 0 0 0 284 150"></path></defs><circle cx="150" cy="150" r="146" fill="#ffffff"></circle><circle cx="150" cy="150" r="142" fill="url(#tb-inel)"></circle><circle cx="150" cy="150" r="142" fill="none" stroke="#ffc93d" stroke-width="3"></circle><circle cx="150" cy="150" r="118" fill="none" stroke="#ffc93d" stroke-width="3"></circle><path d="M30 159 L32 164 L37 166 L32 168 L30 173 L28 168 L23 166 L28 164 Z" fill="#ffc93d"></path><path d="M270 159 L272 164 L277 166 L272 168 L270 173 L268 168 L263 166 L268 164 Z" fill="#ffc93d"></path><g clip-path="url(#tb-cerc)"><rect width="300" height="300" fill="url(#tb-cer)"></rect><circle class="lp-stea-mica" cx="70" cy="70" r="1.6" fill="#ffffff" style="animation-delay:0s"></circle><circle class="lp-stea-mica" cx="96" cy="52" r="1.1" fill="#ffffff" style="animation-delay:0.45s"></circle><circle class="lp-stea-mica" cx="120" cy="88" r="1.3" fill="#ffffff" style="animation-delay:0.9s"></circle><circle class="lp-stea-mica" cx="186" cy="52" r="1.2" fill="#ffffff" style="animation-delay:1.35s"></circle><circle class="lp-stea-mica" cx="238" cy="120" r="1" fill="#ffffff" style="animation-delay:1.8s"></circle><circle class="lp-stea-mica" cx="62" cy="118" r="1" fill="#ffffff" style="animation-delay:2.25s"></circle><circle class="lp-stea-mica" cx="150" cy="44" r="1.4" fill="#ffffff" style="animation-delay:2.7s"></circle><path d="M84 84 A16 16 0 1 0 104 104 A12.5 12.5 0 1 1 84 84 Z" fill="#fbf3cf"></path><g class="lp-luceafar"><circle cx="214" cy="84" r="38" fill="url(#tb-aura)"></circle><path d="M214 60 L218.5 79.5 L238 84 L218.5 88.5 L214 108 L209.5 88.5 L190 84 L209.5 79.5 Z" fill="#fff6cf"></path><path d="M214 72 L216 82 L226 84 L216 86 L214 96 L212 86 L202 84 L212 82 Z" fill="#ffffff"></path></g><g class="lp-nor" fill="#ffffff" opacity="0.9"><path d="M40 150 q0 -12 14 -12 q4 -10 16 -8 q10 -6 18 4 q12 0 12 10 q0 8 -10 8 h-40 q-10 0 -10 -2 Z"></path></g><rect y="176" width="300" height="130" fill="url(#tb-mare)"></rect><g class="lp-val"><path d="M-40 178 q10 -5 20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0 t20 0" fill="none" stroke="#c8ecff" stroke-width="2.5"></path></g><g stroke="#ffffff" stroke-opacity="0.65" stroke-width="2.4" stroke-linecap="round" fill="none"><path class="lp-sclipire" d="M58 238 q6 -5 12 0 q6 5 12 0"></path><path class="lp-sclipire" style="animation-delay:1.2s" d="M214 248 q6 -5 12 0 q6 5 12 0"></path><path class="lp-sclipire" style="animation-delay:2.1s" d="M120 262 q6 -5 12 0 q6 5 12 0"></path></g><ellipse cx="150" cy="232" rx="98" ry="12" fill="#06305e" opacity="0.4"></ellipse><g transform="translate(150 200) scale(1.9) translate(-60 -74)"><path d="M12 66 C28 62 48 64 60 74 C72 64 92 62 108 66 L108 78 C92 74 72 76 60 88 C48 76 28 74 12 78 Z" fill="#e2c387" stroke="#5c3a1a" stroke-width="1.6" stroke-linejoin="round"></path><path d="M14 70 C30 66 48 68 58 78 M14 74 C30 70 48 72 58 82 M62 78 C72 68 90 66 106 70 M62 82 C72 72 90 70 106 74" fill="none" stroke="#c9a061" stroke-width="0.8"></path><path d="M60 74 C48 60 28 56 12 60 L12 66 C28 62 48 64 60 74 Z" fill="url(#tb-pag)" stroke="#5c3a1a" stroke-width="1.6" stroke-linejoin="round"></path><path d="M60 74 C72 60 92 56 108 60 L108 66 C92 62 72 64 60 74 Z" fill="url(#tb-pag)" stroke="#5c3a1a" stroke-width="1.6" stroke-linejoin="round"></path><path d="M60 74 C48 58 26 52 10 56 L12 60 C28 56 48 60 60 74 Z" fill="#fffdf6" stroke="#5c3a1a" stroke-width="1.4" stroke-linejoin="round"></path><path d="M60 74 C72 58 94 52 110 56 L108 60 C92 56 72 60 60 74 Z" fill="#fffdf6" stroke="#5c3a1a" stroke-width="1.4" stroke-linejoin="round"></path><path d="M16 63 C22 61.5 27 61.5 32 62.5 M88 62.5 C93 61.5 98 61.5 104 63" fill="none" stroke="#b8a37a" stroke-width="1" stroke-linecap="round"></path></g><path d="M98 176 C104 146 124 124 150 122 C176 124 196 146 202 176 C186 168 168 172 150 190 C132 172 114 168 98 176 Z" fill="#5daf4b" stroke="#2f6e2c" stroke-width="2.6" stroke-linejoin="round"></path><path d="M110 166 C116 146 132 132 150 130 C138 140 126 152 122 170 Z" fill="#8ad06a" opacity="0.85"></path><path d="M150 188 C140 176 132 170 122 168" fill="none" stroke="#e6c98e" stroke-width="4" stroke-linecap="round"></path><g transform="translate(106 138)"><rect x="0" y="14" width="34" height="24" fill="#ece4d4" stroke="#5c3a1a" stroke-width="1.6"></rect><rect x="22" y="14" width="12" height="24" fill="#c8bca6"></rect><rect x="1" y="10" width="5" height="5" fill="#ece4d4" stroke="#5c3a1a" stroke-width="1.2"></rect><rect x="9" y="10" width="5" height="5" fill="#ece4d4" stroke="#5c3a1a" stroke-width="1.2"></rect><rect x="17" y="10" width="5" height="5" fill="#ece4d4" stroke="#5c3a1a" stroke-width="1.2"></rect><rect x="25" y="10" width="5" height="5" fill="#ece4d4" stroke="#5c3a1a" stroke-width="1.2"></rect><rect x="-6" y="4" width="10" height="34" fill="#ece4d4" stroke="#5c3a1a" stroke-width="1.6"></rect><path d="M-8 5 L-1 -12 L6 5 Z" fill="#cf4436" stroke="#5c3a1a" stroke-width="1.4" stroke-linejoin="round"></path><path d="M14 38 V30 a4 4 0 0 1 8 0 V38 Z" fill="#4a3426"></path><rect x="-3" y="14" width="3.5" height="6" rx="1.5" fill="#4a5a78"></rect><path class="lp-steag-mic" d="M-1 -12 V-20 L6 -17 L-1 -14.5" fill="#ffc93d" stroke="#5c3a1a" stroke-width="1"></path></g><g transform="translate(184 150)"><path d="M-9 30 L-6 2 L6 2 L9 30 Z" fill="#f3ead8" stroke="#5c3a1a" stroke-width="1.6" stroke-linejoin="round"></path><path d="M2 2 L6 2 L9 30 L3 30 Z" fill="#d4c6aa"></path><path d="M-8 3 L0 -8 L8 3 Z" fill="#cf4436" stroke="#5c3a1a" stroke-width="1.4" stroke-linejoin="round"></path><rect x="-3" y="20" width="6" height="10" fill="#4a3426"></rect><g transform="translate(0 -2)"><g class="lp-palete"><g transform="rotate(0)"><rect x="-1.2" y="-24" width="2.4" height="22" fill="#8a5a2e"></rect><rect x="1.2" y="-23" width="7" height="17" fill="#fbf8f0" stroke="#5c3a1a" stroke-width="1"></rect></g><g transform="rotate(90)"><rect x="-1.2" y="-24" width="2.4" height="22" fill="#8a5a2e"></rect><rect x="1.2" y="-23" width="7" height="17" fill="#fbf8f0" stroke="#5c3a1a" stroke-width="1"></rect></g><g transform="rotate(180)"><rect x="-1.2" y="-24" width="2.4" height="22" fill="#8a5a2e"></rect><rect x="1.2" y="-23" width="7" height="17" fill="#fbf8f0" stroke="#5c3a1a" stroke-width="1"></rect></g><g transform="rotate(270)"><rect x="-1.2" y="-24" width="2.4" height="22" fill="#8a5a2e"></rect><rect x="1.2" y="-23" width="7" height="17" fill="#fbf8f0" stroke="#5c3a1a" stroke-width="1"></rect></g><circle r="3" fill="#8a5a2e" stroke="#5c3a1a" stroke-width="1"></circle></g></g></g><g transform="translate(140 150) scale(1)"><path d="M0 -16 L8 0 L-8 0 Z" fill="#2e7d3c" stroke="#1d5228" stroke-width="1.6" stroke-linejoin="round"></path><rect x="-1.2" y="0" width="2.4" height="3.5" fill="#6b3a17"></rect></g><g transform="translate(160 146) scale(0.85)"><path d="M0 -16 L8 0 L-8 0 Z" fill="#2e7d3c" stroke="#1d5228" stroke-width="1.6" stroke-linejoin="round"></path><rect x="-1.2" y="0" width="2.4" height="3.5" fill="#6b3a17"></rect></g><g transform="translate(206 170) scale(0.8)"><path d="M0 -16 L8 0 L-8 0 Z" fill="#2e7d3c" stroke="#1d5228" stroke-width="1.6" stroke-linejoin="round"></path><rect x="-1.2" y="0" width="2.4" height="3.5" fill="#6b3a17"></rect></g><rect x="148.6" y="84" width="3.2" height="40" rx="1.4" fill="#5a3418"></rect><path class="lp-steag" d="M151.8 86 L176 93 L151.8 100 Z" fill="#ff5a4a" stroke="#a8302a" stroke-width="1.8" stroke-linejoin="round"></path><circle cx="150.2" cy="83" r="3.4" fill="#ffc93d" stroke="#a8302a" stroke-width="1.2"></circle></g></svg>`

// Iconițe simple, desenate din cod
const ICON = {
  acasa: <path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z" />,
  antrenament: <><circle cx="12" cy="12" r="8.5" fill="none" strokeWidth="2.2" /><circle cx="12" cy="12" r="4" fill="none" strokeWidth="2.2" /><circle cx="12" cy="12" r="1.3" /></>,
  materiale: <path d="M4 4.5A1.5 1.5 0 0 1 5.5 3H11v17H5.5A1.5 1.5 0 0 1 4 18.5zM13 3h5.5A1.5 1.5 0 0 1 20 4.5v14a1.5 1.5 0 0 1-1.5 1.5H13z" />,
  triviador: <><path d="M6 21V3" fill="none" strokeWidth="2.2" /><path d="M7 4h11l-3 4 3 4H7z" /></>,
  profesori: <><path d="M12 4 2 9l10 5 10-5z" /><path d="M6 11.5V16c0 1.6 2.7 3 6 3s6-1.4 6-3v-4.5l-6 3z" /></>,
  linia: <path d="M6.6 3.5 9 3l1.6 4.4-2 1.4a12 12 0 0 0 6.6 6.6l1.4-2L21 15l-.5 2.4A2.5 2.5 0 0 1 18 19.5 15.5 15.5 0 0 1 4.5 6 2.5 2.5 0 0 1 6.6 3.5z" />,
  continut: <path d="M5 3h10l4 4v14H5zM14 3v5h5M8 12h8M8 16h8" fill="none" strokeWidth="2" />,
  mesaje: <path d="M3 6h18v12H3zM3 6l9 7 9-7" fill="none" strokeWidth="2" />,
  organizatie: <path d="M3 21h18M5 21V9l7-5 7 5v12M9 21v-6h6v6M10 11h4" fill="none" strokeWidth="2" />,
}

const PAGINI = {
  elev: [
    { href: '/', nume: 'Acasă', icon: 'acasa' },
    { href: '/antrenament', nume: 'Antrenament', icon: 'antrenament' },
    { href: '/materiale', nume: 'Materiale', icon: 'materiale' },
    { href: '/joc', nume: 'Triviador', icon: 'triviador' },
    { href: '/profesori', nume: 'Profesori', icon: 'profesori' },
    { href: '/linia-ai', nume: 'Linia AI', icon: 'linia', nou: true },
  ],
  profesor: [
    { href: '/', nume: 'Acasă', icon: 'acasa' },
    { href: '/profesori', nume: 'Ședințele mele', icon: 'profesori' },
    { href: '/admin/continut', nume: 'Conținut', icon: 'continut' },
    { href: '/organizatie', nume: 'Elevi', icon: 'organizatie' },
    { href: '/linia-ai', nume: 'Linia AI', icon: 'linia' },
  ],
  organizatie: [
    { href: '/', nume: 'Acasă', icon: 'acasa' },
    { href: '/organizatie', nume: 'Organizația', icon: 'organizatie' },
    { href: '/profesori', nume: 'Ședințele mele', icon: 'profesori' },
    { href: '/admin/continut', nume: 'Conținut', icon: 'continut' },
    { href: '/linia-ai', nume: 'Linia AI', icon: 'linia' },
  ],
  admin: [
    { href: '/', nume: 'Acasă', icon: 'acasa' },
    { href: '/admin/organizatii', nume: 'Organizații', icon: 'organizatie' },
    { href: '/admin/profesori', nume: 'Profesori', icon: 'profesori' },
    { href: '/admin/continut', nume: 'Conținut', icon: 'continut' },
    { href: '/admin/mesaje', nume: 'Mesaje', icon: 'mesaje' },
    { href: '/admin/feedback', nume: 'Feedback', icon: 'mesaje' },
  ],
}

function Iconita({ nume }) {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" stroke="currentColor" strokeLinecap="round" aria-hidden="true">
      {ICON[nume]}
    </svg>
  )
}

function Cont({ utilizator, onIese }) {
  const { preferinta, alege, stil, alegeStil, avatar } = useTema()
  const [deschis, setDeschis] = useState(false)
  const [nivel, setNivel] = useState(null)
  const cale = usePathname()
  const ref = useRef(null)

  // Nivelul elevului se reîmprospătează la fiecare schimbare de pagină (după un quiz câștigat, de ex.)
  useEffect(() => {
    if (utilizator.rol !== 'elev') return
    api('/progres').then((p) => setNivel(p.nivel)).catch(() => {})
  }, [utilizator, cale])

  useEffect(() => {
    if (!deschis) return
    const inchide = (e) => !ref.current?.contains(e.target) && setDeschis(false)
    const esc = (e) => e.key === 'Escape' && setDeschis(false)
    document.addEventListener('pointerdown', inchide)
    document.addEventListener('keydown', esc)
    return () => {
      document.removeEventListener('pointerdown', inchide)
      document.removeEventListener('keydown', esc)
    }
  }, [deschis])

  return (
    <div className="mn-cont" ref={ref}>
      <button className="mn-cont-buton" onClick={() => setDeschis(!deschis)} aria-expanded={deschis} aria-haspopup="true">
        {avatar ? (
          <span className="mn-initiala mn-avatar" aria-hidden="true"><Avatar id={avatar} size={32} /></span>
        ) : (
          <span className="mn-initiala" aria-hidden="true">{utilizator.nume.charAt(0).toUpperCase()}</span>
        )}
        <span className="mn-cont-text">
          {utilizator.nume.split(' ')[0]}
          {nivel && <small>Nivel {nivel.nivel} · {nivel.xpTotal} XP</small>}
        </span>
      </button>
      {deschis && (
        <div className="mn-popover" role="menu">
          <Link href="/setari" className="sc-menu-link" onClick={() => setDeschis(false)}>
            <svg viewBox="0 0 24 24" width="19" height="19" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z" /><path d="m8 12 3 3 5-6" /></svg>
            Setările contului
          </Link>
          <Link href="/profil" className="sc-menu-link" onClick={() => setDeschis(false)}>
            <svg viewBox="0 0 24 24" width="19" height="19" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><circle cx="12" cy="8" r="4" /><path d="M4 21c0-4 3.6-7 8-7s8 3 8 7" /></svg>
            Profil: avatar și stil
          </Link>
          <div className="mn-pop-titlu">Tema site-ului</div>
          <div className="mn-teme">
            <button className={`mn-tema ${preferinta === 'auto' ? 'mn-tema-activa' : ''}`} onClick={() => alege('auto')}>
              <span className="mn-mostra" data-mostra="auto" />
              Automat
              <small>după anotimp și oră</small>
            </button>
            {TEME.map((t) => (
              <button key={t.cod} className={`mn-tema ${preferinta === t.cod ? 'mn-tema-activa' : ''}`} onClick={() => alege(t.cod)}>
                <span className="mn-mostra" data-mostra={t.cod} />
                {t.nume}
              </button>
            ))}
          </div>
          <div className="mn-pop-titlu">Stilul desenelor</div>
          <div className="mn-teme mn-stiluri">
            {STILURI.map((s) => (
              <button key={s.id} className={`mn-tema ${stil === s.id ? 'mn-tema-activa' : ''}`} onClick={() => alegeStil(s.id)}>
                {s.nume}
              </button>
            ))}
          </div>
          <button className="mn-iesire" onClick={onIese}>Ieși din cont</button>
        </div>
      )}
    </div>
  )
}

export default function Meniu() {
  const cale = usePathname() || '/'
  const router = useRouter()
  const { utilizator, iese } = useSesiune()
  const [deschis, setDeschis] = useState(false)
  const activ = (href) => (href === '/' ? cale === '/' : cale.startsWith(href))
  const pagini = utilizator && (!esteProfesor(utilizator) || profesorAprobat(utilizator)) ? PAGINI[utilizator.rol] : []

  async function ieseSiPleaca() {
    await iese()
    router.push('/')
  }

  return (
    <header className="mn-bara">
      <div className="mn-interior">
        <Link href="/" className="mn-brand" onClick={() => setDeschis(false)}>
          <span style={{ display: 'inline-flex', width: 40, height: 40, flexShrink: 0 }} dangerouslySetInnerHTML={{ __html: LOGO_SVG }} />
          <b>TeleBac</b>
        </Link>

        {pagini.length > 0 && (
          <>
            <button className="mn-buton" onClick={() => setDeschis(!deschis)} aria-expanded={deschis} aria-controls="mn-lista">
              <span className="mn-hamburger" aria-hidden="true"><i /><i /><i /></span>
              Meniu
            </button>
            <nav id="mn-lista" className={`mn-lista ${deschis ? 'mn-deschis' : ''}`} aria-label="Meniul principal">
              {pagini.map((p) => (
                <Link
                  key={p.href}
                  href={p.href}
                  className={`mn-link ${activ(p.href) ? 'mn-activ' : ''}`}
                  aria-current={activ(p.href) ? 'page' : undefined}
                  onClick={() => setDeschis(false)}
                >
                  <Iconita nume={p.icon} />
                  {p.nume}
                  {p.nou && <span className="mn-nou">nou</span>}
                </Link>
              ))}
            </nav>
          </>
        )}

        {utilizator && <Cont utilizator={utilizator} onIese={ieseSiPleaca} />}
      </div>
    </header>
  )
}