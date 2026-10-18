"use client";
// components/HartaInsule.jsx — harta cu insule a unei materii (Next.js App Router + Tailwind).
// Fiecare insulă = un capitol (ex. „Harap-Alb”), fiecare cerc de pe insulă = o lecție (Curs din API).
// Harta are 3 locuri pentru insule; dacă materia are mai multe capitole, harta se răsfoiește pe pagini.
import { useCallback, useMemo, useState } from "react";
import { ACTIVITATI } from "@/components/Iconite";
import TabNivel from "@/components/TabNivel";

// Harta e un singur SVG de 1000 x 625 unități (raport 16:10).
// Fiecare insulă e desenată în coordonate proprii („locale”), apoi mutată cu tx, ty și micșorată cu scara.
// Pozițiile lecțiilor, titlului și lacătului sunt tot în coordonate locale.
// Ordinea lecțiilor (Curs.ordine) = ordinea de deblocare.
const L = 1000;
const H = 625;
const PE_PAGINA = 3;

// Cele 3 locuri de pe hartă. `id` alege desenele (DECOR) când capitolul e chiar opera desenată.
const LOCURI = [
  {
    id: "harap",
    titlu: "Harap-Alb",
    autor: "Ion Creangă",
    tx: 20,
    ty: 40,
    scara: 0.78,
    centru: [240, 165],
    nisip:
      "M30,160 C25,100 90,60 160,70 C200,35 280,30 330,60 C400,60 455,105 445,165 C440,230 380,265 300,262 C250,290 170,295 120,265 C60,265 30,215 30,160 Z",
    titluPoz: [240, 42],
    puncte: [[165, 228], [300, 215], [235, 255], [110, 190], [370, 175]],
  },
  {
    id: "moara",
    titlu: "Moara cu noroc",
    autor: "Ioan Slavici",
    tx: 300,
    ty: 318,
    scara: 0.72,
    centru: [245, 165],
    nisip:
      "M40,170 C20,110 70,55 150,50 C200,20 270,30 320,55 C390,45 450,90 455,150 C470,215 420,270 350,280 C300,310 220,305 170,285 C100,295 35,250 40,170 Z",
    titluPoz: [245, 28],
    lacatPoz: [245, 140],
    puncte: [[255, 250], [160, 230], [350, 225], [205, 75], [400, 140]],
  },
  {
    id: "luceafarul",
    titlu: "Luceafărul",
    autor: "Mihai Eminescu",
    tx: 630,
    ty: 70,
    scara: 0.8,
    centru: [205, 160],
    nisip:
      "M50,150 C40,90 95,45 160,60 C190,40 230,30 270,45 C340,50 375,110 360,170 C350,235 290,280 210,272 C140,285 70,250 55,200 C45,185 45,165 50,150 Z",
    titluPoz: [205, 38],
    lacatPoz: [180, 128],
    culori: { nisip: "#bfae92", iarba: "#2f5d57", halo: "#7d8cf0", halo2: "#a3b2ff" },
    puncte: [[165, 222], [245, 250], [95, 175], [315, 215], [130, 110]],
  },
];

// Poduri între locurile consecutive. a = punct pe insula „de”, b = punct pe insula „la” (coordonate locale, pe nisip).
// Podul e întreg și din lemn doar când insula „la” e deblocată; altfel e gri și neterminat.
const PODURI = [
  { de: 0, la: 1, a: [395, 245], b: [115, 78] },
  { de: 1, la: 2, a: [380, 72], b: [118, 252] },
];

// Lecțiile grupate pe capitole, în ordinea lor; fiecare capitol primește un loc de pe hartă.
function construiesteInsule(cursuri) {
  const capitole = [];
  for (const c of cursuri) {
    const nume = c.capitol || "Lecții";
    let cap = capitole.find((x) => x.titlu === nume);
    if (!cap) capitole.push((cap = { titlu: nume, cursuri: [] }));
    cap.cursuri.push(c);
  }
  return capitole.map((cap, i) => {
    const loc = LOCURI[i % PE_PAGINA];
    const desenata = LOCURI.find((l) => l.titlu === cap.titlu);
    const toate = cap.cursuri.map((c, j) => ({ id: c.id, titlu: `Lecția ${j + 1}`, curs: c, x: loc.puncte[j]?.[0], y: loc.puncte[j]?.[1] }));
    const nivele = toate.slice(0, loc.puncte.length);
    return {
      ...loc,
      id: `${i}-${cap.titlu}`,
      // desenele speciale apar doar când opera e pe locul ei de pe hartă
      arta: desenata?.id === loc.id ? loc.id : null,
      culori: desenata?.id === loc.id ? loc.culori : undefined,
      titlu: cap.titlu,
      autor: desenata ? desenata.autor : `${cap.cursuri.length} ${cap.cursuri.length === 1 ? "lecție" : "lecții"}`,
      nivele,
      toate,
      ascunse: toate.length - nivele.length,
    };
  });
}

const CULORI_IMPLICITE = { nisip: "#efd29a", iarba: "#5db65a", halo: "#8fe6f0", halo2: "#b5f1f6" };
// Progresul (nivele terminate, video/materiale parcurse) stă în lib/progres.js.

// --- Animații (folosite de elementele SVG) ---
const CSS = `
.harta-insule .h-valut{animation:h-valut 7s ease-in-out infinite}
@keyframes h-valut{0%,100%{transform:translateX(0);opacity:.12}50%{transform:translateX(9px);opacity:.55}}
.harta-insule .h-spuma{transform-box:fill-box;transform-origin:center;animation:h-spuma 4.5s ease-out infinite}
@keyframes h-spuma{0%{transform:scale(1);opacity:.75}100%{transform:scale(1.07);opacity:0}}
.harta-insule .h-legan{transform-box:fill-box;transform-origin:50% 100%;animation:h-legan 3.6s ease-in-out infinite}
@keyframes h-legan{0%,100%{transform:rotate(-3deg) translateY(0)}50%{transform:rotate(3deg) translateY(2px)}}
.harta-insule .h-deriva{animation:h-deriva 20s ease-in-out infinite alternate}
@keyframes h-deriva{from{transform:translateX(-25px)}to{transform:translateX(30px)}}
.harta-insule .h-palete{transform-box:fill-box;transform-origin:center;animation:h-rot 9s linear infinite}
@keyframes h-rot{to{transform:rotate(360deg)}}
.harta-insule .h-zbor{animation:h-zbor 40s linear infinite}
@keyframes h-zbor{from{transform:translateX(-80px)}to{transform:translateX(1080px)}}
.harta-insule .h-aripi{transform-box:fill-box;transform-origin:center;animation:h-aripi .55s ease-in-out infinite alternate}
@keyframes h-aripi{to{transform:scaleY(.3)}}
.harta-insule .h-unda{transform-box:fill-box;transform-origin:center;animation:h-unda 3.2s ease-out infinite}
@keyframes h-unda{0%{transform:scale(.3);opacity:.8}100%{transform:scale(1.7);opacity:0}}
.harta-insule .h-fum{transform-box:fill-box;transform-origin:center;animation:h-fum 3s ease-out infinite}
@keyframes h-fum{0%{transform:translateY(0) scale(.6);opacity:.75}100%{transform:translateY(-24px) scale(1.7);opacity:0}}
.harta-insule .h-sclipire{animation:h-sclipire 2.6s ease-in-out infinite}
@keyframes h-sclipire{0%,100%{opacity:.2}50%{opacity:1}}
.harta-insule .h-puls{transform-box:fill-box;transform-origin:center;animation:h-puls 3.2s ease-in-out infinite}
@keyframes h-puls{0%,100%{transform:scale(.88);opacity:.8}50%{transform:scale(1.15);opacity:1}}
.harta-insule .h-licurici{animation:h-licurici 2.4s ease-in-out infinite}
@keyframes h-licurici{0%,100%{opacity:0;transform:translate(0,0)}50%{opacity:1;transform:translate(3px,-5px)}}
@media (prefers-reduced-motion: reduce){.harta-insule *{animation:none!important}}
`;

// Generator pseudo-aleator cu seed fix: elementele apar mereu în aceleași locuri (fără erori de hidratare).
function aleator(seed) {
  return () => {
    seed = (seed * 16807) % 2147483647;
    return (seed - 1) / 2147483646;
  };
}

const VALURI = (() => {
  const r = aleator(42);
  return Array.from({ length: 48 }, () => ({
    x: (r() * L).toFixed(1),
    y: (r() * H).toFixed(1),
    s: (0.6 + r() * 0.9).toFixed(2),
    lung: r() > 0.55,
    d: (r() * 7).toFixed(2),
    t: (5 + r() * 4).toFixed(2),
  }));
})();

// zona de noapte din jurul Luceafărului
const NOAPTE = { cx: 845, cy: 150, rx: 300, ry: 230 };
const STELE = (() => {
  const r = aleator(7);
  const stele = [];
  while (stele.length < 46) {
    const x = r() * L;
    const y = r() * H;
    const dx = (x - NOAPTE.cx) / (NOAPTE.rx * 0.85);
    const dy = (y - NOAPTE.cy) / (NOAPTE.ry * 0.85);
    if (dx * dx + dy * dy < 1) {
      stele.push({ x: x.toFixed(1), y: y.toFixed(1), r: (0.8 + r() * 1.6).toFixed(2), d: (r() * 3).toFixed(2), mare: r() > 0.85 });
    }
  }
  return stele;
})();

const poz = ([x, y]) => ({ left: `${(x / L) * 100}%`, top: `${(y / H) * 100}%` });
const peHarta = (insula, [x, y]) => [insula.tx + x * insula.scara, insula.ty + y * insula.scara];
const scalare = ([cx, cy], s) => `translate(${cx} ${cy}) scale(${s}) translate(${-cx} ${-cy})`;

// ---------- Definiții SVG: gradienți, filtre, texturi ----------
function Definitii() {
  return (
    <defs>
      <linearGradient id="h-adanc" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#3bb3dc" />
        <stop offset="0.55" stopColor="#1f8dc4" />
        <stop offset="1" stopColor="#13679e" />
      </linearGradient>
      <radialGradient id="h-noapte">
        <stop offset="0" stopColor="#0a1236" stopOpacity="0.95" />
        <stop offset="0.6" stopColor="#141d55" stopOpacity="0.85" />
        <stop offset="1" stopColor="#1b2a6b" stopOpacity="0" />
      </radialGradient>
      <filter id="h-blur" x="-30%" y="-30%" width="160%" height="160%">
        <feGaussianBlur stdDeviation="16" />
      </filter>
      <filter id="h-blur-mic" x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur stdDeviation="5" />
      </filter>
      <filter id="h-blur-mare" x="-50%" y="-50%" width="200%" height="200%">
        <feGaussianBlur stdDeviation="40" />
      </filter>
      {/* face țărmul neregulat, ca o coastă reală */}
      <filter id="h-coasta" x="-6%" y="-6%" width="112%" height="112%">
        <feTurbulence type="fractalNoise" baseFrequency="0.04" numOctaves="2" seed="5" result="zgomot" />
        <feDisplacementMap in="SourceGraphic" in2="zgomot" scale="9" xChannelSelector="R" yChannelSelector="G" />
      </filter>
      {/* dâre de lumină pe apă */}
      <filter id="h-textura" x="0" y="0" width="100%" height="100%">
        <feTurbulence type="fractalNoise" baseFrequency="0.006 0.04" numOctaves="3" seed="11" />
        <feColorMatrix values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 0 0 1.6 -0.85" />
      </filter>
      <filter id="h-gri">
        <feColorMatrix type="saturate" values="0" />
      </filter>
      <mask id="h-semiluna">
        <circle cx="0" cy="0" r="17" fill="#fff" />
        <circle cx="8" cy="-5" r="15" fill="#000" />
      </mask>
    </defs>
  );
}

// ---------- Apa ----------
function Apa() {
  return (
    <>
      <rect width={L} height={H} fill="url(#h-adanc)" />
      {/* zone mai puțin și mai mult adânci */}
      <g filter="url(#h-blur-mare)" opacity=".55">
        <ellipse cx="180" cy="120" rx="220" ry="110" fill="#62d3ef" />
        <ellipse cx="520" cy="200" rx="160" ry="90" fill="#4cc4e8" />
        <ellipse cx="470" cy="430" rx="190" ry="130" fill="#4bbfe2" />
        <ellipse cx="900" cy="590" rx="210" ry="90" fill="#0e5687" />
        <ellipse cx="110" cy="600" rx="220" ry="80" fill="#0f5b8e" />
      </g>
      <rect width={L} height={H} filter="url(#h-textura)" opacity=".3" />
      {VALURI.map((v, i) => (
        <g key={i} transform={`translate(${v.x} ${v.y}) scale(${v.s})`}>
          <path
            className="h-valut"
            d={v.lung ? "M0,0 q7,-5 14,0 t14,0 t14,0" : "M0,0 q7,-5 14,0 t14,0"}
            fill="none"
            stroke="#fff"
            strokeWidth="2.2"
            strokeLinecap="round"
            style={{ animationDelay: `-${v.d}s`, animationDuration: `${v.t}s` }}
          />
        </g>
      ))}
    </>
  );
}

// Colțul de noapte: cer înstelat, semiluna și luceafărul cu reflexia lui pe apă
function Noapte() {
  return (
    <>
      <ellipse cx={NOAPTE.cx} cy={NOAPTE.cy} rx={NOAPTE.rx} ry={NOAPTE.ry} fill="url(#h-noapte)" />
      {STELE.map((s, i) =>
        s.mare ? (
          <path
            key={i}
            className="h-sclipire"
            transform={`translate(${s.x} ${s.y})`}
            d="M0,-5 L1.2,-1.2 L5,0 L1.2,1.2 L0,5 L-1.2,1.2 L-5,0 L-1.2,-1.2 Z"
            fill="#fff8d6"
            style={{ animationDelay: `-${s.d}s` }}
          />
        ) : (
          <circle key={i} className="h-sclipire" cx={s.x} cy={s.y} r={s.r} fill="#fff" style={{ animationDelay: `-${s.d}s` }} />
        )
      )}
      <g transform="translate(700 46)">
        <circle r="26" fill="#f4f1d0" opacity=".18" filter="url(#h-blur-mic)" />
        <circle r="17" fill="#f4f1d0" mask="url(#h-semiluna)" />
      </g>
      {/* reflexia luceafărului pe apă */}
      <g transform="translate(958 0)" stroke="#fff3b0" strokeLinecap="round">
        {[110, 130, 150, 172, 196].map((y, i) => (
          <line key={y} className="h-sclipire" x1={-14 + i * 2} y1={y} x2={14 - i * 2} y2={y} strokeWidth="2.5" style={{ animationDelay: `-${i * 0.4}s` }} />
        ))}
      </g>
      <g transform="translate(958 58)">
        <circle r="34" fill="#fff3b0" opacity=".35" filter="url(#h-blur-mic)" />
        <g className="h-puls">
          <path d="M0,-18 L3.5,-3.5 L18,0 L3.5,3.5 L0,18 L-3.5,3.5 L-18,0 L-3.5,-3.5 Z" fill="#fffbe6" />
          <circle r="4" fill="#fff" />
        </g>
      </g>
    </>
  );
}

// ---------- Obiecte pe apă ----------
function Corabie({ x, y, s = 1, durata = 20 }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <g className="h-deriva" style={{ animationDuration: `${durata}s` }}>
        <ellipse cx="0" cy="13" rx="32" ry="5" fill="rgba(8,50,80,.35)" />
        <path d="M-52,10 q7,-4 14,0 M-46,16 q7,-4 14,0" fill="none" stroke="#fff" strokeOpacity=".6" strokeWidth="2" strokeLinecap="round" />
        <g className="h-legan">
          <path d="M-24,0 L24,0 L16,11 L-16,11 Z" fill="#8b5a2b" />
          <rect x="-24" y="-2" width="48" height="3" fill="#a8703c" />
          <line x1="0" y1="0" x2="0" y2="-46" stroke="#5a3a1f" strokeWidth="2.2" />
          <path d="M2,-42 L2,-4 L24,-6 Z" fill="#fbf6ea" />
          <path d="M-2,-36 L-2,-4 L-18,-4 Z" fill="#efe3c8" />
          <path d="M0,-46 L10,-43 L0,-40 Z" fill="#e2483d" />
        </g>
      </g>
    </g>
  );
}

function Pelican() {
  // origine la picioare, privește spre dreapta
  return (
    <g>
      <path d="M-3,-4 L-3,0 M3,-4 L3,0" stroke="#f2a33a" strokeWidth="2" />
      <path d="M-14,-12 L-21,-9 L-13,-7 Z" fill="#cfd8df" />
      <ellipse cx="0" cy="-12" rx="14" ry="9" fill="#fff" stroke="#c9d3db" />
      <path d="M-12,-14 Q-2,-20 8,-12 Q0,-8 -12,-10 Z" fill="#dfe6ec" />
      <path d="M8,-16 Q12,-24 13,-30" stroke="#fff" strokeWidth="6" fill="none" strokeLinecap="round" />
      <circle cx="14" cy="-31" r="5.5" fill="#fff" stroke="#c9d3db" />
      <path d="M18,-28 Q26,-21 34,-27 L18,-28 Z" fill="#f6c06b" />
      <path d="M18,-32 L36,-27 L18,-28 Z" fill="#f2a33a" />
      <circle cx="15.5" cy="-32.5" r="1.2" fill="#222" />
    </g>
  );
}

function PelicanPeStalp({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse cx="0" cy="28" rx="13" ry="3.5" fill="none" stroke="#fff" strokeOpacity=".6" strokeWidth="2" />
      <rect x="-5" y="0" width="10" height="28" rx="2" fill="#7a4e2a" />
      <ellipse cx="0" cy="0" rx="5" ry="2" fill="#9a6a3e" />
      <Pelican />
    </g>
  );
}

function PelicanPlutitor({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <g className="h-legan" style={{ animationDuration: "4.6s" }}>
        <ellipse cx="0" cy="1" rx="19" ry="4" fill="rgba(8,50,80,.3)" />
        <Pelican />
      </g>
    </g>
  );
}

function Stanca({ x, y, s = 1 }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse cx="0" cy="4" rx="27" ry="10" fill="none" stroke="#fff" strokeOpacity=".55" strokeWidth="3" filter="url(#h-coasta)" />
      <path d="M-18,4 Q-17,-9 -5,-12 Q8,-17 18,-3 Q21,6 2,7 Q-13,9 -18,4 Z" fill="#7f8a93" />
      <path d="M-10,-6 Q-3,-12 6,-11 Q0,-6 -10,-6 Z" fill="#a8b2ba" />
    </g>
  );
}

function Geamandura({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <g className="h-legan" style={{ animationDuration: "2.8s" }}>
        <ellipse cx="0" cy="9" rx="12" ry="3" fill="rgba(8,50,80,.3)" />
        <path d="M-8,8 Q-9,-6 0,-8 Q9,-6 8,8 Z" fill="#e2483d" />
        <rect x="-8.5" y="-1" width="17" height="4" fill="#fff" />
        <line x1="0" y1="-8" x2="0" y2="-16" stroke="#333" strokeWidth="1.5" />
        <circle cy="-17" r="2.4" fill="#ffd54a" />
      </g>
    </g>
  );
}

function Unda({ x, y, d = 0 }) {
  return (
    <g transform={`translate(${x} ${y})`} fill="none" stroke="#fff" strokeWidth="2">
      <ellipse className="h-unda" rx="14" ry="6" style={{ animationDelay: `${d}s` }} />
      <ellipse className="h-unda" rx="14" ry="6" style={{ animationDelay: `${d + 1.6}s` }} />
    </g>
  );
}

function DecorApa() {
  return (
    <>
      <Stanca x={952} y={400} />
      <Stanca x={58} y={575} s={1.2} />
      <Stanca x={240} y={598} s={0.8} />
      <Unda x={120} y={335} />
      <Unda x={862} y={608} d={1.1} />
      <Unda x={700} y={545} d={2.2} />
      <Corabie x={515} y={140} durata={22} />
      <Corabie x={150} y={478} s={0.85} durata={28} />
      <Geamandura x={628} y={262} />
      <PelicanPeStalp x={800} y={455} />
      <PelicanPlutitor x={905} y={560} />
    </>
  );
}

function Pescarusi() {
  const stoluri = [
    { y: 300, durata: 44, intarziere: 0, pasari: [[0, 0], [22, 10], [42, -4]] },
    { y: 560, durata: 56, intarziere: -27, pasari: [[0, 0], [26, 9]] },
  ];
  return stoluri.map((st, i) => (
    <g key={i} transform={`translate(0 ${st.y})`}>
      <g className="h-zbor" style={{ animationDuration: `${st.durata}s`, animationDelay: `${st.intarziere}s` }}>
        {st.pasari.map(([x, y], j) => (
          <g key={j} transform={`translate(${x} ${y})`}>
            <ellipse cx="4" cy="24" rx="6" ry="2" fill="rgba(0,40,70,.18)" />
            <path
              className="h-aripi"
              style={{ animationDelay: `${-j * 0.2}s` }}
              d="M-9,0 Q-4.5,-6 0,0 Q4.5,-6 9,0"
              fill="none"
              stroke="#fff"
              strokeWidth="2.4"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </g>
        ))}
      </g>
    </g>
  ));
}

// ---------- Obiecte pe insule ----------
function Copac({ x, y, s = 1, frunze = "#24803a", lumina = "#3caa52", flori }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse cx="4" cy="12" rx="14" ry="5" fill="rgba(0,0,0,.18)" />
      <rect x="-3" y="0" width="6" height="13" rx="2" fill="#7a4a1a" />
      <circle r="15" cy="-5" fill={frunze} />
      <circle r="9" cx="-6" cy="-12" fill={lumina} />
      {flori &&
        [[-8, -2], [5, -12], [8, 2], [-2, -8], [2, -1]].map(([fx, fy], i) => <circle key={i} cx={fx} cy={fy} r="1.6" fill={flori} />)}
    </g>
  );
}

function Brad({ x, y, s = 1, culoare = "#2f7d45", lumina = "#3b9152" }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse cx="4" cy="8" rx="13" ry="4" fill="rgba(0,0,0,.18)" />
      <rect x="-2.5" y="0" width="5" height="9" fill="#6b4220" />
      <path d="M0,-20 L15,4 L-15,4 Z" fill={culoare} />
      <path d="M0,-32 L12,-10 L-12,-10 Z" fill={lumina} />
    </g>
  );
}

function Drum({ d, latime = 13, culoare = "#c99e5c", lumina = "#e6c98c" }) {
  return (
    <g fill="none" strokeLinecap="round">
      <path d={d} stroke={culoare} strokeWidth={latime} />
      <path d={d} stroke={lumina} strokeWidth={latime - 5} />
    </g>
  );
}

// --- Harap-Alb ---
function Castel({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse cx="4" cy="2" rx="48" ry="8" fill="rgba(0,0,0,.2)" />
      <rect x="-26" y="-38" width="52" height="38" fill="#e6dfd0" stroke="#8d8473" strokeWidth="1.5" />
      {[0, 1, 2, 3, 4].map((i) => (
        <rect key={i} x={-26 + i * 11} y="-44" width="7" height="7" fill="#e6dfd0" stroke="#8d8473" strokeWidth="1.2" />
      ))}
      {[-34, 34].map((tx) => (
        <g key={tx} transform={`translate(${tx} 0)`}>
          <rect x="-10" y="-58" width="20" height="58" fill="#d9d2c3" stroke="#8d8473" strokeWidth="1.5" />
          <rect x="-3" y="-46" width="6" height="9" rx="3" fill="#4a3a2a" />
          <path d="M-13,-58 L0,-82 L13,-58 Z" fill="#c0392b" />
          <line x1="0" y1="-82" x2="0" y2="-93" stroke="#5a3a1f" strokeWidth="1.5" />
          <path d="M0,-93 L10,-90 L0,-87 Z" fill="#f1c40f" />
        </g>
      ))}
      <path d="M-8,0 L-8,-16 Q0,-27 8,-16 L8,0 Z" fill="#6b4a2b" />
    </g>
  );
}

function FantanaCumpana({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse cx="6" cy="2" rx="20" ry="5" fill="rgba(0,0,0,.2)" />
      <line x1="16" y1="0" x2="16" y2="-30" stroke="#6b4220" strokeWidth="3" />
      <rect x="-10" y="-15" width="20" height="15" rx="2" fill="#9aa3ab" stroke="#6d757c" />
      <path d="M-10,-10 H10 M-10,-5 H10" stroke="#7d868e" strokeWidth="1" />
      <ellipse cx="0" cy="-15" rx="10" ry="3" fill="#2d5f86" stroke="#6d757c" />
      <line x1="-14" y1="-48" x2="36" y2="-18" stroke="#8b5a2b" strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="36" cy="-18" r="3.5" fill="#6b4220" />
      <line x1="-14" y1="-48" x2="-6" y2="-24" stroke="#5a3a1f" strokeWidth="1" />
      <rect x="-9" y="-25" width="6" height="5" rx="1" fill="#6b4220" />
    </g>
  );
}

// Harap-Alb: castelul craiului, codrul întunecat și fântâna unde îl păcălește Spânul
function DecorHarap() {
  return (
    <>
      <Drum d="M165,228 C215,255 265,248 300,215" />
      <Drum d="M300,215 C312,190 322,170 330,152" latime={10} />
      <Drum d="M165,228 C140,220 125,212 112,204" latime={9} />
      <Castel x={330} y={145} />
      <Brad x={75} y={150} culoare="#1f5e36" lumina="#2b7445" />
      <Brad x={100} y={122} culoare="#1f5e36" lumina="#2b7445" />
      <Brad x={128} y={140} s={0.9} culoare="#1f5e36" lumina="#2b7445" />
      <Brad x={156} y={110} culoare="#1f5e36" lumina="#2b7445" />
      <Brad x={186} y={130} s={0.85} culoare="#1f5e36" lumina="#2b7445" />
      <Brad x={62} y={185} s={0.8} culoare="#1f5e36" lumina="#2b7445" />
      <Copac x={238} y={98} s={0.85} />
      <Copac x={398} y={200} s={0.8} />
      <FantanaCumpana x={100} y={208} />
    </>
  );
}

// --- Moara cu noroc ---
function Moara({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse cx="3" cy="2" rx="17" ry="5" fill="rgba(0,0,0,.2)" />
      <path d="M-12,0 L12,0 L8,-34 L-8,-34 Z" fill="#f1e4c8" stroke="#8b6b44" strokeWidth="1.5" />
      <rect x="-4" y="-12" width="8" height="12" rx="1" fill="#6b4a2b" />
      <path d="M-11,-34 L0,-48 L11,-34 Z" fill="#a0522d" />
      <g transform="translate(0 -38)">
        <g className="h-palete">
          {[0, 90, 180, 270].map((r) => (
            <g key={r} transform={`rotate(${r})`}>
              <rect x="-1.5" y="-30" width="3" height="30" fill="#6b4a2b" />
              <rect x="1.5" y="-28" width="8" height="20" fill="#f7efe0" stroke="#8b6b44" strokeWidth="1" />
            </g>
          ))}
        </g>
        <circle r="3.5" fill="#4a3220" />
      </g>
    </g>
  );
}

function Han({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse cx="3" cy="2" rx="31" ry="6" fill="rgba(0,0,0,.2)" />
      <rect x="-24" y="-26" width="48" height="26" fill="#f3e6cf" stroke="#8b6b44" strokeWidth="1.5" />
      <rect x="-5" y="-15" width="10" height="15" fill="#6b3f1f" />
      <rect x="-19" y="-20" width="9" height="7" fill="#7fc6e8" stroke="#8b6b44" />
      <rect x="10" y="-20" width="9" height="7" fill="#7fc6e8" stroke="#8b6b44" />
      <rect x="12" y="-50" width="6" height="18" fill="#8a5a3a" />
      <path d="M-29,-24 L0,-44 L29,-24 Z" fill="#b5452b" />
      {[0, 1, 2].map((i) => (
        <circle key={i} className="h-fum" cx="15" cy="-54" r="4" fill="#eef2f5" style={{ animationDelay: `${i}s` }} />
      ))}
      <line x1="34" y1="0" x2="34" y2="-24" stroke="#6b4220" strokeWidth="2" />
      <rect x="27" y="-26" width="15" height="9" rx="1.5" fill="#c99e5c" stroke="#6b4220" />
    </g>
  );
}

function BarcaVasle({ x, y, r = 0 }) {
  return (
    <g transform={`translate(${x} ${y}) rotate(${r})`}>
      <g className="h-legan" style={{ animationDuration: "3s" }}>
        <path d="M-27,-6 L27,6" stroke="#6b4220" strokeWidth="2" />
        <path d="M-18,0 Q-14,-8 0,-8 Q14,-8 18,0 Q14,8 0,8 Q-14,8 -18,0 Z" fill="#8b5a2b" />
        <path d="M-14,0 Q-11,-5 0,-5 Q11,-5 14,0 Q11,5 0,5 Q-11,5 -14,0 Z" fill="#b47a45" />
        <rect x="-2" y="-5" width="4" height="10" fill="#8b5a2b" />
      </g>
    </g>
  );
}

// Moara cu noroc: dealul cu moara, hanul de la răscruce și pontonul
function DecorMoara() {
  return (
    <>
      <ellipse cx="310" cy="130" rx="74" ry="38" fill="#4aa047" />
      <ellipse cx="300" cy="122" rx="52" ry="22" fill="#62ba5c" />
      <Drum d="M255,250 C200,250 150,215 112,152" latime={11} />
      <Drum d="M255,250 C300,240 330,180 314,140" latime={11} />
      <Drum d="M255,250 C330,250 400,200 450,174" />
      <Drum d="M255,250 C230,262 200,278 175,282" latime={9} />
      <rect x="446" y="166" width="54" height="13" fill="#a77543" />
      <path d="M455,166 v13 M464,166 v13 M473,166 v13 M482,166 v13 M491,166 v13" stroke="#7d5330" strokeWidth="1" />
      <circle cx="497" cy="182" r="3" fill="#6b4220" />
      <circle cx="497" cy="163" r="3" fill="#6b4220" />
      <BarcaVasle x={515} y={203} r={-12} />
      <Moara x={312} y={112} />
      <Han x={108} y={148} />
      <Copac x={200} y={92} />
      <Copac x={228} y={106} s={0.85} />
      <Copac x={96} y={215} s={0.9} />
      <Copac x={170} y={185} s={0.7} />
      <Brad x={412} y={135} />
      <Brad x={398} y={235} s={0.85} />
    </>
  );
}

// --- Luceafărul ---
function Palat({ x, y }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse cx="4" cy="2" rx="40" ry="7" fill="rgba(0,0,0,.3)" />
      <rect x="-32" y="-34" width="52" height="34" fill="#5b5f8f" stroke="#3a3d63" strokeWidth="1.5" />
      {[0, 1, 2, 3].map((i) => (
        <rect key={i} x={-32 + i * 14} y="-40" width="8" height="7" fill="#5b5f8f" stroke="#3a3d63" strokeWidth="1.2" />
      ))}
      <rect x="-26" y="-24" width="7" height="10" rx="3.5" fill="#2a2d52" />
      <rect x="-12" y="-24" width="7" height="10" rx="3.5" fill="#ffd86b" />
      <path d="M2,0 L2,-12 Q8,-19 14,-12 L14,0 Z" fill="#2a2d52" />
      <rect x="20" y="-74" width="20" height="74" fill="#666aa0" stroke="#3a3d63" strokeWidth="1.5" />
      <path d="M17,-74 L30,-98 L43,-74 Z" fill="#2f325a" />
      {/* fereastra Cătălinei, luminată */}
      <circle cx="30" cy="-58" r="13" fill="#ffd86b" opacity=".55" filter="url(#h-blur-mic)" />
      <rect x="26" y="-64" width="8" height="12" rx="4" fill="#ffe08a" />
      <rect x="26" y="-38" width="8" height="10" rx="4" fill="#2a2d52" />
    </g>
  );
}

// Luceafărul: palatul cu fereastra Cătălinei, teii înfloriți și licuricii
function DecorLuceafarul() {
  const tei = { frunze: "#2a6a4c", lumina: "#3b8560", flori: "#f3e9a0" };
  return (
    <>
      <Drum d="M165,222 C200,215 225,190 250,168" latime={10} culoare="#6e6a91" lumina="#8d89b0" />
      <Palat x={258} y={166} />
      <Copac x={110} y={135} {...tei} />
      <Copac x={142} y={112} s={0.9} {...tei} />
      <Copac x={92} y={182} s={0.85} {...tei} />
      <Copac x={320} y={215} s={0.8} {...tei} />
      <Copac x={228} y={92} s={0.75} {...tei} />
      {[[120, 155], [155, 135], [96, 205], [205, 120], [300, 200], [70, 160], [250, 235], [190, 250]].map(([fx, fy], i) => (
        <circle key={i} className="h-licurici" cx={fx} cy={fy} r="2" fill="#fff59a" style={{ animationDelay: `-${(i * 0.37).toFixed(2)}s` }} />
      ))}
    </>
  );
}

// ---------- Pod de lemn cu frânghii ----------
function Pod({ a, b, deschis }) {
  const [x1, y1] = a;
  const [x2, y2] = b;
  const lungime = Math.hypot(x2 - x1, y2 - y1);
  const unghi = (Math.atan2(y2 - y1, x2 - x1) * 180) / Math.PI;
  const pas = 9;
  const scanduri = Math.floor(lungime / pas);
  const intervale = Math.max(1, Math.floor(lungime / 45));
  const stalpi = Array.from({ length: intervale + 1 }, (_, i) => (i * lungime) / intervale);

  return (
    <g transform={`translate(${x1.toFixed(1)} ${y1.toFixed(1)}) rotate(${unghi.toFixed(2)})`} filter={deschis ? undefined : "url(#h-gri)"} opacity={deschis ? 1 : 0.7}>
      {/* umbra pe apă */}
      {(deschis ? [[0, lungime]] : [[0, lungime * 0.38], [lungime * 0.62, lungime * 0.38]]).map(([x, w]) => (
        <g key={x}>
          <rect x={x} y="-8" width={w} height="20" rx="4" fill="rgba(5,40,70,.3)" transform="translate(3 7)" />
          {/* grinzile de sub scânduri */}
          <rect x={x} y="-9" width={w} height="3" fill="#6b4220" />
          <rect x={x} y="6" width={w} height="3" fill="#6b4220" />
        </g>
      ))}
      {Array.from({ length: scanduri }, (_, i) => {
        const x = i * pas + 2;
        // podul blocat are o bucată lipsă la mijloc
        if (!deschis && x > lungime * 0.38 && x < lungime * 0.62) return null;
        return <rect key={i} x={x} y="-10" width="6.5" height="20" rx="1" fill={i % 3 ? "#a77543" : "#b8854f"} stroke="#7d5330" strokeWidth=".8" />;
      })}
      {/* frânghiile de pe margini */}
      <path d={`M0,-11 Q${lungime / 2},-7 ${lungime},-11`} fill="none" stroke="#5a3a1f" strokeWidth="2.2" />
      <path d={`M0,11 Q${lungime / 2},15 ${lungime},11`} fill="none" stroke="#5a3a1f" strokeWidth="2.2" />
      {stalpi.map((x, i) => (
        <g key={i}>
          <circle cx={x} cy="-11" r="3.2" fill="#6b4220" />
          <circle cx={x} cy="11" r="3.2" fill="#6b4220" />
        </g>
      ))}
    </g>
  );
}

const DECOR = { harap: DecorHarap, moara: DecorMoara, luceafarul: DecorLuceafarul };

// Pentru capitolele fără desene proprii: câțiva copaci în jurul centrului
function DecorSimplu({ insula }) {
  const [cx, cy] = insula.centru;
  return (
    <>
      <Copac x={cx - 70} y={cy - 40} />
      <Copac x={cx + 60} y={cy - 55} s={0.85} />
      <Copac x={cx - 10} y={cy - 80} s={0.75} />
      <Copac x={cx + 95} y={cy + 5} s={0.7} />
    </>
  );
}

function Insula({ insula, blocata }) {
  const Decor = DECOR[insula.arta] ?? DecorSimplu;
  const c = { ...CULORI_IMPLICITE, ...insula.culori };
  const [cx, cy] = insula.centru;
  return (
    <g transform={`translate(${insula.tx} ${insula.ty}) scale(${insula.scara})`}>
      {/* apă mică în jurul insulei */}
      <path d={insula.nisip} fill={c.halo} opacity=".55" filter="url(#h-blur)" transform={scalare([cx, cy], 1.22)} />
      <path d={insula.nisip} fill={c.halo2} opacity=".5" filter="url(#h-blur-mic)" transform={scalare([cx, cy], 1.08)} />
      {/* spumă: un val care se tot lovește de mal */}
      <path className="h-spuma" d={insula.nisip} fill="none" stroke="#fff" strokeWidth="3" />
      <path d={insula.nisip} fill="none" stroke="#fff" strokeOpacity=".8" strokeWidth="5" filter="url(#h-coasta)" />
      <g filter="url(#h-coasta)">
        <path d={insula.nisip} fill={c.nisip} />
        <path d={insula.nisip} fill={c.iarba} transform={scalare([cx, cy], 0.86)} />
      </g>
      {Decor && <Decor insula={insula} />}
      {/* cover gri translucent, pe forma exactă a insulei */}
      {blocata && <path d={insula.nisip} fill="rgba(70,75,85,.62)" filter="url(#h-coasta)" />}
    </g>
  );
}

// ---------- Interfața peste hartă ----------
function Lacat({ className = "" }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.4} strokeLinecap="round" className={className}>
      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
      <rect x="4" y="10" width="16" height="11" rx="2.5" fill="currentColor" stroke="none" />
    </svg>
  );
}

// Cele 3 cerculețe de sub nivel: cameră (video), carte (materiale), play (întrebări). Gri → galben după ce le parcurgi.
function Cerculete({ activitati = {} }) {
  return (
    <span className="pointer-events-none absolute left-1/2 top-[160%] flex -translate-x-1/2 gap-[3px]">
      {ACTIVITATI.map(({ tip, Icon, eticheta }) => (
        <span
          key={tip}
          title={eticheta}
          className={`grid aspect-square w-[clamp(14px,1.9vw,20px)] place-items-center rounded-full border-2 shadow transition-colors duration-500 ${
            activitati[tip] ? "border-white bg-amber-400 text-amber-950" : "border-white/40 bg-slate-500/90 text-white/70"
          }`}
        >
          <Icon className="h-[58%] w-[58%]" />
        </span>
      ))}
    </span>
  );
}

// stare: "terminat" | "disponibil" | "blocat"
function ButonNivel({ nivel, punct, index, stare, activitati, onDeschide }) {
  const baza =
    "absolute z-[2] grid aspect-square w-[5.2%] min-w-[32px] -translate-x-1/2 -translate-y-1/2 place-items-center rounded-full border-4 font-black text-[clamp(13px,2vw,22px)] shadow-[0_5px_0_rgba(0,0,0,0.25)] transition-transform";
  const eticheta = (
    <span className="absolute left-1/2 top-[108%] -translate-x-1/2 whitespace-nowrap rounded-full bg-black/45 px-2 py-0.5 text-[clamp(9px,1.1vw,12px)] font-bold text-white">
      {nivel.titlu}
    </span>
  );

  if (stare === "blocat") {
    return (
      <button disabled title="Termină nivelul anterior" style={poz(punct)} className={`${baza} cursor-not-allowed border-gray-300 bg-gray-500`}>
        <span className="absolute -inset-1 rounded-full bg-slate-700/45" />
        <Lacat className="relative z-[1] h-[45%] w-[45%] text-white" />
        {eticheta}
        <Cerculete activitati={activitati} />
      </button>
    );
  }

  return (
    <button
      onClick={onDeschide}
      style={poz(punct)}
      aria-label={`${nivel.titlu}: alege videoclip, materiale sau întrebări`}
      className={`${baza} cursor-pointer border-white hover:scale-110 ${
        stare === "terminat" ? "bg-emerald-500 text-white" : "bg-amber-400 text-amber-950"
      }`}
    >
      {stare === "disponibil" && <span className="absolute -inset-2 animate-ping rounded-full border-[3px] border-amber-200" />}
      {stare === "terminat" ? "✓" : index + 1}
      {eticheta}
      <Cerculete activitati={activitati} />
    </button>
  );
}

// cursuri = lecțiile publicate ale materiei, cu progresul elevului (GET /materii/{id}/cursuri)
export default function HartaInsule({ cursuri, onActualizare }) {
  const insule = useMemo(() => construiesteInsule(cursuri), [cursuri]);
  // Harta se deschide la pagina cu prima insulă neterminată
  const [pagina, setPagina] = useState(() => {
    const prima = insule.findIndex((ins) => !ins.toate.every((n) => n.curs.progres?.quizPromovat));
    return Math.floor(Math.max(prima, 0) / PE_PAGINA);
  });
  const [deschis, setDeschis] = useState(null); // { insula, nivel } pentru tabul lecției
  const inchide = useCallback(() => setDeschis(null), []);

  const terminat = (n) => !!n.curs.progres?.quizPromovat;
  // „play” e parcurs când lecția e terminată (răspuns corect la întrebare)
  const activitatiNivel = (n) => ({ video: !!n.curs.progres?.videoVazut, materiale: !!n.curs.progres?.materialeCitite, play: terminat(n) });

  // O insulă e deschisă când cea dinainte e terminată sau când elevul a terminat deja ceva pe ea
  // (lecțiile adăugate ulterior într-o insulă terminată nu blochează la loc ce urmează).
  const insulaDeblocata = (i) => i === 0 || insule[i - 1].toate.every(terminat) || insule[i].toate.some(terminat);
  const stareNivel = (insula, j) => {
    const n = insula.toate[j];
    if (terminat(n)) return "terminat";
    if (j === 0 || terminat(insula.toate[j - 1])) return "disponibil";
    return "blocat";
  };

  const pagini = Math.max(1, Math.ceil(insule.length / PE_PAGINA));
  const start = pagina * PE_PAGINA;
  const vizibile = insule.slice(start, start + PE_PAGINA);

  return (
    <div className="mx-auto w-full max-w-5xl">
    <div className="harta-insule relative aspect-[16/10] w-full overflow-hidden rounded-3xl bg-[#1f8dc4] shadow-2xl ring-4 ring-white/10">
      <style>{CSS}</style>

      <svg viewBox={`0 0 ${L} ${H}`} className="absolute inset-0 h-full w-full" aria-hidden="true">
        <Definitii />
        <Apa />
        <Noapte />
        <DecorApa />
        {vizibile.map((insula, k) => (
          <Insula key={insula.id} insula={insula} blocata={!insulaDeblocata(start + k)} />
        ))}
        {PODURI.map((pod) => {
          const de = vizibile[pod.de];
          const la = vizibile[pod.la];
          if (!de || !la) return null;
          return <Pod key={`${pod.de}-${pod.la}`} a={peHarta(de, pod.a)} b={peHarta(la, pod.b)} deschis={insulaDeblocata(start + pod.la)} />;
        })}
        <Pescarusi />
      </svg>

      {vizibile.map((insula, k) => {
        const deblocata = insulaDeblocata(start + k);
        return (
          <div key={insula.id}>
            {insula.nivele.map((n, j) => (
              <ButonNivel
                key={n.id}
                nivel={n}
                punct={peHarta(insula, [n.x, n.y])}
                index={j}
                stare={deblocata ? stareNivel(insula, j) : "blocat"}
                activitati={activitatiNivel(n)}
                onDeschide={() => setDeschis({ insula, nivel: n })}
              />
            ))}

            {!deblocata && (
              <div
                style={poz(peHarta(insula, insula.lacatPoz ?? insula.centru))}
                className="absolute z-[4] grid aspect-square w-[7%] -translate-x-1/2 -translate-y-1/2 place-items-center rounded-full bg-slate-900/75 text-white shadow-lg ring-2 ring-white/30"
              >
                <Lacat className="h-[55%] w-[55%]" />
              </div>
            )}

            <div
              style={poz(peHarta(insula, insula.titluPoz))}
              className={`absolute z-[5] flex -translate-x-1/2 -translate-y-1/2 flex-col items-center whitespace-nowrap rounded-2xl px-3.5 py-1 leading-tight shadow-md ${
                deblocata ? "bg-white text-amber-900" : "bg-slate-600 text-slate-100"
              }`}
            >
              <span className="text-[clamp(11px,1.6vw,16px)] font-extrabold">{insula.titlu}</span>
              <span className="text-[clamp(8px,1vw,11px)] font-semibold opacity-70">
                {insula.autor}
                {insula.ascunse > 0 ? ` · +${insula.ascunse} în listă` : ""}
              </span>
            </div>
          </div>
        );
      })}

      {deschis && (
        <TabNivel
          key={deschis.nivel.id}
          insula={deschis.insula}
          nivel={deschis.nivel}
          activitati={activitatiNivel(deschis.nivel)}
          onInchide={inchide}
          onActualizare={onActualizare}
        />
      )}
    </div>
      {pagini > 1 && (
        <div className="mt-3 flex items-center justify-center gap-3 text-white">
          <button className="rounded-full bg-white/90 px-4 py-2 font-black text-amber-900 shadow disabled:opacity-40" onClick={() => setPagina(pagina - 1)} disabled={pagina === 0}>
            ← Insulele anterioare
          </button>
          <span className="font-bold">
            {pagina + 1} / {pagini}
          </span>
          <button className="rounded-full bg-white/90 px-4 py-2 font-black text-amber-900 shadow disabled:opacity-40" onClick={() => setPagina(pagina + 1)} disabled={pagina === pagini - 1}>
            Insulele următoare →
          </button>
        </div>
      )}

      {/* Toate lecțiile, în listă (și cele care nu încap pe hartă) */}
      <div className="mt-5 grid gap-3 sm:grid-cols-2">
        {insule.map((insula, i) => (
          <section key={insula.id} className="rounded-2xl bg-white/95 p-4 shadow-lg">
            <h3 className="font-extrabold text-amber-900">{insula.titlu}</h3>
            <ul className="mt-2 grid gap-1.5">
              {insula.toate.map((n, j) => {
                const stare = insulaDeblocata(i) ? stareNivel(insula, j) : "blocat";
                return (
                  <li key={n.id}>
                    <button
                      disabled={stare === "blocat"}
                      onClick={() => setDeschis({ insula, nivel: n })}
                      className="flex w-full items-center gap-2 rounded-xl px-2 py-1.5 text-left text-slate-800 hover:bg-sky-50 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      <span className={`grid h-6 w-6 shrink-0 place-items-center rounded-full text-xs font-black ${stare === "terminat" ? "bg-emerald-500 text-white" : stare === "disponibil" ? "bg-amber-400 text-amber-950" : "bg-slate-300 text-slate-600"}`}>
                        {stare === "terminat" ? "✓" : j + 1}
                      </span>
                      <span className="min-w-0 flex-1 truncate font-semibold">{n.curs.titlu}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
