"use client";

import { useEffect, useState, type ReactElement } from "react";
import type { Opera, Material } from "@/lib/materiale";

const FONT = "'Fredoka', var(--font-fredoka), ui-rounded, system-ui, sans-serif";

/* ------------------------------------------------------------------ */
/*  Fiecare operă are un „reper” pe hartă: un obiect mare desenat.     */
/*  cx = centrul pe orizontală, eticheta / punctele = pozițiile pe Y.  */
/*  O operă nouă are nevoie de o intrare aici + un desen.              */
/* ------------------------------------------------------------------ */
type Reper = { cx: number; eticheta: number; puncte: number; noapte?: boolean; Desen: () => ReactElement };

const REPERE: Record<string, Reper> = {
  "harap-alb": { cx: 235, eticheta: 92, puncte: 398, Desen: Castel },
  "moara-cu-noroc": { cx: 585, eticheta: 300, puncte: 590, Desen: Moara },
  luceafarul: { cx: 805, eticheta: 72, puncte: 318, noapte: true, Desen: Stea },
};

/* ------------------------------ desene ------------------------------- */
function Steag({ x, y, c = "#f4b400" }: { x: number; y: number; c?: string }) {
  return (
    <g>
      <line x1={x} y1={y} x2={x} y2={y - 22} stroke="#6b4a2b" strokeWidth="2.5" />
      <path d={`M${x} ${y - 22} q 9 3 18 0 q -4 5 0 10 q -9 3 -18 0 Z`} fill={c} className="flutura" />
    </g>
  );
}

function Turn({ x, y, w, h, acoperis }: { x: number; y: number; w: number; h: number; acoperis: number }) {
  return (
    <g>
      <rect x={x - w / 2} y={y - h} width={w} height={h} fill="#efe6d2" />
      <rect x={x + w / 2 - w * 0.28} y={y - h} width={w * 0.28} height={h} fill="#ddd1b6" />
      <polygon points={`${x - w / 2 - 7},${y - h} ${x},${y - h - acoperis} ${x + w / 2 + 7},${y - h}`} fill="#d6453a" />
      <polygon points={`${x},${y - h - acoperis} ${x + w / 2 + 7},${y - h} ${x + 4},${y - h}`} fill="#b8372e" />
      <path d={`M${x - 5} ${y - h + 34} v-8 a5 5 0 0 1 10 0 v8 Z`} fill="#6b4a2b" />
      <Steag x={x} y={y - h - acoperis} />
    </g>
  );
}

/** Harap-Alb: castelul craiului, pe un deal. */
function Castel() {
  const y = 372;
  return (
    <g>
      <ellipse cx="235" cy={y + 6} rx="160" ry="26" fill="#4f9e37" />
      <ellipse cx="235" cy={y} rx="150" ry="22" fill="#79c95a" />
      {/* turnul central */}
      <Turn x={235} y={y - 40} w={64} h={132} acoperis={56} />
      {/* zidul */}
      <rect x="145" y={y - 92} width="180" height="92" fill="#efe6d2" />
      <rect x="145" y={y - 92} width="180" height="10" fill="#ddd1b6" />
      {Array.from({ length: 9 }, (_, i) => (
        <rect key={i} x={147 + i * 20} y={y - 104} width="12" height="13" fill="#efe6d2" />
      ))}
      <path d={`M213 ${y} v-38 a22 22 0 0 1 44 0 v38 Z`} fill="#6b4a2b" />
      {[220, 228, 236, 244, 252].map((x) => (
        <line key={x} x1={x} y1={y - 52} x2={x} y2={y} stroke="#4d3520" strokeWidth="2" />
      ))}
      {/* turnurile laterale */}
      <Turn x={140} y={y} w={46} h={128} acoperis={50} />
      <Turn x={330} y={y} w={46} h={128} acoperis={50} />
      {/* stema */}
      <path d={`M235 ${y - 78} l12 4 v10 q0 10 -12 15 q-12 -5 -12 -15 v-10 Z`} fill="#4a6fd1" stroke="#f4b400" strokeWidth="2" />
      {/* cal mic lângă castel */}
      <g transform={`translate(392 ${y + 2})`}>
        <ellipse cx="0" cy="2" rx="24" ry="4" fill="#000" opacity=".15" />
        <path d="M-18 -22 q 4 -10 22 -8 l 10 -12 q 6 -6 12 0 l 2 8 l -6 4 l -6 -2 l -2 12 q 0 8 -6 10 Z" fill="#fff8ee" />
        <rect x="-16" y="-14" width="5" height="16" rx="2" fill="#fff8ee" />
        <rect x="-8" y="-14" width="5" height="16" rx="2" fill="#f1e7d6" />
        <rect x="6" y="-14" width="5" height="16" rx="2" fill="#fff8ee" />
        <rect x="12" y="-14" width="5" height="16" rx="2" fill="#f1e7d6" />
        <path d="M-18 -20 q -10 4 -8 16" fill="none" stroke="#e7d7b8" strokeWidth="4" strokeLinecap="round" />
        <path d="M14 -38 q -6 6 -10 16" fill="none" stroke="#e7d7b8" strokeWidth="4" strokeLinecap="round" />
        <circle cx="20" cy="-36" r="1.5" fill="#333" />
      </g>
    </g>
  );
}

/** Moara cu noroc: moara cu roată, lângă râu. */
function Moara() {
  const x = 585, y = 545;
  return (
    <g>
      <ellipse cx={x} cy={y + 4} rx="110" ry="16" fill="#000" opacity=".15" />
      {/* roata */}
      <g className="roata" style={{ transformOrigin: `${x - 92}px ${y - 52}px` }}>
        <circle cx={x - 92} cy={y - 52} r="54" fill="none" stroke="#6b4a2b" strokeWidth="8" />
        <circle cx={x - 92} cy={y - 52} r="40" fill="none" stroke="#7d5a3c" strokeWidth="3" />
        {Array.from({ length: 8 }, (_, i) => (
          <g key={i} transform={`rotate(${i * 22.5} ${x - 92} ${y - 52})`}>
            <line x1={x - 146} y1={y - 52} x2={x - 38} y2={y - 52} stroke="#7d5a3c" strokeWidth="4" />
          </g>
        ))}
        {Array.from({ length: 12 }, (_, i) => (
          <rect key={i} x={x - 98} y={y - 114} width="12" height="14" fill="#a87845" transform={`rotate(${i * 30} ${x - 92} ${y - 52})`} />
        ))}
        <circle cx={x - 92} cy={y - 52} r="9" fill="#5b3b22" />
      </g>
      {/* clădirea */}
      <rect x={x - 62} y={y - 44} width="124" height="46" fill="#a9a39a" />
      {[0, 1, 2, 3].map((r) =>
        [0, 1, 2, 3, 4].map((c) => (
          <rect key={`${r}${c}`} x={x - 62 + c * 25 + (r % 2) * 12} y={y - 44 + r * 11.5} width="22" height="9.5" rx="2" fill="#bdb7ad" />
        ))
      )}
      <rect x={x - 58} y={y - 128} width="116" height="86" fill="#c9a77a" />
      {[0, 1, 2, 3, 4, 5, 6].map((i) => (
        <line key={i} x1={x - 58} y1={y - 120 + i * 12} x2={x + 58} y2={y - 120 + i * 12} stroke="#b38f62" strokeWidth="2" />
      ))}
      <polygon points={`${x - 76},${y - 124} ${x},${y - 186} ${x + 76},${y - 124}`} fill="#7d5a3c" />
      <polygon points={`${x},${y - 186} ${x + 76},${y - 124} ${x + 10},${y - 124}`} fill="#674a31" />
      <rect x={x + 26} y={y - 182} width="14" height="34" fill="#8a6a4f" />
      <circle cx={x + 33} cy={y - 194} r="7" fill="#fff" opacity=".7" className="fum" />
      <circle cx={x + 40} cy={y - 210} r="9" fill="#fff" opacity=".5" className="fum" style={{ animationDelay: "1s" }} />
      <path d={`M${x + 4} ${y} v-34 a14 14 0 0 1 28 0 v34 Z`} fill="#6b4a2b" />
      <rect x={x - 40} y={y - 104} width="24" height="22" rx="3" fill="#ffd36b" stroke="#6b4a2b" strokeWidth="3" />
      <rect x={x + 18} y={y - 104} width="24" height="22" rx="3" fill="#ffd36b" stroke="#6b4a2b" strokeWidth="3" />
      {/* sacii de făină */}
      <ellipse cx={x + 76} cy={y - 8} rx="12" ry="14" fill="#efe3c8" />
      <ellipse cx={x + 92} cy={y - 4} rx="10" ry="11" fill="#e4d6b6" />
      {/* stropi la roată */}
      <ellipse cx={x - 92} cy={y + 4} rx="40" ry="7" fill="#fff" opacity=".55" />
    </g>
  );
}

/** Luceafărul: steaua mare de pe cerul de noapte. */
function Stea() {
  const cx = 805, cy = 200;
  return (
    <g>
      <circle cx={cx} cy={cy} r="110" fill="url(#aura)" />
      <g className="raze" style={{ transformOrigin: `${cx}px ${cy}px` }}>
        {Array.from({ length: 12 }, (_, i) => (
          <line key={i} x1={cx} y1={cy - 72} x2={cx} y2={cy - 98} stroke="#fff3b0" strokeWidth="3" strokeLinecap="round" opacity=".6"
            transform={`rotate(${i * 30 + 15} ${cx} ${cy})`} />
        ))}
      </g>
      <g className="luceafar" style={{ transformOrigin: `${cx}px ${cy}px` }}>
        <path
          d={`M${cx} ${cy - 88} C ${cx + 8} ${cy - 22}, ${cx + 22} ${cy - 8}, ${cx + 88} ${cy}
              C ${cx + 22} ${cy + 8}, ${cx + 8} ${cy + 22}, ${cx} ${cy + 88}
              C ${cx - 8} ${cy + 22}, ${cx - 22} ${cy + 8}, ${cx - 88} ${cy}
              C ${cx - 22} ${cy - 8}, ${cx - 8} ${cy - 22}, ${cx} ${cy - 88} Z`}
          fill="#fff6c8"
        />
        <path
          d={`M${cx} ${cy - 50} C ${cx + 5} ${cy - 12}, ${cx + 12} ${cy - 5}, ${cx + 50} ${cy}
              C ${cx + 12} ${cy + 5}, ${cx + 5} ${cy + 12}, ${cx} ${cy + 50}
              C ${cx - 5} ${cy + 12}, ${cx - 12} ${cy + 5}, ${cx - 50} ${cy}
              C ${cx - 12} ${cy - 5}, ${cx - 5} ${cy - 12}, ${cx} ${cy - 50} Z`}
          fill="#ffe27a" transform={`rotate(45 ${cx} ${cy})`} opacity=".9"
        />
        <circle cx={cx} cy={cy} r="16" fill="#fff" />
      </g>
    </g>
  );
}

/* -------------------------------- harta ------------------------------ */
export default function HartaMateriale({ opere }: { opere: Opera[] }) {
  const [selectat, setSelectat] = useState<string | null>(null);
  const [parteInitiala, setParteInitiala] = useState<string | null>(null);
  const [pdf, setPdf] = useState<Material | null>(null);
  const opera = opere.find((o) => o.slug === selectat) ?? null;

  const deschide = (slug: string, parte?: string) => {
    setSelectat(slug);
    setParteInitiala(parte ?? null);
  };

  useEffect(() => {
    const esc = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      if (pdf) setPdf(null);
      else setSelectat(null);
    };
    window.addEventListener("keydown", esc);
    return () => window.removeEventListener("keydown", esc);
  }, [pdf]);

  return (
    <div className="relative" style={{ fontFamily: FONT }}>
      <style>{CSS}</style>

      <div className="relative overflow-hidden rounded-[28px] border-4 border-[#1b4f7a] shadow-xl">
        <div className="pointer-events-none absolute left-3 top-3 z-10 rounded-2xl bg-white/90 px-4 py-2 shadow-md sm:left-5 sm:top-5">
          <div className="text-lg font-semibold leading-tight text-[#1b4f7a] sm:text-2xl">Materiale</div>
          <div className="text-[11px] text-[#1b4f7a]/70 sm:text-sm">Alege o operă ca să deschizi materialele</div>
        </div>

        <svg viewBox="0 0 1000 640" className="block h-auto w-full select-none" style={{ fontFamily: FONT }} role="group" aria-label="Harta operelor">
          <defs>
            <linearGradient id="cer" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#8fd6ff" />
              <stop offset=".42" stopColor="#5cb6ef" />
              <stop offset=".62" stopColor="#2a3f8f" />
              <stop offset="1" stopColor="#0c173f" />
            </linearGradient>
            <linearGradient id="deal1" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#a8de86" /><stop offset=".45" stopColor="#8ccf6c" />
              <stop offset=".65" stopColor="#3f5f7c" /><stop offset="1" stopColor="#24395c" />
            </linearGradient>
            <linearGradient id="deal2" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#7cc95b" /><stop offset=".45" stopColor="#6aba4b" />
              <stop offset=".65" stopColor="#2f4d68" /><stop offset="1" stopColor="#1c2f4f" />
            </linearGradient>
            <linearGradient id="deal3" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#62b445" /><stop offset=".45" stopColor="#56a63c" />
              <stop offset=".68" stopColor="#264260" /><stop offset="1" stopColor="#172845" />
            </linearGradient>
            <linearGradient id="rau" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#7fd0f5" /><stop offset="1" stopColor="#3a9ee0" />
            </linearGradient>
            <radialGradient id="aura">
              <stop offset="0" stopColor="#fff6c8" stopOpacity=".55" />
              <stop offset=".5" stopColor="#ffe27a" stopOpacity=".15" />
              <stop offset="1" stopColor="#ffe27a" stopOpacity="0" />
            </radialGradient>
            <radialGradient id="soare">
              <stop offset="0" stopColor="#fff3a8" /><stop offset=".6" stopColor="#ffd84d" />
              <stop offset="1" stopColor="#ffd84d" stopOpacity="0" />
            </radialGradient>
          </defs>

          {/* cer, soare, nori, stele */}
          <rect width="1000" height="640" fill="url(#cer)" />
          <circle cx="440" cy="62" r="46" fill="url(#soare)" />
          <circle cx="440" cy="62" r="24" fill="#ffe46b" />
          <Nor x={345} y={158} s={1} />
          <Nor x={545} y={120} s={0.75} d={3} />
          <Nor x={60} y={250} s={0.7} d={6} />
          <path d="M380 100 q6 -6 12 0 q6 -6 12 0 M410 84 q5 -5 10 0 q5 -5 10 0" fill="none" stroke="#2b5b85" strokeWidth="2" strokeLinecap="round" />
          {STELE.map(([x, y, r], i) => (
            <circle key={i} cx={x} cy={y} r={r} fill="#fff" className="stea" style={{ animationDelay: `${(i % 5) * 0.6}s` }} />
          ))}
          <path d="M955 40 a20 20 0 1 0 14 34 a16 16 0 1 1 -14 -34 Z" fill="#fff5cc" />

          {/* dealuri */}
          <path d="M0 300 Q 140 240 290 292 T 600 284 T 1000 300 V640 H0 Z" fill="url(#deal1)" />
          <path d="M0 395 Q 210 335 430 380 T 1000 360 V640 H0 Z" fill="url(#deal2)" />
          <path d="M0 485 Q 240 445 470 488 T 1000 470 V640 H0 Z" fill="url(#deal3)" />

          {/* râul */}
          <path d="M548 300 C 530 350, 505 380, 500 420 C 495 470, 470 520, 455 640 L 535 640 C 535 560, 525 500, 530 440 C 534 400, 555 350, 560 300 Z" fill="url(#rau)" />
          {[[520, 390], [500, 470], [490, 560], [505, 610]].map(([x, y], i) => (
            <path key={i} d={`M${x} ${y} q 6 -4 12 0`} fill="none" stroke="#fff" strokeOpacity=".6" strokeWidth="2" strokeLinecap="round" />
          ))}

          {/* drumul care leagă operele */}
          <path d="M300 410 C 350 460, 400 470, 425 500 M 690 548 C 730 530, 760 480, 780 440 C 795 410, 803 380, 805 350"
            fill="none" stroke="#e8cf92" strokeWidth="10" strokeLinecap="round" strokeDasharray="1 18" />

          {/* turnul Cătălinei, sub stea */}
          <g transform="translate(905 470)">
            <rect x="-14" y="-90" width="28" height="90" fill="#6b6585" />
            <polygon points="-20,-90 0,-122 20,-90" fill="#3e3a58" />
            <rect x="-5" y="-72" width="10" height="14" rx="5" fill="#ffd36b" className="fereastra" />
          </g>
          <Brad x={690} y={455} noapte />
          <Brad x={950} y={520} s={1.2} noapte />
          <Brad x={60} y={470} />
          <Brad x={95} y={500} s={1.2} />
          <Copac x={410} y={575} />
          <Copac x={720} y={600} noapte />
          <Copac x={385} y={315} s={0.8} />

          {/* reperele operelor */}
          {opere.map((o) => {
            const r = REPERE[o.slug];
            if (!r) return null;
            const { Desen } = r;
            const total = o.parti.reduce((s, p) => s + p.materiale.length, 0);
            const titlu = o.titlu.replace("Povestea lui ", "");
            const lat = Math.max(titlu.length * 12.5, o.autor.length * 7.8) + 48;
            return (
              <g
                key={o.slug}
                className={`reper ${selectat === o.slug ? "activ" : ""}`}
                role="button"
                tabIndex={0}
                aria-label={`${o.titlu}, ${o.autor} — deschide materialele`}
                onClick={() => deschide(o.slug)}
                onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && (e.preventDefault(), deschide(o.slug))}
              >
                <Desen />

                {/* cele 5 părți */}
                {o.parti.map((p, i) => {
                  const x = r.cx + (i - (o.parti.length - 1) / 2) * 36;
                  const are = p.materiale.length > 0;
                  return (
                    <g key={p.id} className="punct" onClick={(e) => { e.stopPropagation(); deschide(o.slug, p.id); }}>
                      <title>{`${p.titlu}${are ? ` · ${p.materiale.length} PDF` : " · în curând"}`}</title>
                      <circle cx={x} cy={r.puncte + 2} r="15" fill="#000" opacity=".22" />
                      <circle cx={x} cy={r.puncte} r="15" fill={are ? o.culoare : "#e9edf2"} stroke="#fff" strokeWidth="3" />
                      <text x={x} y={r.puncte + 5} textAnchor="middle" fontSize="14" fontWeight="700" fill={are ? "#fff" : "#8a96a3"}>{i + 1}</text>
                    </g>
                  );
                })}

                {/* eticheta */}
                <g transform={`translate(${r.cx} ${r.eticheta})`}>
                  <rect x={-lat / 2} y="-26" width={lat} height="56" rx="28" fill="#000" opacity=".18" transform="translate(0 4)" />
                  <rect x={-lat / 2} y="-26" width={lat} height="56" rx="28" fill={r.noapte ? "#2b3566" : "#fff"} />
                  <text textAnchor="middle" y="0" fontSize="21" fontWeight="700" fill={r.noapte ? "#fff" : "#7a3b12"}>{titlu}</text>
                  <text textAnchor="middle" y="20" fontSize="13" fontWeight="500" fill={r.noapte ? "#c5cdf2" : o.culoare}>{o.autor}</text>
                  <g transform={`translate(${lat / 2 - 6} -22)`}>
                    <circle r="15" fill={total ? "#22b573" : "#f4b400"} stroke="#fff" strokeWidth="3" />
                    <text y="5" textAnchor="middle" fontSize="14" fontWeight="700" fill="#fff">{total || "…"}</text>
                  </g>
                </g>
              </g>
            );
          })}
        </svg>

        <div className="pointer-events-none absolute bottom-3 left-3 hidden gap-3 rounded-xl bg-white/85 px-3 py-2 text-xs text-[#1b4f7a] shadow sm:flex">
          <span className="flex items-center gap-1.5"><i className="inline-block h-3 w-3 rounded-full bg-[#22b573]" /> are materiale</span>
          <span className="flex items-center gap-1.5"><i className="inline-block h-3 w-3 rounded-full bg-[#f4b400]" /> în curând</span>
          <span className="flex items-center gap-1.5"><i className="inline-block h-3 w-3 rounded-full border border-slate-300 bg-[#e9edf2]" /> 1–5 = părțile operei</span>
        </div>
      </div>

      {opera && <PanouOpera opera={opera} parteInitiala={parteInitiala} onClose={() => setSelectat(null)} onPdf={setPdf} />}

      {pdf && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/60 p-2 sm:p-6" onClick={() => setPdf(null)}>
          <div className="flex h-full w-full max-w-5xl flex-col overflow-hidden rounded-2xl bg-white" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center gap-3 border-b px-4 py-3">
              <span className="flex-1 truncate font-semibold">{pdf.titlu}</span>
              <a href={pdf.fisier} target="_blank" rel="noreferrer" className="text-sm underline opacity-70">Tab nou</a>
              <button onClick={() => setPdf(null)} className="text-2xl leading-none" aria-label="Închide">×</button>
            </div>
            <iframe src={pdf.fisier} title={pdf.titlu} className="flex-1" />
          </div>
        </div>
      )}
    </div>
  );
}

/* ---------------------------- decor mic ------------------------------ */
function Nor({ x, y, s = 1, d = 0 }: { x: number; y: number; s?: number; d?: number }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <g className="nor" style={{ animationDelay: `${d}s` }}>
        <ellipse cx="0" cy="0" rx="46" ry="16" fill="#fff" />
        <circle cx="-14" cy="-10" r="18" fill="#fff" />
        <circle cx="12" cy="-14" r="22" fill="#fff" />
        <ellipse cx="0" cy="6" rx="44" ry="8" fill="#e3f2fb" />
      </g>
    </g>
  );
}

function Brad({ x, y, s = 1, noapte = false }: { x: number; y: number; s?: number; noapte?: boolean }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse cx="0" cy="2" rx="11" ry="4" fill="#000" opacity=".15" />
      <rect x="-2.5" y="-6" width="5" height="8" fill="#7a5230" />
      <polygon points="0,-36 -13,-4 13,-4" fill={noapte ? "#1f3a40" : "#2f7d3b"} />
      <polygon points="0,-36 -6,-18 0,-4" fill={noapte ? "#2a4a50" : "#3f9a4a"} />
    </g>
  );
}

function Copac({ x, y, s = 1, noapte = false }: { x: number; y: number; s?: number; noapte?: boolean }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse cx="0" cy="2" rx="12" ry="4" fill="#000" opacity=".15" />
      <rect x="-2.5" y="-10" width="5" height="12" fill="#7a5230" />
      <circle cx="0" cy="-18" r="13" fill={noapte ? "#24424a" : "#3c9a45"} />
      <circle cx="-4" cy="-22" r="5" fill={noapte ? "#305660" : "#5bbb5a"} />
    </g>
  );
}

/* ----------------------- panoul cu materiale ------------------------- */
function PanouOpera({
  opera,
  parteInitiala,
  onClose,
  onPdf,
}: {
  opera: Opera;
  parteInitiala: string | null;
  onClose: () => void;
  onPdf: (m: Material) => void;
}) {
  useEffect(() => {
    if (parteInitiala) document.getElementById(`parte-${parteInitiala}`)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [opera.slug, parteInitiala]);

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-end bg-[#0b2b4a]/40 sm:items-stretch" onClick={onClose}>
      <aside
        className="panou flex max-h-[85vh] w-full flex-col overflow-hidden rounded-t-[28px] bg-[#fdf6e6] shadow-2xl sm:max-h-none sm:w-[440px] sm:rounded-none sm:rounded-l-[28px]"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-label={`Materiale: ${opera.titlu}`}
      >
        <header className="relative px-6 pb-5 pt-6" style={{ background: `${opera.culoare}2e` }}>
          <button
            onClick={onClose}
            className="absolute right-4 top-4 flex h-9 w-9 items-center justify-center rounded-full bg-white/80 text-xl shadow"
            aria-label="Închide"
          >
            ×
          </button>
          <div className="flex items-center gap-4">
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white text-3xl shadow">{opera.emoji}</div>
            <div className="pr-8">
              <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: opera.culoare }}>{opera.gen}</div>
              <h2 className="text-xl font-semibold leading-tight text-[#3b2410]">{opera.titlu}</h2>
              <div className="text-sm text-[#3b2410]/70">{opera.autor} · {opera.an}</div>
            </div>
          </div>
          <p className="mt-3 text-sm text-[#3b2410]/80">{opera.descriere}</p>
        </header>

        <div className="flex-1 space-y-3 overflow-y-auto px-5 py-5">
          {opera.parti.map((p, i) => (
            <section
              key={p.id}
              id={`parte-${p.id}`}
              className={`scroll-mt-4 rounded-2xl border-2 bg-white p-4 ${parteInitiala === p.id ? "" : "border-transparent"}`}
              style={parteInitiala === p.id ? { borderColor: opera.culoare } : undefined}
            >
              <div className="flex items-start gap-3">
                <span
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-[3px] border-white text-sm font-bold shadow"
                  style={p.materiale.length ? { background: opera.culoare, color: "#fff" } : { background: "#e9edf2", color: "#8a96a3" }}
                >
                  {i + 1}
                </span>
                <div>
                  <h3 className="font-semibold text-[#3b2410]">{p.titlu}</h3>
                  <p className="text-sm text-[#3b2410]/65">{p.descriere}</p>
                </div>
              </div>

              {p.materiale.length === 0 ? (
                <p className="mt-3 rounded-xl bg-[#f4b400]/15 px-3 py-2 text-sm text-[#7a5a00]">În curând — materialele vin aici.</p>
              ) : (
                <ul className="mt-3 space-y-2">
                  {p.materiale.map((m) => (
                    <li key={m.fisier} className="flex flex-wrap items-center gap-2 rounded-xl bg-[#f6f1e4] px-3 py-2">
                      <span className="rounded-md bg-[#d6453a] px-1.5 py-0.5 text-[10px] font-bold text-white">PDF</span>
                      <span className="min-w-0 flex-1 text-sm font-medium text-[#3b2410]">{m.titlu}</span>
                      <button onClick={() => onPdf(m)} className="rounded-full px-3 py-1 text-sm font-semibold text-white shadow-sm" style={{ background: opera.culoare }}>
                        Citește
                      </button>
                      <a href={m.fisier} download className="rounded-full bg-white px-3 py-1 text-sm font-medium text-[#3b2410] shadow-sm">
                        Descarcă
                      </a>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          ))}
        </div>
      </aside>
    </div>
  );
}

/* --------------------------- date decor ------------------------------ */
const STELE: [number, number, number][] = [
  [640, 40, 1.6], [700, 22, 1.4], [690, 120, 2], [740, 300, 1.2], [905, 30, 1.8], [985, 130, 1.5],
  [650, 190, 1.3], [990, 230, 1.4], [930, 300, 1.6], [620, 110, 1.2], [960, 190, 1.4], [880, 110, 1.3],
  [720, 240, 1.5], [870, 330, 1.2], [610, 260, 1.1],
];

const CSS = `
.reper{cursor:pointer;transition:transform .25s ease,filter .25s ease;outline:none}
.reper:hover,.reper:focus-visible,.reper.activ{transform:translateY(-6px);filter:drop-shadow(0 0 14px rgba(255,255,255,.7))}
.punct{transition:transform .15s ease;transform-box:fill-box;transform-origin:center}
.punct:hover{transform:scale(1.18)}
.stea{animation:clipeste 3s ease-in-out infinite}
.fereastra{animation:clipeste 5s ease-in-out infinite}
@keyframes clipeste{50%{opacity:.25}}
.luceafar{animation:pulseaza 4s ease-in-out infinite}
@keyframes pulseaza{50%{transform:scale(.9)}}
.raze{animation:roteste 40s linear infinite}
.roata{animation:roteste 7s linear infinite}
@keyframes roteste{to{transform:rotate(360deg)}}
.fum{animation:fum 3s ease-out infinite;transform-box:fill-box;transform-origin:center}
@keyframes fum{0%{transform:translateY(6px) scale(.6);opacity:0}30%{opacity:.7}100%{transform:translateY(-18px) scale(1.3);opacity:0}}
.flutura{animation:flutura 1.6s ease-in-out infinite;transform-box:fill-box;transform-origin:left center}
@keyframes flutura{50%{transform:scaleX(.8) skewY(4deg)}}
.nor{animation:nor 12s ease-in-out infinite}
@keyframes nor{50%{transform:translateX(14px)}}
.panou{animation:intra .3s ease-out}
@keyframes intra{from{transform:translateY(40px);opacity:0}}
@media (min-width:640px){@keyframes intra{from{transform:translateX(60px);opacity:0}}}
@media (prefers-reduced-motion:reduce){.stea,.fereastra,.luceafar,.raze,.roata,.fum,.flutura,.nor,.panou{animation:none}}
`;
