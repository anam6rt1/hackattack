"use client";

/* ==================================================================
   TEMELE SITE-ULUI + PROFILUL  (BacQuest)
   3 teme: "acvatic", "realist", "pixel".

   1) În src/app/layout.tsx, totul se învelește în <TemaSite>:
        <html lang="ro" suppressHydrationWarning>
          <body><TemaSite> ...restul site-ului... </TemaSite></body>
        </html>
      → fundalul animat, sunetul și culorile temei apar pe TOATE paginile.

   2) Pagina de profil (src/app/profil/page.tsx):
        export { default } from "@/components/ProfilTeme";

   3) Pentru colegi, în paginile lor:
        - culori: var(--card-bg), var(--text), var(--text-2), var(--accent), var(--accent-2)
        - fonturi: var(--font-titlu), var(--font-text)
        - clase gata făcute: "tema-card", "tema-buton", "tema-text-2"
        - insule în stilul temei: <Insula />  (import { Insula } din acest fișier)
        - harta: <HartaOperelor opere={...} onNivel={(opera, nivel) => ...} />
        - tema curentă: const { tema } = useTema();
        - avatarul ales: const { avatar } = useAvatar();  →  <Avatar id={avatar} size={48} />
   ================================================================== */


import { createContext, useCallback, useContext, useEffect, useId, useState } from "react";

export type TemaId = "acvatic" | "realist" | "pixel";

export const TEME: { id: TemaId; nume: string; descriere: string }[] = [
  { id: "acvatic", nume: "Acvatic", descriere: "Lume acvatică, cu bule și peștișori" },
  { id: "realist", nume: "Realist", descriere: "Apă, lumină și nori ca în realitate" },
  { id: "pixel", nume: "Pixel", descriere: "Joc retro pe 16 biți" },
];

const CHEIE = "bac_tema";

/* ---------------- Tema activă, salvată în browser ---------------- */
type ValoareTema = { tema: TemaId; setTema: (t: TemaId) => void };
const TemaContext = createContext<ValoareTema | null>(null);

function citesteTemaSalvata(): TemaId {
  let salvata: string | null = null;
  try {
    salvata = localStorage.getItem(CHEIE);
  } catch {}
  if (salvata === "copilaresc") salvata = "acvatic"; // numele vechi
  return (TEME.some((x) => x.id === salvata) ? salvata : "acvatic") as TemaId;
}

function useTemaLocala(activ: boolean): ValoareTema {
  const [tema, setTemaState] = useState<TemaId>("acvatic");


  useEffect(() => {
    if (!activ) return;
    const t = citesteTemaSalvata();
    setTemaState(t);
    document.documentElement.dataset.theme = t;
  }, [activ]);

  const setTema = useCallback((t: TemaId) => {
    const aplica = () => {
      document.documentElement.dataset.theme = t;
      setTemaState(t);
    };
    // tranziție lină între teme, unde browserul o permite
    const doc = document as Document & { startViewTransition?: (cb: () => void) => void };
    if (doc.startViewTransition) doc.startViewTransition(aplica);
    else aplica();
    try {
      localStorage.setItem(CHEIE, t);
    } catch {}
  }, []);

  return { tema, setTema };
}

/* Tema curentă, oriunde în site: const { tema, setTema } = useTema(); */
export function useTema(): ValoareTema {
  const dinSite = useContext(TemaContext);
  const locala = useTemaLocala(dinSite === null);
  return dinSite ?? locala;
}

/* Se pune o singură dată, în layout.tsx, în jurul întregului site */
export function TemaSite({ children }: { children: React.ReactNode }) {
  const valoare = useTemaLocala(true);
  return (
    <TemaContext.Provider value={valoare}>
      <style>{CSS}</style>
      
      <TemaFundal />
      <ButonSunet tema={valoare.tema} />
      <div className="bq-site">{children}</div>
    </TemaContext.Provider>
  );
}

/* ---------------- Insula (se desenează în stilul temei) ----------------
   Are trei desene: acvatic, realist și pixel.
   CSS-ul temei arată doar desenul potrivit. */
export function Insula({ casa = true, forma = casa ? "far" : "golf" }: { casa?: boolean; forma?: FormaReala }) {
  return (
    <svg viewBox="0 0 220 120" className="bq-insula-svg">
      <g className="desen-cartoon">
        <ellipse className="i-umbra" cx="112" cy="92" rx="104" ry="22" />
        <ellipse className="i-nisip" cx="110" cy="80" rx="100" ry="32" />
        <ellipse className="i-iarba" cx="110" cy="68" rx="84" ry="27" />
        <ellipse className="i-lumina" cx="92" cy="60" rx="46" ry="11" />
        <path className="i-drum" d="M60 82 Q100 66 130 74 T176 62" />
        <circle className="i-copac" cx="52" cy="58" r="11" />
        <circle className="i-copac" cx="66" cy="50" r="9" />
        <circle className="i-copac i-copac-2" cx="168" cy="54" r="10" />
        {casa && (
          <g>
            <rect className="i-perete" x="112" y="42" width="30" height="22" rx="2" />
            <polygon className="i-acoperis" points="107,44 127,26 147,44" />
            <rect className="i-usa" x="123" y="52" width="8" height="12" rx="1.5" />
          </g>
        )}
      </g>
      <InsulaRealista forma={forma} />
      <PixelArt grid={gridInsula(casa)} className="desen-pixel" />
    </svg>
  );
}

export type FormaReala = "far" | "sat" | "stanca" | "mica" | "golf";

/* O casă realistă: perete, latura din umbră, acoperiș în două ape, ferestre */
function CasaReala({ x, y, w, h, perete, acoperis, lumina = false }: {
  x: number; y: number; w: number; h: number; perete: string; acoperis: string; lumina?: boolean;
}) {
  const varf = y - h * 0.62;
  return (
    <g>
      <rect x={x} y={y} width={w} height={h} fill={perete} />
      <rect x={x + w * 0.62} y={y} width={w * 0.38} height={h} fill="#000" opacity="0.22" />
      <polygon points={`${x - 1.4},${y + 0.6} ${x + w / 2},${varf} ${x + w + 1.4},${y + 0.6}`} fill={acoperis} />
      <polygon points={`${x + w / 2},${varf} ${x + w + 1.4},${y + 0.6} ${x + w / 2},${y + 0.6}`} fill="#000" opacity="0.25" />
      <rect x={x + w * 0.18} y={y + h * 0.3} width={Math.max(1.4, w * 0.14)} height={h * 0.26} fill={lumina ? "#ffd27a" : "#24282c"} />
      <rect x={x + w * 0.66} y={y + h * 0.3} width={Math.max(1.4, w * 0.12)} height={h * 0.26} fill="#1a1d20" />
      <rect x={x - 0.5} y={y + h - 0.8} width={w + 1} height="1.2" fill="#000" opacity="0.3" />
    </g>
  );
}

/* Brazi și copaci întunecați, pentru siluete neregulate */
function Copac({ x, y, s = 1 }: { x: number; y: number; s?: number }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <rect x="-0.6" y="-2" width="1.2" height="3" fill="#2a1d15" />
      <path d="M0 -12 L3.5 -5 L2 -5.4 L4.5 -1.5 L-4.5 -1.5 L-2 -5.4 L-3.5 -5 Z" fill="#1d2e1f" />
      <path d="M0 -12 L3.5 -5 L2 -5.4 L4.5 -1.5 L0 -1.5 Z" fill="#000" opacity="0.25" />
    </g>
  );
}

/* Gradientele folosite de desenele realiste — definite o singură dată, mereu vizibile */
export function DefinitiiReale() {
  return (
    <svg width="0" height="0" style={{ position: "absolute" }} aria-hidden="true">
        <defs>
        <linearGradient id="r-nisip" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#b9a17a" />
          <stop offset="1" stopColor="#6e5c43" />
        </linearGradient>
        <linearGradient id="r-deal" x1="0.2" y1="0" x2="0.8" y2="1">
          <stop offset="0" stopColor="#4d6340" />
          <stop offset="0.55" stopColor="#2c3d27" />
          <stop offset="1" stopColor="#18231a" />
        </linearGradient>
        <linearGradient id="r-stanca" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#8a8d8c" />
          <stop offset="1" stopColor="#3b3f42" />
        </linearGradient>
        <linearGradient id="r-faleza" x1="0" y1="0" x2="1" y2="0.4">
          <stop offset="0" stopColor="#8b8579" />
          <stop offset="0.5" stopColor="#5d5850" />
          <stop offset="1" stopColor="#2f2c28" />
        </linearGradient>
        <linearGradient id="r-reflex" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0b1a22" stopOpacity="0.55" />
          <stop offset="1" stopColor="#0b1a22" stopOpacity="0" />
        </linearGradient>
        <filter id="hm-pix" x="0" y="0" width="1" height="1" colorInterpolationFilters="sRGB">
          <feFlood x="2" y="2" width="1" height="1" />
          <feComposite width="5" height="5" />
          <feTile result="grila" />
          <feComposite in="SourceGraphic" in2="grila" operator="in" />
          <feMorphology operator="dilate" radius="2.5" />
        </filter>
        <linearGradient id="av-umbra-fata" x1="0" y1="0" x2="1" y2="0.3">
          <stop offset="0.5" stopColor="#8a4a2a" stopOpacity="0" />
          <stop offset="1" stopColor="#8a4a2a" stopOpacity="0.28" />
        </linearGradient>
        <radialGradient id="av-lumina" cx="0.28" cy="0.2" r="0.7">
          <stop offset="0" stopColor="#ffffff" stopOpacity="0.32" />
          <stop offset="1" stopColor="#ffffff" stopOpacity="0" />
        </radialGradient>
        <linearGradient id="av-umbra" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0.5" stopColor="#0a1e3a" stopOpacity="0" />
          <stop offset="1" stopColor="#0a1e3a" stopOpacity="0.22" />
        </linearGradient>
        <radialGradient id="r-far-lumina">
          <stop offset="0" stopColor="#ffe8b0" stopOpacity="0.9" />
          <stop offset="1" stopColor="#ffe8b0" stopOpacity="0" />
        </radialGradient>
      </defs>
    </svg>
  );
}

function InsulaRealista({ forma }: { forma: FormaReala }) {
  return (
    <g className="desen-real">

      {forma === "far" && (
        <g>
          <path d="M12 88 C60 112 160 114 210 90 L206 118 L16 118 Z" fill="url(#r-reflex)" />
          <path d="M8 86 C22 76 40 74 58 72 C90 66 140 64 168 70 C188 74 204 78 214 86 C196 94 150 98 110 98 C66 98 30 95 8 86 Z" fill="url(#r-nisip)" />
          <path className="r-spuma" d="M8 86 C30 95 66 98 110 98 C150 98 196 94 214 86" fill="none" />
          <path d="M22 80 C30 62 46 52 62 48 C78 34 98 28 118 34 C136 30 154 40 166 52 C182 58 194 68 200 80 C160 84 70 86 22 80 Z" fill="url(#r-deal)" />
          <path d="M40 60 l4 -10 l3 7 l4 -14 l4 12 l3 -8 l4 10 l5 -16 l4 13 l3 -6 l4 9 l5 -18 l4 15 l3 -9 l4 11 l4 -7 l3 8 V68 H40 Z" fill="#1b2a1c" opacity="0.85" />
          <path d="M128 52 l3 -9 l3 6 l4 -13 l3 11 l4 -8 l3 9 l4 -12 l3 10 l3 -5 l4 8 V62 H128 Z" fill="#1d2d1f" opacity="0.8" />
          <CasaReala x={140} y={62} w={11} h={8} perete="#d8d2c4" acoperis="#7b3626" lumina />
          <CasaReala x={156} y={66} w={8} h={6} perete="#c9a45c" acoperis="#4a3a30" />
          <path d="M176 84 l8 -12 l10 -2 l8 8 l2 8 Z" fill="url(#r-stanca)" />
          <path d="M18 86 l6 -9 l9 1 l4 8 Z" fill="url(#r-stanca)" />
          <path d="M196 90 l5 -6 l6 2 l2 5 Z" fill="#4a4e50" />
          <circle cx="104" cy="16" r="16" fill="url(#r-far-lumina)" className="r-far-lumina" />
          <path d="M98 50 L100 22 H108 L110 50 Z" fill="#e6e2da" />
          <path d="M99 40 H109 L109.6 46 H98.4 Z" fill="#9b3b32" />
          <path d="M100.5 30 H107.5 L108 34 H100 Z" fill="#9b3b32" />
          <rect x="99" y="16" width="10" height="6" fill="#f3d58a" />
          <path d="M97 16 L104 9 L111 16 Z" fill="#3a3a3a" />
          <rect x="97" y="21" width="14" height="2" fill="#3a3a3a" />
          <path d="M104 50 L110 50 L109 24 Z" fill="#000" opacity="0.18" />
        </g>
      )}

      {forma === "sat" && (
        <g>
          <path d="M6 86 C50 106 170 108 216 88 L212 118 L10 118 Z" fill="url(#r-reflex)" />
          <path d="M4 84 C20 78 44 76 70 74 C110 70 150 70 182 74 C200 77 212 80 218 85 C190 94 140 96 100 95 C60 95 26 92 4 84 Z" fill="url(#r-nisip)" />
          <path className="r-spuma" d="M4 84 C26 92 60 95 100 95 C140 96 190 94 218 85" fill="none" />
          <path d="M16 80 C34 70 58 62 84 60 C110 56 138 58 160 62 C180 66 196 72 206 80 C160 84 60 86 16 80 Z" fill="url(#r-deal)" />
          <Copac x={28} y={77} s={0.9} /><Copac x={36} y={75} s={1.1} /><Copac x={190} y={77} s={0.9} />
          <CasaReala x={50} y={66} w={12} h={9} perete="#e9e4d8" acoperis="#9a4630" lumina />
          <CasaReala x={66} y={62} w={15} h={11} perete="#d6a94e" acoperis="#6b3424" />
          <CasaReala x={85} y={63} w={10} h={8} perete="#a9c0d0" acoperis="#3f4a55" />
          {/* biserica */}
          <rect x="100" y="48" width="7" height="22" fill="#ece7dc" />
          <rect x="104" y="48" width="3" height="22" fill="#000" opacity="0.2" />
          <path d="M99 48 L103.5 36 L108 48 Z" fill="#3a4652" />
          <rect x="102.6" y="52" width="1.8" height="3" fill="#24282c" />
          <CasaReala x={110} y={60} w={16} h={11} perete="#e3c3b8" acoperis="#8b3a2e" lumina />
          <CasaReala x={130} y={64} w={11} h={8} perete="#f0e6c8" acoperis="#7a4a32" />
          <CasaReala x={145} y={66} w={9} h={7} perete="#b88a6a" acoperis="#3d2f28" />
          <Copac x={162} y={72} s={1.2} /><Copac x={170} y={74} />
          <path d="M196 88 l4 -7 l7 0 l4 6 Z" fill="url(#r-stanca)" />
          {/* debarcader */}
          <rect x="54" y="92" width="22" height="2" fill="#4a3628" />
          <rect x="56" y="93" width="1.2" height="5" fill="#2e2219" /><rect x="72" y="93" width="1.2" height="5" fill="#2e2219" />
        </g>
      )}

      {forma === "stanca" && (
        <g>
          <path d="M30 94 C70 114 150 114 190 94 L186 118 L34 118 Z" fill="url(#r-reflex)" />
          <path d="M26 94 C40 88 60 86 90 86 C130 86 170 88 192 94 C160 100 70 100 26 94 Z" fill="url(#r-nisip)" />
          <path className="r-spuma" d="M26 94 C70 100 160 100 192 94" fill="none" />
          <path d="M38 92 C42 72 50 48 66 34 C80 22 102 20 118 30 C134 40 146 60 156 74 C164 84 172 90 176 92 Z" fill="url(#r-faleza)" />
          <path d="M60 58 L84 60 M54 72 L90 74 M118 50 L140 56 M124 66 L150 72" stroke="#2a2724" strokeWidth="1.2" opacity="0.6" />
          <path d="M60 40 C72 26 100 20 120 32 C108 38 80 42 60 40 Z" fill="#33472d" />
          <Copac x={64} y={40} s={0.9} /><Copac x={116} y={34} s={0.8} />
          <CasaReala x={86} y={22} w={14} h={10} perete="#ece6da" acoperis="#8a3b2b" lumina />
          <path d="M150 92 l6 -10 l8 2 l6 8 Z" fill="url(#r-stanca)" />
        </g>
      )}

      {forma === "mica" && (
        <g>
          <path d="M50 94 C80 110 140 110 170 94 L166 116 L54 116 Z" fill="url(#r-reflex)" />
          <path d="M44 92 C62 82 90 80 112 80 C140 80 162 84 176 92 C150 100 70 100 44 92 Z" fill="url(#r-nisip)" />
          <path className="r-spuma" d="M44 92 C70 100 150 100 176 92" fill="none" />
          <path d="M70 88 C80 76 104 72 124 76 C140 79 150 84 156 88 C130 91 92 91 70 88 Z" fill="url(#r-deal)" />
          <CasaReala x={98} y={70} w={13} h={10} perete="#d9a441" acoperis="#5e3326" lumina />
          <Copac x={82} y={86} s={1.1} /><Copac x={130} y={84} s={0.9} />
          <path d="M160 94 l4 -6 l6 1 l3 5 Z" fill="#4a4e50" />
        </g>
      )}

      {forma === "golf" && (
        <g>
          <path d="M10 90 C60 112 150 112 212 90 L208 118 L14 118 Z" fill="url(#r-reflex)" />
          <path d="M6 88 C14 70 30 60 44 64 C58 68 60 80 76 82 C96 84 100 64 120 58 C142 52 170 60 186 70 C200 78 210 84 214 90 C170 98 60 100 6 88 Z" fill="url(#r-nisip)" />
          <path className="r-spuma" d="M6 88 C60 100 170 98 214 90" fill="none" />
          <path d="M12 84 C18 70 30 64 42 67 C52 70 54 78 62 82 C40 86 24 86 12 84 Z" fill="url(#r-deal)" />
          <path d="M88 80 C96 66 112 58 130 56 C150 54 172 62 186 72 C196 78 202 82 204 86 C160 88 110 86 88 80 Z" fill="url(#r-deal)" />
          <Copac x={22} y={80} /><Copac x={30} y={76} s={1.2} /><Copac x={44} y={78} s={0.9} />
          <Copac x={176} y={78} s={1.1} /><Copac x={186} y={80} />
          <CasaReala x={110} y={66} w={13} h={10} perete="#f1ece2" acoperis="#2f5f7a" />
          <CasaReala x={128} y={62} w={18} h={12} perete="#c97a5a" acoperis="#4a2b22" lumina />
          <CasaReala x={150} y={68} w={10} h={8} perete="#e8d48a" acoperis="#6b3a2a" />
          <path d="M70 92 l5 -6 l6 1 l3 5 Z" fill="#4a4e50" />
        </g>
      )}
    </g>
  );
}

/* ---------------- Desene pixel, 16 biți (fiecare caracter = o culoare) ----------------
   Desenele se generează din cod: formă → umbre → contur închis la culoare. */
const PALETA_PIXEL: Record<string, string> = {
  B: "#1a1230",
  "1": "#c4f57a", "2": "#86dc52", "3": "#4aa83a", "4": "#2e7a33",
  "5": "#e8a453", "6": "#b86e2e", "7": "#84481c", "8": "#5a2f12",
  s: "#d9ccb6", z: "#8f8273",
  L: "#a8ea72", M: "#5cbf4c", N: "#2f8a3c", O: "#1f6a33",
  t: "#9a5c2c", u: "#5c3418",
  P: "#f4f4f4", p: "#a9a9c0", R: "#ff4d4d", r: "#b82a35",
  Y: "#ffd84a", y: "#e8812a",
  W: "#ffffff", w: "#d6e4ff", v: "#a9c0f0",
  H: "#c0702f", h: "#7a3f18", k: "#f0c878", S: "#fffaf0", Q: "#d6cdb8",
  U: "#fff1a0", V: "#ffc23f", X: "#ff9a2e",
  A: "#7fd6a0", C: "#3f9f78", E: "#2b7a62", F: "#1e5a4c",
};

function PixelArt({ grid, className = "", celula = 2.5 }: { grid: string[]; className?: string; celula?: number }) {
  const rects: React.ReactNode[] = [];
  grid.forEach((rand, y) => {
    let x = 0;
    while (x < rand.length) {
      const c = rand[x];
      let lung = 1;
      while (x + lung < rand.length && rand[x + lung] === c) lung++;
      if (c !== "." && PALETA_PIXEL[c]) {
        rects.push(<rect key={`${x}-${y}`} x={x * celula} y={y * celula} width={lung * celula + 0.02} height={celula + 0.02} fill={PALETA_PIXEL[c]} />);
      }
      x += lung;
    }
  });
  return <g className={className} shapeRendering="crispEdges">{rects}</g>;
}

type Grila = string[][];
const grilaGoala = (W: number, H: number): Grila => Array.from({ length: H }, () => Array(W).fill("."));
const pune = (g: Grila, x: number, y: number, c: string) => {
  if (y >= 0 && y < g.length && x >= 0 && x < g[0].length) g[y][x] = c;
};
// contur închis în jurul tuturor formelor
function contur(g: Grila): string[] {
  const H = g.length, W = g[0].length;
  const out = g.map((r) => [...r]);
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++) {
      if (g[y][x] !== ".") continue;
      const vecini = [g[y - 1]?.[x], g[y + 1]?.[x], g[y]?.[x - 1], g[y]?.[x + 1]];
      if (vecini.some((v) => v && v !== ".")) out[y][x] = "B";
    }
  return out.map((r) => r.join(""));
}
// un cerc umbrit (lumina vine din stânga-sus)
function cercUmbrit(g: Grila, cx: number, cy: number, r: number, tonuri: string[]) {
  for (let y = Math.floor(cy - r); y <= cy + r; y++)
    for (let x = Math.floor(cx - r); x <= cx + r; x++) {
      const d = Math.hypot(x - cx, y - cy);
      if (d > r) continue;
      const lumina = (x - cx) + (y - cy) + d * 0.4;
      const i = lumina < -r * 0.6 ? 0 : lumina < 0 ? 1 : lumina < r * 0.7 ? 2 : 3;
      pune(g, x, y, tonuri[Math.min(i, tonuri.length - 1)]);
    }
}
// bucata de pământ cu iarbă (insulă sau platformă)
function teren(g: Grila, x0: number, x1: number, sus: number, adanc: number) {
  const cx = (x0 + x1) / 2, hw0 = (x1 - x0) / 2;
  for (let y = sus; y < sus + adanc; y++) {
    const t = (y - sus - 7) / (adanc - 7);
    const hw = y < sus + 7 ? hw0 - (y === sus ? 3 : y === sus + 1 ? 1 : 0) : Math.round(hw0 * (1 - Math.pow(Math.max(0, t), 1.5)));
    if (hw < 1) break;
    const st = Math.round(cx - hw), dr = Math.round(cx + hw);
    for (let x = st; x <= dr; x++) {
      let c: string;
      const r = y - sus;
      if (r < 7) {
        c = r < 2 ? "1" : r < 4 ? "2" : r < 6 ? "3" : "4";
        if (x > dr - 6 && r < 4) c = r < 2 ? "2" : "3";
        if (r === 6 && (x * 7) % 9 < 3) c = "3";
      } else {
        const banda = (y + Math.round(Math.sin(x / 5) * 1.6)) % 7;
        c = banda < 3 ? "6" : banda < 5 ? "7" : banda === 5 ? "6" : "8";
        if (x < st + 3) c = banda < 4 ? "5" : "6";
        if (x > dr - 3) c = "8";
        if (t > 0.6 && c === "6") c = "7";
        if ((x * 13 + y * 7) % 41 === 0 && x < dr - 3) { c = "s"; pune(g, x + 1, y, "z"); }
        if (y < sus + 10 && (x * 7) % 9 < 3 && y === sus + 7) c = "4"; // iarbă care atârnă
      }
      if (g[y][x] === ".") g[y][x] = c;
    }
  }
  for (let x = x0 + 3; x <= x1 - 4; x += 5) {
    pune(g, x, sus - 1, "1");
    if ((x / 5) % 2 < 1) { pune(g, x + 1, sus - 1, "1"); pune(g, x + 1, sus - 2, "2"); }
  }
}

const cachePixel: Record<string, string[]> = {};
function memo(cheie: string, f: () => string[]) {
  return cachePixel[cheie] ?? (cachePixel[cheie] = f());
}

function gridInsula(cuSteag: boolean): string[] {
  return memo("insula" + cuSteag, () => {
    const g = grilaGoala(88, 48);
    // copacul
    for (let y = 15; y <= 21; y++) { pune(g, 19, y, "t"); pune(g, 20, y, "t"); pune(g, 21, y, "u"); }
    cercUmbrit(g, 14, 12, 6, ["L", "M", "N", "O"]);
    cercUmbrit(g, 26, 12, 6, ["L", "M", "N", "O"]);
    cercUmbrit(g, 20, 8, 7, ["L", "M", "N", "O"]);
    // tufiș
    cercUmbrit(g, 60, 18, 4, ["L", "M", "N", "O"]);
    // floare
    for (let y = 16; y <= 20; y++) pune(g, 50, y, "3");
    [[50, 13], [49, 14], [51, 14], [50, 15]].forEach(([x, y]) => pune(g, x, y, "Y"));
    pune(g, 50, 14, "y");
    if (cuSteag) {
      for (let y = 3; y <= 21; y++) { pune(g, 71, y, "P"); pune(g, 72, y, "p"); }
      for (let x = 73; x <= 81; x++) {
        const val = x % 4 < 2 ? 0 : 1;
        for (let y = 4 + val; y <= 9 + val; y++) pune(g, x, y, y === 9 + val || x === 81 ? "r" : "R");
      }
    }
    teren(g, 5, 82, 22, 25);
    return contur(g);
  });
}

function gridPlatforma(): string[] {
  return memo("platforma", () => {
    const g = grilaGoala(48, 22);
    cercUmbrit(g, 36, 5, 3, ["L", "M", "N", "O"]);
    teren(g, 3, 44, 8, 13);
    return contur(g);
  });
}

function gridNor(): string[] {
  return memo("nor", () => {
    const g = grilaGoala(50, 22);
    ([[12, 13, 7], [22, 9, 8], [33, 11, 7], [41, 14, 5]] as const).forEach(([cx, cy, r]) => {
      for (let y = cy - r; y <= cy + r; y++)
        for (let x = cx - r; x <= cx + r; x++)
          if (Math.hypot(x - cx, y - cy) <= r && y <= 18) pune(g, x, y, y >= 17 ? "v" : y >= 15 ? "w" : "W");
    });
    for (let x = 6; x <= 44; x++) { pune(g, x, 17, "v"); pune(g, x, 18, "v"); }
    pune(g, 18, 5, "W"); pune(g, 17, 6, "W");
    return contur(g);
  });
}

function gridBarca(): string[] {
  return memo("barca", () => {
    const g = grilaGoala(34, 30);
    for (let y = 3; y <= 20; y++) {
      const lat = Math.floor((y - 3) * 0.75);
      for (let x = 17; x <= 17 + lat; x++) pune(g, x, y, x >= 17 + lat - 1 ? "Q" : "S");
    }
    for (let y = 7; y <= 20; y++) {
      const lat = Math.floor((y - 7) * 0.55);
      for (let x = 15 - lat; x <= 15; x++) pune(g, x, y, "Q");
    }
    for (let y = 1; y <= 22; y++) pune(g, 16, y, "u");
    for (let x = 17; x <= 20; x++) { pune(g, x, 1, "R"); pune(g, x, 2, "r"); }
    for (let x = 2; x <= 31; x++) pune(g, x, 21, "k");
    for (let y = 22; y <= 25; y++)
      for (let x = 2 + (y - 21); x <= 31 - (y - 21); x++) pune(g, x, y, y === 25 ? "h" : "H");
    return contur(g);
  });
}

function gridSoare(): string[] {
  return memo("soare", () => {
    const g = grilaGoala(30, 30);
    const c = 14.5;
    for (let i = 0; i < 8; i++) {
      const a = (i * Math.PI) / 4;
      for (let d = 10; d <= 13; d++) pune(g, Math.round(c + Math.cos(a) * d), Math.round(c + Math.sin(a) * d), "V");
    }
    for (let y = 0; y < 30; y++)
      for (let x = 0; x < 30; x++) {
        const d = Math.hypot(x - c, y - c);
        if (d <= 7.5) pune(g, x, y, d < 4 && x < c && y < c ? "W" : d < 5.5 ? "U" : "V");
      }
    return contur(g);
  });
}

function gridDealuri(): string[] {
  return memo("dealuri", () => {
    const W = 200, H = 26;
    const g = grilaGoala(W, H);
    for (let x = 0; x < W; x++) {
      const h = Math.round(13 + 5 * Math.sin(x / 11) + 3 * Math.sin(x / 4.7 + 1) + 1.5 * Math.sin(x / 2.3));
      for (let y = H - h; y < H; y++) {
        const r = y - (H - h);
        let c = r === 0 ? "A" : r < 3 ? "C" : (x + y) % 2 === 0 && r < 6 ? "E" : r < 6 ? "C" : "E";
        if (r > 9) c = (x + y) % 2 === 0 ? "F" : "E";
        g[y][x] = c;
      }
    }
    return contur(g);
  });
}

export function Barca() {
  return (
    <svg viewBox="0 0 60 60" className="bq-barca-svg">
      <g className="desen-cartoon">
        <polygon className="b-vela" points="30,6 30,40 50,40" />
        <polygon className="b-vela b-vela-2" points="28,12 28,40 14,40" />
        <line className="b-catarg" x1="29" y1="4" x2="29" y2="44" />
        <path className="b-corp" d="M8 42 H52 L45 52 H15 Z" />
      </g>
      <g className="desen-real">
        <path d="M30 4 C38 14 44 28 46 40 L30 40 Z" fill="#d9d6cf" />
        <path d="M30 4 C38 14 44 28 46 40 L40 40 C38 28 34 16 30 4 Z" fill="#a9a59c" />
        <path d="M28 10 C22 20 18 30 16 40 L28 40 Z" fill="#c4c0b7" />
        <line x1="29" y1="3" x2="29" y2="43" stroke="#2c2c2c" strokeWidth="1.2" />
        <path d="M6 42 H54 C52 47 48 50 42 51 H16 C11 50 8 47 6 42 Z" fill="#1d2b36" />
        <path d="M6 42 H54 L53.4 44 H6.6 Z" fill="#c9c3b6" />
        <path d="M14 53 C24 55 38 55 48 53" stroke="#ffffff" strokeOpacity="0.25" strokeWidth="1.5" fill="none" />
      </g>
      <g className="desen-pixel" transform="translate(-4 2) scale(0.92)"><PixelArt grid={gridBarca()} celula={2} /></g>
    </svg>
  );
}

/* Decorurile lumii acvatice (tema Acvatic) */
const BULE = Array.from({ length: 14 }, (_, i) => ({
  left: `${(i * 29 + 5) % 96}%`,
  delay: `${-((i * 1.7) % 12)}s`,
  dur: `${8 + ((i * 5) % 7)}s`,
  size: `${8 + ((i * 7) % 18)}px`,
}));

function DecorAcvatic() {
  return (
    <div className="bq-acvatic">
      <div className="bq-raze">
        {[0, 1, 2, 3, 4].map((i) => <span key={i} style={{ left: `${8 + i * 21}%`, animationDelay: `${-i * 1.6}s` }} />)}
      </div>
      <div className="bq-bule">
        {BULE.map((b, i) => (
          <span key={i} style={{ left: b.left, animationDelay: b.delay, animationDuration: b.dur, width: b.size, height: b.size }} />
        ))}
      </div>
      <svg className="bq-alge" viewBox="0 0 1200 200" preserveAspectRatio="none">
        {[60, 150, 260, 930, 1040, 1130].map((x, i) => (
          <path key={i} className={`alga a${i % 3}`} style={{ transformOrigin: `${x}px 200px` }}
            d={`M${x} 200 C${x - 20} 160 ${x + 20} 130 ${x} 90 C${x - 18} 60 ${x + 14} 40 ${x + 2} ${10 + (i % 3) * 20}`} />
        ))}
      </svg>
      <svg viewBox="0 0 120 70" className="bq-peste p1">
        <path d="M18 35 L2 18 L6 35 L2 52 Z" fill="#ff8a6b" stroke="#7a2e22" strokeWidth="2.5" strokeLinejoin="round" />
        <path d="M60 14 C66 2 84 2 82 16" fill="#ff6f61" stroke="#7a2e22" strokeWidth="2.5" />
        <ellipse cx="58" cy="35" rx="44" ry="22" fill="#ffb07a" stroke="#7a2e22" strokeWidth="2.5" />
        <path d="M40 18 C46 30 46 40 40 52" stroke="#ff6f61" strokeWidth="5" fill="none" />
        <circle cx="86" cy="31" r="7" fill="#fff" stroke="#7a2e22" strokeWidth="2" />
        <circle cx="88" cy="31" r="3.5" fill="#1d1d1d" />
      </svg>
      <svg viewBox="0 0 120 70" className="bq-peste p2">
        <path d="M18 35 L2 20 L6 35 L2 50 Z" fill="#9be7ff" stroke="#1d4a5a" strokeWidth="2.5" strokeLinejoin="round" />
        <ellipse cx="58" cy="35" rx="40" ry="18" fill="#c9f3ff" stroke="#1d4a5a" strokeWidth="2.5" />
        <circle cx="84" cy="32" r="5" fill="#1d1d1d" />
      </svg>
      <div className="bq-banc">
        {[0, 1, 2, 3, 4].map((i) => (
          <svg key={i} viewBox="0 0 40 20" style={{ left: `${(i % 3) * 34}px`, top: `${i * 14}px` }}>
            <path d="M8 10 L1 3 L3 10 L1 17 Z M10 10 C14 2 32 2 38 10 C32 18 14 18 10 10 Z" fill="rgba(255,255,255,.45)" />
          </svg>
        ))}
      </div>
      {/* pisicuța care se uită */}
      <svg viewBox="0 0 160 120" className="bq-pisica">
        <path d="M20 120 C14 70 30 34 80 34 C130 34 146 70 140 120 Z" fill="#1f1b24" />
        <path d="M30 60 L26 10 L64 38 Z M130 60 L134 10 L96 38 Z" fill="#1f1b24" />
        <path d="M34 44 L32 22 L52 38 Z M126 44 L128 22 L108 38 Z" fill="#4b3a52" />
        <circle cx="56" cy="78" r="21" fill="#ffe36e" stroke="#0d0b10" strokeWidth="3" />
        <circle cx="104" cy="78" r="21" fill="#ffe36e" stroke="#0d0b10" strokeWidth="3" />
        <circle cx="58" cy="80" r="13" fill="#1a1530" />
        <circle cx="102" cy="80" r="13" fill="#1a1530" />
        <path d="M63 72 l2 5 l5 2 l-5 2 l-2 5 l-2 -5 l-5 -2 l5 -2 Z" fill="#fff" />
        <path d="M107 72 l2 5 l5 2 l-5 2 l-2 5 l-2 -5 l-5 -2 l5 -2 Z" fill="#fff" />
        <circle cx="52" cy="86" r="2.5" fill="#fff" />
        <circle cx="96" cy="86" r="2.5" fill="#fff" />
        <path d="M76 100 Q80 104 84 100" stroke="#ff9eb5" strokeWidth="3" fill="none" strokeLinecap="round" />
      </svg>
    </div>
  );
}

/* Decorurile temei pixel */
const STELE_PIXEL = Array.from({ length: 9 }, (_, i) => ({ left: `${(i * 31 + 4) % 96}%`, top: `${(i * 13 + 4) % 40}%`, delay: `${-(i * 0.4)}s` }));

function DecorPixel() {
  return (
    <div className="bq-pixel-decor">
      <svg className="px-soare" viewBox="0 0 90 90"><PixelArt grid={gridSoare()} celula={3} /></svg>
      {STELE_PIXEL.map((s, i) => (
        <span key={i} className="px-stea" style={{ left: s.left, top: s.top, animationDelay: s.delay }} />
      ))}
      <svg className="px-nor n1" viewBox="0 0 125 55"><PixelArt grid={gridNor()} /></svg>
      <svg className="px-nor n2" viewBox="0 0 125 55"><PixelArt grid={gridNor()} /></svg>
      <svg className="px-nor n3" viewBox="0 0 125 55"><PixelArt grid={gridNor()} /></svg>
      <svg className="px-dealuri" viewBox="0 0 1200 156" preserveAspectRatio="xMidYMax slice"><PixelArt grid={gridDealuri()} celula={6} /></svg>
      <svg className="px-platforma" viewBox="0 0 120 55"><PixelArt grid={gridPlatforma()} /></svg>
    </div>
  );
}

function Nor({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 120 50" className={`bq-nor ${className}`}>
      <path d="M18 42 Q2 42 6 30 Q8 20 22 22 Q26 6 46 10 Q58 0 72 10 Q92 4 96 22 Q114 22 112 34 Q112 44 98 42 Z" />
    </svg>
  );
}





/* ---------------- Valuri, orizont, vânt ---------------- */
function caleVal(lat: number, perioada: number, amp: number, baza: number, inchis: number | null) {
  let d = `M0 ${baza}`;
  for (let x = 0; x < lat; x += perioada) {
    const p = perioada;
    d += ` C${x + p * 0.28} ${baza} ${x + p * 0.36} ${baza - amp} ${x + p * 0.5} ${baza - amp}`;
    d += ` C${x + p * 0.62} ${baza - amp} ${x + p * 0.7} ${baza} ${x + p} ${baza}`;
  }
  return inchis === null ? d : `${d} V${inchis} H0 Z`;
}

// Valurile realiste: mici lângă orizont, mari în față (perspectivă)
const STRATURI_REALE = [
  { top: "1.5%", h: 8, per: 36, amp: 1.6, dur: 60 },
  { top: "7%", h: 12, per: 64, amp: 2.6, dur: 48 },
  { top: "16%", h: 18, per: 104, amp: 4.5, dur: 38 },
  { top: "30%", h: 26, per: 160, amp: 7, dur: 30 },
  { top: "49%", h: 36, per: 236, amp: 10, dur: 23 },
  { top: "72%", h: 48, per: 320, amp: 14, dur: 17 },
];

function ValuriRealiste() {
  return (
    <div className="bq-valuri-real">
      {STRATURI_REALE.map((v, i) => (
        <div key={i} className="vr-strat" style={{ top: v.top, height: v.h, animationDelay: `${-i * 0.8}s` }}>
          <svg viewBox={`0 0 2400 ${v.h}`} preserveAspectRatio="none"
            style={{ animationDuration: `${v.dur}s`, animationDirection: i % 2 ? "reverse" : "normal" }}>
            <path d={caleVal(2400, v.per * 2, v.amp, v.amp + 1, v.h)} fill="rgba(0,12,22,.16)" />
            <path d={caleVal(2400, v.per * 2, v.amp, v.amp + 1, null)} fill="none"
              stroke="rgba(214,230,240,.26)" strokeWidth={1 + i * 0.35} strokeLinecap="round" />
          </svg>
        </div>
      ))}
    </div>
  );
}

// Linia orizontului realistă: țărmuri îndepărtate și hula mării (nu o linie dreaptă)
function caleTarm() {
  let d = "M0 60";
  for (let x = 0; x <= 1200; x += 6) {
    const munte = Math.max(0, Math.sin((x - 40) / 120) * 30) * (x < 420 ? 1 : 0.15);
    const dealuri = 6 + 4 * Math.sin(x / 37) + 3 * Math.sin(x / 13 + 1) + 1.5 * Math.sin(x / 5.3);
    const insulite = x > 760 && x < 840 ? 10 * Math.sin(((x - 760) / 80) * Math.PI) : 0;
    const h = Math.max(1.5, munte + dealuri * (x > 470 && x < 740 ? 0.25 : 1) + insulite);
    d += ` L${x} ${(60 - h).toFixed(1)}`;
  }
  return d + " L1200 60 Z";
}
const CALE_TARM = caleTarm();
const CALE_HULA = caleVal(2400, 46, 2.4, 6, 14);

function OrizontReal() {
  return (
    <div className="bq-orizont-real">
      <svg className="or-tarm" viewBox="0 0 1200 60" preserveAspectRatio="none">
        <defs>
          <linearGradient id="or-ceata" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#3c4856" />
            <stop offset="1" stopColor="#5d646a" />
          </linearGradient>
        </defs>
        <path d={CALE_TARM} fill="url(#or-ceata)" />
      </svg>
      <svg className="or-hula" viewBox="0 0 2400 14" preserveAspectRatio="none">
        <path d={CALE_HULA} fill="var(--mare-1)" />
      </svg>
    </div>
  );
}

// Valurile mari, desenate, la suprafață (tema Acvatic)
const CALE_ACV = [caleVal(2400, 120, 9, 12, 40), caleVal(2400, 150, 11, 13, 40), caleVal(2400, 100, 8, 11, 60)];
const CREASTA_ACV = [caleVal(2400, 120, 9, 12, null), caleVal(2400, 150, 11, 13, null), caleVal(2400, 100, 8, 11, null)];
function ValuriAcvatice() {
  const culori = ["#b4e2ff", "#79c3f2", "#3aa2e6"];
  return (
    <div className="bq-valuri-acv">
      {[0, 1, 2].map((i) => (
        <div key={i} className={`va-strat s${i}`}>
          <svg viewBox={`0 0 2400 ${i === 2 ? 60 : 40}`} preserveAspectRatio="none">
            <path d={CALE_ACV[i]} fill={culori[i]} />
            <path d={CREASTA_ACV[i]} fill="none" stroke="#1d4a6a" strokeWidth="2.5" />
            <path d={CREASTA_ACV[i]} fill="none" stroke="#fff7e2" strokeWidth="3" strokeDasharray="18 26"
              transform="translate(0 5)" strokeLinecap="round" />
          </svg>
        </div>
      ))}
    </div>
  );
}

// Vântul cu sclipici (tema Acvatic)
const SCLIPICI = Array.from({ length: 22 }, (_, i) => ({
  top: `${(i * 17 + 3) % 34}%`,
  delay: `${-((i * 1.9) % 14)}s`,
  dur: `${9 + ((i * 5) % 7)}s`,
  size: 9 + ((i * 3) % 9),
  culoare: ["#ffffff", "#fff3a0", "#ffd1e8", "#c8fff6"][i % 4],
}));
function VantSclipici() {
  return (
    <div className="bq-vant">
      {[0, 1, 2, 3, 4].map((i) => (
        <svg key={i} className={`vant-rafala r${i}`} viewBox="0 0 260 70">
          <path d="M2 46 C70 46 110 40 150 26 C180 16 200 28 192 40 C186 48 172 44 174 34 C176 26 190 24 198 30" />
          <path d="M30 58 C80 58 120 54 160 46" className="scurt" />
        </svg>
      ))}
      {SCLIPICI.map((p, i) => (
        <span key={i} className="sclipici" style={{ top: p.top, animationDelay: p.delay, animationDuration: p.dur, width: p.size, height: p.size, background: p.culoare }} />
      ))}
    </div>
  );
}

/* Păsări realiste care dau din aripi (tema Realist) */
const ARIPI = [
  "M30 15 C26 8 18 3 6 2 C15 7 23 11 30 15 C37 11 45 7 54 2 C42 3 34 8 30 15 Z",
  "M30 15 C25 11 16 9 4 10 C14 13 23 14 30 15 C37 14 46 13 56 10 C44 9 35 11 30 15 Z",
  "M30 15 C25 16 16 19 6 24 C15 22 23 18 30 15 C37 18 45 22 54 24 C44 19 35 16 30 15 Z",
];
const PASARI = [
  { top: "10%", w: 64, dur: 30, del: -4, bat: 0.62, inv: false },
  { top: "20%", w: 40, dur: 42, del: -20, bat: 0.5, inv: false },
  { top: "5%", w: 28, dur: 56, del: -35, bat: 0.42, inv: true },
  { top: "28%", w: 20, dur: 66, del: -12, bat: 0.38, inv: true },
  { top: "14%", w: 32, dur: 48, del: -40, bat: 0.46, inv: false },
];
function Pasari() {
  return (
    <div className="bq-pasari">
      {PASARI.map((p, i) => (
        <div key={i} className={`pasare ${p.inv ? "inv" : ""}`} style={{ top: p.top, animationDuration: `${p.dur}s`, animationDelay: `${p.del}s` }}>
          <svg viewBox="0 0 60 30" width={p.w} style={{ animationDelay: `${-i * 0.7}s` }}>
            <path fill="#10171c">
              <animate attributeName="d" dur={`${p.bat * (i % 2 ? 3 : 2.4)}s`} repeatCount="indefinite"
                values={[ARIPI[0], ARIPI[1], ARIPI[2], ARIPI[1], ARIPI[0], ARIPI[1], ARIPI[1], ARIPI[1], ARIPI[0]].join(";")}
                keyTimes="0;0.1;0.2;0.3;0.4;0.5;0.75;0.9;1" calcMode="spline"
                keySplines={Array(8).fill("0.45 0 0.55 1").join(";")} />
            </path>
            <path d="M22 15 C25 13.4 33 13.2 38 14.4 C40 14.6 42 14.8 43 15.4 C41 16 39 16 37 16 C32 16.8 26 16.6 22 15 Z" fill="#10171c" />
            <path d="M22 15 L17 13.4 L18.4 15.2 L17 16.8 Z" fill="#10171c" />
          </svg>
        </div>
      ))}
    </div>
  );
}

/* ---------------- Fundalul animat (cerul + oceanul) ---------------- */
export function TemaFundal() {
  return (
    <div className="bq-scena" aria-hidden="true">
      <DefinitiiReale />
      <div className="bq-cer" />
      <div className="bq-soare" />
      <div className="bq-luna" />
      <div className="bq-nori">
        <Nor className="n1" />
        <Nor className="n2" />
        <Nor className="n3" />
      </div>
      <div className="bq-mare">
        {/* sclipirea apei — doar la tema realistă */}
        <svg className="bq-sclipire" preserveAspectRatio="none">
          <filter id="bq-apa">
            <feTurbulence type="fractalNoise" baseFrequency="0.012 0.09" numOctaves="2" seed="3">
              <animate attributeName="baseFrequency" dur="18s" values="0.012 0.09;0.016 0.11;0.012 0.09" repeatCount="indefinite" />
            </feTurbulence>
            <feColorMatrix type="matrix" values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 0 0 1.4 -0.55" />
          </filter>
          <rect width="100%" height="100%" filter="url(#bq-apa)" />
        </svg>
        <div className="bq-reflex" />
        <ValuriRealiste />
        <svg className="bq-val v1" viewBox="0 0 1200 40" preserveAspectRatio="none">
          <path d="M0 20 Q75 0 150 20 T300 20 T450 20 T600 20 T750 20 T900 20 T1050 20 T1200 20 V40 H0 Z" />
        </svg>
        <svg className="bq-val v2" viewBox="0 0 1200 40" preserveAspectRatio="none">
          <path d="M0 20 Q75 0 150 20 T300 20 T450 20 T600 20 T750 20 T900 20 T1050 20 T1200 20 V40 H0 Z" />
        </svg>
        <div className="bq-valuri-mici">
          {Array.from({ length: 8 }, (_, i) => (
            <span key={i} style={{ left: `${(i * 13 + 4) % 92}%`, top: `${20 + ((i * 29) % 70)}%`, animationDelay: `${i * 0.6}s` }} />
          ))}
        </div>
      </div>
      <OrizontReal />
      <div className="bq-insula ins3"><Insula casa={false} forma="mica" /></div>
      <div className="bq-insula ins4"><Insula forma="stanca" /></div>
      <div className="bq-insula ins1"><Insula /></div>
      <div className="bq-insula ins2"><Insula casa={false} /></div>
      <div className="bq-insula ins5"><Insula forma="sat" /></div>
      <ValuriAcvatice />
      <VantSclipici />
      <Pasari />
      <div className="bq-barca"><Barca /></div>
      <div className="bq-pescarus p1" />
      <div className="bq-pescarus p2" />
      <DecorAcvatic />
      <DecorPixel />
    </div>
  );
}

/* ==================================================================
   SUNETE DE FUNDAL — generate din cod (Web Audio), fără fișiere audio.
   Acvatic: valuri blânde, bule, cutie muzicală.
   Realist: valuri, vioară, pescăruși.
   Pixel: melodie retro de joc (compusă original), bas și tobe 8-biți.
   Browserul permite sunetul doar după un click, de aceea există butonul 🔊.
   ================================================================== */

type Scena = { iesire: GainNode; surse: AudioScheduledSourceNode[]; timere: number[] };

const NOTE: Record<string, number> = { C: 0, "C#": 1, D: 2, "D#": 3, E: 4, F: 5, "F#": 6, G: 7, "G#": 8, A: 9, "A#": 10, B: 11 };
function frecventa(nota: string) {
  const m = nota.match(/^([A-G]#?)(\d)$/);
  if (!m) return 440;
  const midi = 12 * (Number(m[2]) + 1) + NOTE[m[1]];
  return 440 * Math.pow(2, (midi - 69) / 12);
}

class MotorSunet {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private reverb: ConvolverNode | null = null;
  private zgomot: AudioBuffer | null = null;
  private scena: Scena | null = null;
  private temaCurenta: TemaId | null = null;

  private init() {
    if (this.ctx) return this.ctx;
    const AC = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    const ctx = new AC();
    this.ctx = ctx;
    this.master = ctx.createGain();
    this.master.gain.value = 0.5;
    const compresor = ctx.createDynamicsCompressor();
    this.master.connect(compresor).connect(ctx.destination);
    // zgomot alb (pentru valuri și tobe)
    const buf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    this.zgomot = buf;
    // reverb (ecou de sală) generat
    const lung = ctx.sampleRate * 3;
    const ir = ctx.createBuffer(2, lung, ctx.sampleRate);
    for (let c = 0; c < 2; c++) {
      const ch = ir.getChannelData(c);
      for (let i = 0; i < lung; i++) ch[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / lung, 3);
    }
    this.reverb = ctx.createConvolver();
    this.reverb.buffer = ir;
    const umed = ctx.createGain();
    umed.gain.value = 0.5;
    this.reverb.connect(umed).connect(this.master);
    return ctx;
  }

  porneste(tema: TemaId) {
    const ctx = this.init();
    if (ctx.state === "suspended") ctx.resume();
    if (this.temaCurenta === tema && this.scena) return;
    this.opreste();
    this.temaCurenta = tema;
    const iesire = ctx.createGain();
    iesire.gain.setValueAtTime(0, ctx.currentTime);
    iesire.gain.linearRampToValueAtTime(1, ctx.currentTime + 1.5);
    iesire.connect(this.master!);
    this.scena = { iesire, surse: [], timere: [] };
    if (tema === "acvatic") this.acvatic(this.scena);
    if (tema === "realist") this.realist(this.scena);
    if (tema === "pixel") this.pixel(this.scena);
  }

  opreste() {
    const sc = this.scena;
    const ctx = this.ctx;
    this.scena = null;
    this.temaCurenta = null;
    if (!sc || !ctx) return;
    sc.timere.forEach((t) => clearInterval(t));
    sc.iesire.gain.cancelScheduledValues(ctx.currentTime);
    sc.iesire.gain.setValueAtTime(sc.iesire.gain.value, ctx.currentTime);
    sc.iesire.gain.linearRampToValueAtTime(0, ctx.currentTime + 0.8);
    window.setTimeout(() => {
      sc.surse.forEach((s) => { try { s.stop(); } catch {} });
      sc.iesire.disconnect();
    }, 1000);
  }

  /* ---------- piese de bază ---------- */

  // repetă un tipar muzical: f(t) programează un ciclu care începe la timpul t
  private bucla(sc: Scena, durata: number, f: (t: number) => void) {
    const ctx = this.ctx!;
    let urmator = ctx.currentTime + 0.1;
    const tic = () => {
      while (urmator < ctx.currentTime + 1.2) {
        f(urmator);
        urmator += durata;
      }
    };
    tic();
    sc.timere.push(window.setInterval(tic, 300));
  }

  // la intervale aleatorii (între min și max secunde)
  private aleator(sc: Scena, min: number, max: number, f: (t: number) => void) {
    const ctx = this.ctx!;
    let urmator = ctx.currentTime + min * Math.random() + 0.5;
    sc.timere.push(window.setInterval(() => {
      if (urmator < ctx.currentTime + 1) {
        f(urmator);
        urmator += min + Math.random() * (max - min);
      }
    }, 250));
  }

  private valuri(sc: Scena, frecv: number, volum: number, viteza: number) {
    const ctx = this.ctx!;
    const src = ctx.createBufferSource();
    src.buffer = this.zgomot;
    src.loop = true;
    const filtru = ctx.createBiquadFilter();
    filtru.type = "lowpass";
    filtru.frequency.value = frecv;
    const g = ctx.createGain();
    g.gain.value = volum;
    // valul crește și se retrage
    const lfo = ctx.createOscillator();
    lfo.frequency.value = viteza;
    const lfoG = ctx.createGain();
    lfoG.gain.value = volum * 0.85;
    lfo.connect(lfoG).connect(g.gain);
    const lfoF = ctx.createGain();
    lfoF.gain.value = frecv * 0.5;
    lfo.connect(lfoF).connect(filtru.frequency);
    src.connect(filtru).connect(g).connect(sc.iesire);
    src.start();
    lfo.start();
    sc.surse.push(src, lfo);
  }

  private ton(sc: Scena, t: number, f: number, durata: number, tip: OscillatorType, volum: number, opt: {
    atac?: number; spreReverb?: number; glisare?: number; vibrato?: number; filtru?: number;
  } = {}) {
    const ctx = this.ctx!;
    const o = ctx.createOscillator();
    o.type = tip;
    o.frequency.setValueAtTime(f, t);
    if (opt.glisare) o.frequency.exponentialRampToValueAtTime(opt.glisare, t + durata);
    const g = ctx.createGain();
    const atac = opt.atac ?? 0.005;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(volum, t + atac);
    g.gain.exponentialRampToValueAtTime(0.0001, t + durata);
    let nod: AudioNode = o;
    if (opt.vibrato) {
      const v = ctx.createOscillator();
      v.frequency.value = 5.6;
      const vg = ctx.createGain();
      vg.gain.setValueAtTime(0, t);
      vg.gain.linearRampToValueAtTime(opt.vibrato, t + Math.min(0.5, durata * 0.4));
      v.connect(vg).connect(o.frequency);
      v.start(t);
      v.stop(t + durata + 0.1);
    }
    if (opt.filtru) {
      const fl = ctx.createBiquadFilter();
      fl.type = "lowpass";
      fl.frequency.value = opt.filtru;
      nod.connect(fl);
      nod = fl;
    }
    nod.connect(g);
    g.connect(sc.iesire);
    if (opt.spreReverb && this.reverb) {
      const r = ctx.createGain();
      r.gain.value = opt.spreReverb;
      g.connect(r).connect(this.reverb);
    }
    o.start(t);
    o.stop(t + durata + 0.05);
  }

  private zgomotScurt(sc: Scena, t: number, durata: number, volum: number, tip: BiquadFilterType, frecv: number) {
    const ctx = this.ctx!;
    const src = ctx.createBufferSource();
    src.buffer = this.zgomot;
    const fl = ctx.createBiquadFilter();
    fl.type = tip;
    fl.frequency.value = frecv;
    const g = ctx.createGain();
    g.gain.setValueAtTime(volum, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + durata);
    src.connect(fl).connect(g).connect(sc.iesire);
    src.start(t, Math.random());
    src.stop(t + durata + 0.02);
  }

  /* ---------- Acvatic ---------- */
  private acvatic(sc: Scena) {
    this.valuri(sc, 700, 0.12, 0.13);
    // bule mici care fac „blup”
    this.aleator(sc, 0.5, 2.2, (t) => {
      const n = 1 + Math.floor(Math.random() * 3);
      for (let i = 0; i < n; i++) {
        const f = 350 + Math.random() * 400;
        this.ton(sc, t + i * 0.09, f, 0.12, "sine", 0.12, { glisare: f * 2.6 });
      }
    });
    // cutie muzicală, pentatonică
    const melodie = ["E6", "", "G6", "A6", "", "G6", "E6", "", "D6", "", "C6", "D6", "E6", "", "", "",
      "G6", "", "A6", "C7", "", "A6", "G6", "", "E6", "", "D6", "", "C6", "", "", ""];
    const pas = 0.32;
    this.bucla(sc, melodie.length * pas, (t0) => {
      melodie.forEach((n, i) => {
        if (!n) return;
        this.ton(sc, t0 + i * pas, frecventa(n), 1.4, "triangle", 0.05, { spreReverb: 0.6 });
        this.ton(sc, t0 + i * pas, frecventa(n) * 2, 0.5, "sine", 0.015);
      });
      ["C4", "G3", "A3", "F3"].forEach((n, i) =>
        this.ton(sc, t0 + i * 8 * pas, frecventa(n), 8 * pas, "sine", 0.05, { atac: 0.4, spreReverb: 0.4 }));
    });
  }

  /* ---------- Realist ---------- */
  private realist(sc: Scena) {
    this.valuri(sc, 420, 0.16, 0.09);
    // vioara: două unde „dinte de fierăstrău” ușor dezacordate, cu vibrato și filtru
    const vioara = (t: number, nota: string, d: number) => {
      const f = frecventa(nota);
      [-3, 3].forEach((cents) =>
        this.ton(sc, t, f * Math.pow(2, cents / 1200), d, "sawtooth", 0.022,
          { atac: 0.28, vibrato: 4.5, filtru: 2600, spreReverb: 0.7 }));
    };
    // melodie lentă, în la minor (compusă original)
    const fraza: [string, number][] = [
      ["A4", 2], ["C5", 1], ["E5", 1], ["D5", 2], ["C5", 1], ["B4", 1],
      ["A4", 3], ["E4", 1], ["F4", 2], ["G4", 1], ["A4", 1], ["B4", 2], ["G4", 2],
      ["C5", 2], ["B4", 1], ["A4", 1], ["G#4", 2], ["B4", 2], ["A4", 4],
    ];
    const bataie = 0.75;
    const total = fraza.reduce((s, [, d]) => s + d, 0) * bataie;
    this.bucla(sc, total + bataie * 2, (t0) => {
      let t = t0;
      fraza.forEach(([n, d]) => { vioara(t, n, d * bataie * 1.05); t += d * bataie; });
      // acompaniament grav (ca un violoncel)
      [["A2", 8], ["F2", 6], ["E2", 8]].forEach(([n, d], i, arr) => {
        const start = arr.slice(0, i).reduce((s, x) => s + (x[1] as number), 0) * bataie;
        this.ton(sc, t0 + start, frecventa(n as string), (d as number) * bataie, "sawtooth", 0.018,
          { atac: 0.8, filtru: 500, spreReverb: 0.5 });
      });
    });
    // pescăruși în depărtare
    this.aleator(sc, 5, 12, (t) => {
      const strigate = 2 + Math.floor(Math.random() * 3);
      for (let i = 0; i < strigate; i++) this.pescarus(sc, t + i * (0.32 + Math.random() * 0.15));
    });
  }

  private pescarus(sc: Scena, t: number) {
    const ctx = this.ctx!;
    const o = ctx.createOscillator();
    o.type = "sawtooth";
    const baza = 1300 + Math.random() * 300;
    o.frequency.setValueAtTime(baza, t);
    o.frequency.exponentialRampToValueAtTime(baza * 1.65, t + 0.06);
    o.frequency.exponentialRampToValueAtTime(baza * 0.8, t + 0.32);
    const bp = ctx.createBiquadFilter();
    bp.type = "bandpass";
    bp.frequency.value = 2000;
    bp.Q.value = 2.5;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.06, t + 0.03);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.34);
    o.connect(bp).connect(g);
    g.connect(sc.iesire);
    if (this.reverb) {
      const r = ctx.createGain();
      r.gain.value = 0.9;
      g.connect(r).connect(this.reverb);
    }
    o.start(t);
    o.stop(t + 0.4);
  }

  /* ---------- Pixel ---------- */
  private pixel(sc: Scena) {
    const tempo = 140;
    const s16 = 60 / tempo / 4;
    // melodie veselă de joc de platformă (compusă original): [pas, notă, durată în pași]
    const melodie: [number, string, number][] = [
      [0, "E5", 1], [2, "G5", 1], [4, "C6", 2], [7, "G5", 1], [8, "E5", 1], [10, "G5", 1], [12, "A5", 2], [14, "G5", 1],
      [16, "E5", 1], [18, "C5", 1], [20, "E5", 1], [22, "A5", 2], [25, "G5", 1], [26, "E5", 1], [28, "C5", 2],
      [32, "F5", 1], [34, "A5", 1], [36, "C6", 2], [39, "A5", 1], [40, "F5", 1], [42, "A5", 1], [44, "C6", 2], [46, "D6", 1],
      [48, "B5", 2], [51, "G5", 1], [52, "D5", 1], [54, "G5", 1], [56, "B5", 1], [58, "D6", 2], [60, "C6", 2],
    ];
    const bas = ["C3", "G3", "A2", "E3", "F2", "C3", "G2", "D3"];
    this.bucla(sc, 64 * s16, (t0) => {
      melodie.forEach(([p, n, d]) => {
        this.ton(sc, t0 + p * s16, frecventa(n), d * s16 * 0.95, "square", 0.045);
        this.ton(sc, t0 + p * s16 + s16 * 0.5, frecventa(n), d * s16 * 0.6, "square", 0.012); // ecou
      });
      for (let i = 0; i < 32; i++) {
        const n = bas[Math.floor(i / 4)];
        this.ton(sc, t0 + i * 2 * s16, frecventa(n) * (i % 2 ? 2 : 1), s16 * 1.6, "triangle", 0.11);
      }
      for (let i = 0; i < 64; i += 2) {
        const t = t0 + i * s16;
        if (i % 8 === 0) this.ton(sc, t, 160, 0.12, "sine", 0.25, { glisare: 45 });
        if (i % 8 === 4) this.zgomotScurt(sc, t, 0.1, 0.12, "bandpass", 1800);
        this.zgomotScurt(sc, t, 0.03, 0.04, "highpass", 7000);
      }
    });
    // „moneda” / bonus — un sunet scurt de joc, din când în când
    this.aleator(sc, 6, 11, (t) => {
      this.ton(sc, t, frecventa("B5"), 0.08, "square", 0.04);
      this.ton(sc, t + 0.08, frecventa("E6"), 0.3, "square", 0.04);
    });
  }
}

let motor: MotorSunet | null = null;
const CHEIE_SUNET = "bac_sunet";

/* Sunetul de fundal: pornit/oprit, se schimbă automat cu tema */
export function useSunetFundal(tema: TemaId) {
  const [pornit, setPornit] = useState(false);

  useEffect(() => {
    let salvat = false;
    try { salvat = localStorage.getItem(CHEIE_SUNET) === "da"; } catch {}
    if (!salvat) return;
    // browserul cere un click înainte de sunet
    const laPrimulClick = () => setPornit(true);
    window.addEventListener("pointerdown", laPrimulClick, { once: true });
    return () => window.removeEventListener("pointerdown", laPrimulClick);
  }, []);

  useEffect(() => {
    if (!motor) motor = new MotorSunet();
    if (pornit) motor.porneste(tema);
    else motor.opreste();
  }, [pornit, tema]);

  useEffect(() => () => motor?.opreste(), []);

  const comuta = useCallback(() => {
    setPornit((p) => {
      try { localStorage.setItem(CHEIE_SUNET, p ? "nu" : "da"); } catch {}
      return !p;
    });
  }, []);

  return { pornit, comuta };
}

export function ButonSunet({ tema }: { tema: TemaId }) {
  const { pornit, comuta } = useSunetFundal(tema);
  return (
    <button type="button" className="bq-sunet" onClick={comuta} aria-pressed={pornit}
      aria-label={pornit ? "Oprește sunetul" : "Pornește sunetul"}>
      <span className="bq-sunet-icon">{pornit ? "🔊" : "🔇"}</span>
      <span>{pornit ? "Sunet pornit" : "Pornește sunetul"}</span>
    </button>
  );
}

/* ==================================================================
   AVATARELE — personaje din operele de bac, desenate în SVG.
   Folosire oriunde (profil, multiplayer, clasament):
     import { Avatar, useAvatar } from "@/components/Avatare";
     const { avatar } = useAvatar();          // id-ul ales de utilizator
     <Avatar id={avatar} size={64} />
   ================================================================== */


export type AvatarId =
  | "ion" | "ghita" | "harap-alb" | "calul" | "spanul" | "luceafarul"
  | "lica" | "setila" | "flamanzila" | "gerila" | "ochila" | "lungila";

export const AVATARE: { id: AvatarId; nume: string; opera: string }[] = [
  { id: "ion", nume: "Ion", opera: "Ion" },
  { id: "ghita", nume: "Ghiță", opera: "Moara cu noroc" },
  { id: "lica", nume: "Lică Sămădăul", opera: "Moara cu noroc" },
  { id: "harap-alb", nume: "Harap-Alb", opera: "Povestea lui Harap-Alb" },
  { id: "calul", nume: "Calul năzdrăvan", opera: "Povestea lui Harap-Alb" },
  { id: "spanul", nume: "Spânul", opera: "Povestea lui Harap-Alb" },
  { id: "setila", nume: "Setilă", opera: "Povestea lui Harap-Alb" },
  { id: "flamanzila", nume: "Flămânzilă", opera: "Povestea lui Harap-Alb" },
  { id: "gerila", nume: "Gerilă", opera: "Povestea lui Harap-Alb" },
  { id: "ochila", nume: "Ochilă", opera: "Povestea lui Harap-Alb" },
  { id: "lungila", nume: "Păsări-Lăți-Lungilă", opera: "Povestea lui Harap-Alb" },
  { id: "luceafarul", nume: "Luceafărul", opera: "Luceafărul" },
];

const CHEIE_AVATAR = "bac_avatar";

export function useAvatar() {
  const [avatar, setAvatarState] = useState<AvatarId>("ion");
  useEffect(() => {
    try {
      const a = localStorage.getItem(CHEIE_AVATAR);
      if (AVATARE.some((x) => x.id === a)) setAvatarState(a as AvatarId);
    } catch {}
  }, []);
  const setAvatar = useCallback((a: AvatarId) => {
    setAvatarState(a);
    try {
      localStorage.setItem(CHEIE_AVATAR, a);
    } catch {}
  }, []);
  return { avatar, setAvatar };
}

/* ---------------- culori și piese comune ---------------- */
const C = "#3d2b22"; // contur, mai moale (ca pe hartă)
const PIELE = "#f2c49b";
const PIELE_UMBRA = "#e3a87c";
const ROSU = "#c8323a";
const linie = { stroke: C, strokeWidth: 3, strokeLinejoin: "round" as const, strokeLinecap: "round" as const };

function Cap({ rx = 42, ry = 46, cy = 94, piele = PIELE }: { rx?: number; ry?: number; cy?: number; piele?: string }) {
  return (
    <g>
      <rect x="88" y={cy + ry - 14} width="24" height="22" fill={PIELE_UMBRA} {...linie} />
      <ellipse cx="100" cy={cy} rx={rx} ry={ry} fill={piele} {...linie} />
      <ellipse cx="100" cy={cy} rx={rx - 1.5} ry={ry - 1.5} fill="url(#av-umbra-fata)" />
    </g>
  );
}

function Urechi({ cy = 98, dx = 42, piele = PIELE, r = 9 }: { cy?: number; dx?: number; piele?: string; r?: number }) {
  return (
    <g>
      <ellipse cx={100 - dx} cy={cy} rx={r * 0.8} ry={r} fill={piele} {...linie} />
      <ellipse cx={100 + dx} cy={cy} rx={r * 0.8} ry={r} fill={piele} {...linie} />
    </g>
  );
}

function Ochi({ y = 98, dx = 16, tip = "normal", privire = 0 }: {
  y?: number; dx?: number; tip?: "normal" | "viclean" | "inchisi" | "veseli" | "mari"; privire?: number;
}) {
  if (tip === "inchisi" || tip === "veseli") {
    const d = tip === "inchisi" ? 4 : -6;
    return (
      <g fill="none" stroke={C} strokeWidth="4.5" strokeLinecap="round">
        <path d={`M${100 - dx - 7} ${y} Q${100 - dx} ${y + d} ${100 - dx + 7} ${y}`} />
        <path d={`M${100 + dx - 7} ${y} Q${100 + dx} ${y + d} ${100 + dx + 7} ${y}`} />
      </g>
    );
  }
  const r = tip === "mari" ? 8 : 5.5;
  return (
    <g>
      {[-1, 1].map((s) => (
        <g key={s}>
          {tip === "mari" && <circle cx={100 + s * dx} cy={y} r={r + 4} fill="#fff" {...linie} strokeWidth={3} />}
          <circle cx={100 + s * dx + privire} cy={y} r={r} fill={C} />
          <circle cx={100 + s * dx + privire + 1.8} cy={y - 2} r={r * 0.35} fill="#fff" />
          {tip === "viclean" && (
            <path d={`M${100 + s * dx - 9} ${y - 3} L${100 + s * dx + 9} ${y - 5} L${100 + s * dx + 9} ${y - 12} L${100 + s * dx - 9} ${y - 12} Z`} fill={PIELE} />
          )}
          {tip === "viclean" && <path d={`M${100 + s * dx - 9} ${y - 3} L${100 + s * dx + 9} ${y - 5}`} stroke={C} strokeWidth="3.5" strokeLinecap="round" />}
        </g>
      ))}
    </g>
  );
}

function Sprancene({ y = 84, dx = 16, unghi = 0, culoare = C, grosime = 4.5 }: { y?: number; dx?: number; unghi?: number; culoare?: string; grosime?: number }) {
  return (
    <g stroke={culoare} strokeWidth={grosime} strokeLinecap="round" fill="none">
      <path d={`M${100 - dx - 9} ${y + unghi} Q${100 - dx} ${y - 4} ${100 - dx + 8} ${y - unghi}`} />
      <path d={`M${100 + dx - 8} ${y - unghi} Q${100 + dx} ${y - 4} ${100 + dx + 9} ${y + unghi}`} />
    </g>
  );
}

function Obraji({ y = 114, dx = 26 }: { y?: number; dx?: number }) {
  return (
    <g fill="#ff8f8f" opacity="0.45">
      <ellipse cx={100 - dx} cy={y} rx="8" ry="5" />
      <ellipse cx={100 + dx} cy={y} rx="8" ry="5" />
    </g>
  );
}

function Nas({ y = 108 }: { y?: number }) {
  return <path d={`M98 ${y - 8} Q94 ${y + 2} 101 ${y + 3}`} fill="none" stroke={C} strokeWidth="3.5" strokeLinecap="round" />;
}

function Gura({ y = 122, tip = "zambet" }: { y?: number; tip?: "zambet" | "smirk" | "serios" | "o" | "larga" }) {
  if (tip === "o") return <ellipse cx="100" cy={y + 2} rx="9" ry="11" fill="#5a1e22" {...linie} strokeWidth={3.5} />;
  if (tip === "larga")
    return (
      <g>
        <path d={`M78 ${y - 4} Q100 ${y + 26} 122 ${y - 4} Z`} fill="#5a1e22" {...linie} strokeWidth={3.5} />
        <path d={`M83 ${y - 2} L117 ${y - 2} L115 ${y + 3} L85 ${y + 3} Z`} fill="#fff" />
        <ellipse cx="100" cy={y + 12} rx="9" ry="4" fill="#e86a7a" />
      </g>
    );
  const d =
    tip === "zambet" ? `M88 ${y} Q100 ${y + 10} 112 ${y}` :
    tip === "smirk" ? `M88 ${y + 2} Q104 ${y + 4} 114 ${y - 5}` :
    `M90 ${y + 2} L110 ${y + 2}`;
  return <path d={d} fill="none" stroke={C} strokeWidth="4" strokeLinecap="round" />;
}

/* Cămașa țărănească, cu broderie roșie la gât, și pieptarul */
function Camasa({ pieptar, blana = false }: { pieptar?: string; blana?: boolean }) {
  return (
    <g>
      <path d="M22 210 C26 170 48 150 78 144 L122 144 C152 150 174 170 178 210 Z" fill="#f7f3ea" {...linie} />
      <path d="M86 144 L100 166 L114 144" fill="none" stroke={C} strokeWidth="3" />
      {[0, 1, 2, 3].map((i) => (
        <g key={i}>
          <path d={`M${88 + i * 3.2} ${148 + i * 4.6} l3 3 l-3 3 l-3 -3 Z`} fill={ROSU} />
          <path d={`M${112 - i * 3.2} ${148 + i * 4.6} l3 3 l-3 3 l-3 -3 Z`} fill={ROSU} />
        </g>
      ))}
      {pieptar && (
        <g>
          <path d="M22 210 C26 172 46 152 70 146 L84 200 L84 210 Z" fill={pieptar} {...linie} />
          <path d="M178 210 C174 172 154 152 130 146 L116 200 L116 210 Z" fill={pieptar} {...linie} />
          {blana && <path d="M70 146 L84 200 M130 146 L116 200" stroke="#f3ead6" strokeWidth="7" strokeLinecap="round" />}
          {blana && <path d="M70 146 L84 200 M130 146 L116 200" stroke={C} strokeWidth="1.5" strokeDasharray="2 5" />}
          <path d="M40 178 l6 -4 l6 4 l-6 4 Z M148 178 l6 -4 l6 4 l-6 4 Z" fill={ROSU} />
        </g>
      )}
    </g>
  );
}

function Mana({ x, y, r = 11 }: { x: number; y: number; r?: number }) {
  return <ellipse cx={x} cy={y} rx={r} ry={r * 0.9} fill={PIELE} {...linie} />;
}

/* ---------------- personajele ---------------- */

function Ion() {
  return (
    <g>
      <path d="M0 150 C40 132 80 140 110 150 C150 160 180 142 200 136 V200 H0 Z" fill="#8fbf6a" />
      <path d="M0 170 C50 156 120 172 200 160 V200 H0 Z" fill="#c9a24a" />
      <Camasa pieptar="#8a5a34" blana />
      <Urechi />
      <Cap />
      {/* păr negru la tâmple */}
      <path d="M58 82 C60 70 66 66 72 70 L70 100 C64 98 60 92 58 82 Z M142 82 C140 70 134 66 128 70 L130 100 C136 98 140 92 142 82 Z" fill="#1f1a18" {...linie} strokeWidth={3} />
      {/* căciula */}
      <path d="M54 76 C50 38 66 20 100 18 C134 20 150 38 146 76 C120 70 80 70 54 76 Z" fill="#2b2626" {...linie} />
      <path d="M70 40 q4 -4 8 0 M90 30 q4 -4 8 0 M112 34 q4 -4 8 0 M126 50 q4 -4 8 0 M78 56 q4 -4 8 0 M104 50 q4 -4 8 0" stroke="#5a5252" strokeWidth="3" fill="none" strokeLinecap="round" />
      <Sprancene unghi={2} />
      <Ochi />
      <Nas />
      <Obraji />
      <Gura tip="smirk" />
      {/* furca */}
      <g>
        <line x1="160" y1="210" x2="178" y2="34" stroke={C} strokeWidth="11" strokeLinecap="round" />
        <line x1="160" y1="210" x2="178" y2="34" stroke="#c08a50" strokeWidth="5" strokeLinecap="round" />
        <path d="M160 40 Q178 50 196 34" fill="none" stroke={C} strokeWidth="9" strokeLinecap="round" />
        <path d="M160 40 Q178 50 196 34" fill="none" stroke="#c08a50" strokeWidth="4" strokeLinecap="round" />
        {[160, 178, 196].map((x, i) => (
          <g key={x}>
            <path d={`M${x} ${i === 1 ? 46 : 40} L${x + (i - 1) * 2} 4`} stroke={C} strokeWidth="8" strokeLinecap="round" />
            <path d={`M${x} ${i === 1 ? 46 : 40} L${x + (i - 1) * 2} 4`} stroke="#c08a50" strokeWidth="3.5" strokeLinecap="round" />
          </g>
        ))}
        <Mana x={166} y={154} r={12} />
      </g>
    </g>
  );
}

function Ghita() {
  return (
    <g>
      <rect x="0" y="120" width="200" height="80" fill="#e2b56a" />
      <path d="M0 120 H200" stroke="#b8893f" strokeWidth="4" />
      <Camasa pieptar="#2f2a2a" />
      <path d="M86 146 L100 160 L114 146 L106 142 L100 150 L94 142 Z" fill={ROSU} {...linie} strokeWidth={2.5} />
      <Urechi />
      <Cap />
      <path d="M60 84 C62 72 68 68 74 72 L72 96 C66 94 62 90 60 84 Z M140 84 C138 72 132 68 126 72 L128 96 C134 94 138 90 140 84 Z" fill="#4a3020" {...linie} strokeWidth={3} />
      {/* pălăria */}
      <ellipse cx="100" cy="66" rx="64" ry="12" fill="#2b2626" {...linie} />
      <path d="M66 64 C64 36 76 26 100 26 C124 26 136 36 134 64 Z" fill="#2b2626" {...linie} />
      <path d="M67 56 C90 60 110 60 133 56 L133 64 C110 68 90 68 67 64 Z" fill={ROSU} />
      <Sprancene unghi={-3} />
      <Ochi />
      <Nas />
      {/* mustață */}
      <path d="M100 113 C92 108 80 110 74 120 C84 118 92 120 100 117 C108 120 116 118 126 120 C120 110 108 108 100 113 Z" fill="#4a3020" {...linie} strokeWidth={3} />
      <Gura y={126} tip="serios" />
      {/* punga cu bani */}
      <g>
        <path d="M34 156 C22 176 28 200 56 200 C84 200 90 176 78 156 C74 150 70 148 66 146 L48 146 C42 148 38 150 34 156 Z" fill="#c9a26b" {...linie} />
        <path d="M46 148 C52 152 62 152 68 148" fill="none" stroke="#7a5a2a" strokeWidth="4" />
        <circle cx="56" cy="178" r="11" fill="#f2c94c" {...linie} strokeWidth={3} />
        <text x="56" y="183" textAnchor="middle" fontSize="13" fontWeight="900" fill="#7a5a00" fontFamily="sans-serif">L</text>
        <Mana x={57} y={142} r={11} />
        <circle cx="92" cy="196" r="7" fill="#f2c94c" {...linie} strokeWidth={2.5} />
        <circle cx="24" cy="194" r="6" fill="#f2c94c" {...linie} strokeWidth={2.5} />
      </g>
    </g>
  );
}

function Lica() {
  return (
    <g>
      <path d="M0 140 C60 124 140 128 200 118 V200 H0 Z" fill="#6f7f52" />
      <Camasa pieptar="#1f1c1c" />
      {/* chimirul */}
      <path d="M30 192 H170 L172 210 H28 Z" fill="#7a4a24" {...linie} />
      {[44, 64, 84, 116, 136, 156].map((x) => <circle key={x} cx={x} cy="200" r="3.2" fill="#d9c08a" stroke={C} strokeWidth="1.5" />)}
      <rect x="92" y="193" width="16" height="14" rx="2" fill="#d9c08a" {...linie} strokeWidth={2.5} />
      <Urechi />
      <Cap />
      <path d="M58 86 C58 72 66 66 74 70 L72 98 C64 96 60 92 58 86 Z M142 86 C142 72 134 66 126 70 L128 98 C136 96 140 92 142 86 Z" fill="#141010" {...linie} strokeWidth={3} />
      {/* pălăria, trasă pe ochi */}
      <g transform="rotate(-9 100 64)">
        <ellipse cx="100" cy="68" rx="66" ry="12" fill="#1a1616" {...linie} />
        <path d="M66 66 C64 38 76 28 100 28 C124 28 136 38 134 66 Z" fill="#1a1616" {...linie} />
        <path d="M128 40 C150 22 160 30 156 44" fill="none" stroke="#3d6b3a" strokeWidth="5" strokeLinecap="round" />
      </g>
      <Sprancene y={88} unghi={-6} grosime={5.5} />
      <Ochi tip="viclean" y={100} />
      <Nas />
      {/* cicatrice */}
      <path d="M124 104 L134 118 M126 110 l5 -2 M129 114 l5 -2" stroke="#a5534a" strokeWidth="2.5" strokeLinecap="round" />
      {/* mustață groasă, răsucită */}
      <path d="M100 113 C90 106 74 108 66 118 C64 122 68 126 72 122 C80 118 90 120 100 118 C110 120 120 118 128 122 C132 126 136 122 134 118 C126 108 110 106 100 113 Z" fill="#141010" {...linie} strokeWidth={3} />
      <Gura y={127} tip="smirk" />
      {/* biciul încolăcit */}
      <g>
        <path d="M150 204 L168 150" stroke={C} strokeWidth="11" strokeLinecap="round" />
        <path d="M150 204 L168 150" stroke="#6b3d1e" strokeWidth="5" strokeLinecap="round" />
        <path d="M168 150 C196 140 198 114 176 112 C156 110 150 132 166 138 C184 144 194 124 182 120" fill="none" stroke={C} strokeWidth="6" strokeLinecap="round" />
        <path d="M168 150 C196 140 198 114 176 112 C156 110 150 132 166 138 C184 144 194 124 182 120" fill="none" stroke="#a87444" strokeWidth="2.5" strokeLinecap="round" />
        <Mana x={160} y={176} r={11} />
      </g>
    </g>
  );
}

function HarapAlb() {
  return (
    <g>
      <circle cx="160" cy="40" r="3" fill="#fff" /><circle cx="40" cy="56" r="2.5" fill="#fff" /><circle cx="150" cy="80" r="2" fill="#fff" />
      {/* mantia */}
      <path d="M14 210 C18 168 40 146 74 140 L126 140 C160 146 182 168 186 210 Z" fill="#e8ecf5" {...linie} />
      {/* tunica albă cu fir de aur */}
      <path d="M44 210 C48 176 62 154 82 146 L118 146 C138 154 152 176 156 210 Z" fill="#ffffff" {...linie} />
      <path d="M100 150 V210" stroke="#e0b23c" strokeWidth="6" />
      <path d="M82 146 L100 162 L118 146" fill="none" stroke="#e0b23c" strokeWidth="5" />
      {[172, 186, 200].map((y) => <circle key={y} cx="100" cy={y} r="3.5" fill="#fff6d0" stroke="#b8891c" strokeWidth="1.5" />)}
      <path d="M44 172 Q60 168 72 176 M156 172 Q140 168 128 176" stroke="#e0b23c" strokeWidth="4" fill="none" />
      <circle cx="72" cy="150" r="7" fill="#e0b23c" {...linie} strokeWidth={2.5} />
      <circle cx="128" cy="150" r="7" fill="#e0b23c" {...linie} strokeWidth={2.5} />
      <Urechi />
      <Cap />
      {/* păr castaniu, ondulat */}
      <path d="M56 92 C50 52 74 38 100 38 C130 38 152 54 144 94 C140 80 134 70 128 66 C116 74 96 76 80 70 C72 74 62 80 56 92 Z" fill="#9a6232" {...linie} />
      {/* coroniță de aur */}
      <path d="M70 52 L76 36 L86 48 L100 30 L114 48 L124 36 L130 52 C112 46 88 46 70 52 Z" fill="#f2c94c" {...linie} strokeWidth={3} />
      <circle cx="100" cy="42" r="3.5" fill="#4aa3df" />
      <Sprancene unghi={2} culoare="#6b4220" />
      <Ochi />
      <Nas />
      <Obraji />
      <Gura tip="zambet" />
    </g>
  );
}

function Calul() {
  return (
    <g>
      {/* scântei de jăratic */}
      {[[30, 40], [168, 52], [24, 120], [176, 130], [150, 22]].map(([x, y], i) => (
        <path key={i} d={`M${x} ${y - 7} L${x + 2} ${y - 2} L${x + 7} ${y} L${x + 2} ${y + 2} L${x} ${y + 7} L${x - 2} ${y + 2} L${x - 7} ${y} L${x - 2} ${y - 2} Z`} fill="#ffd166" />
      ))}
      {/* gâtul */}
      <path d="M50 210 C54 170 64 150 80 140 L120 140 C136 150 146 170 150 210 Z" fill="#f4f1fa" {...linie} />
      {/* coama aurie */}
      <path d="M100 30 C130 34 150 60 146 100 C156 120 150 150 140 170 C134 140 128 120 122 100 C120 70 110 50 100 30 Z" fill="#f2b43c" {...linie} />
      <path d="M100 30 C70 34 50 60 54 100 C44 120 50 150 60 170 C66 140 72 120 78 100 C80 70 90 50 100 30 Z" fill="#f2b43c" {...linie} />
      {/* urechile */}
      <path d="M72 58 L66 18 L90 44 Z" fill="#f4f1fa" {...linie} />
      <path d="M128 58 L134 18 L110 44 Z" fill="#f4f1fa" {...linie} />
      <path d="M73 50 L70 28 L84 44 Z M127 50 L130 28 L116 44 Z" fill="#f0b8c4" />
      {/* capul */}
      <path d="M66 70 C66 46 134 46 134 70 L130 130 C128 150 116 164 100 164 C84 164 72 150 70 130 Z" fill="#f4f1fa" {...linie} />
      <path d="M92 52 C94 70 96 96 100 120 C104 96 106 70 108 52 Z" fill="#ffffff" />
      {/* bot */}
      <ellipse cx="100" cy="146" rx="28" ry="20" fill="#e6dff0" {...linie} />
      <ellipse cx="90" cy="148" rx="4" ry="6" fill={C} />
      <ellipse cx="110" cy="148" rx="4" ry="6" fill={C} />
      <path d="M92 160 Q100 165 108 160" fill="none" stroke={C} strokeWidth="3" strokeLinecap="round" />
      {/* moțul de pe frunte */}
      <path d="M100 40 C88 46 86 60 94 66 C94 56 100 52 106 54 C104 48 102 44 100 40 Z" fill="#f2b43c" {...linie} strokeWidth={3} />
      {/* ochi mari, blânzi */}
      <ellipse cx="80" cy="96" rx="8" ry="10" fill={C} />
      <ellipse cx="120" cy="96" rx="8" ry="10" fill={C} />
      <circle cx="83" cy="92" r="3" fill="#fff" /><circle cx="123" cy="92" r="3" fill="#fff" />
      <path d="M70 84 l-4 -4 M74 82 l-2 -5 M130 84 l4 -4 M126 82 l2 -5" stroke={C} strokeWidth="3" strokeLinecap="round" />
      {/* căpăstrul */}
      <path d="M70 118 C84 124 116 124 130 118" fill="none" stroke="#8a4b2a" strokeWidth="6" />
      <path d="M70 118 C84 124 116 124 130 118" fill="none" stroke={C} strokeWidth="1.5" strokeDasharray="1 7" />
      <circle cx="70" cy="118" r="5" fill="#f2c94c" {...linie} strokeWidth={2.5} />
      <circle cx="130" cy="118" r="5" fill="#f2c94c" {...linie} strokeWidth={2.5} />
    </g>
  );
}

function Spanul() {
  return (
    <g>
      {/* mantie roșie închisă */}
      <path d="M16 210 C20 168 42 146 76 140 L124 140 C158 146 180 168 184 210 Z" fill="#7a1f2b" {...linie} />
      <path d="M60 210 C64 176 76 156 88 148 L112 148 C124 156 136 176 140 210 Z" fill="#3a2b2f" {...linie} />
      <path d="M76 140 L100 170 L124 140" fill="none" stroke="#c9a24a" strokeWidth="4" />
      <circle cx="100" cy="172" r="5" fill="#c9a24a" {...linie} strokeWidth={2.5} />
      <Urechi r={10} dx={41} />
      {/* cap chel, lucios, fără barbă */}
      <rect x="88" y="126" width="24" height="22" fill={PIELE_UMBRA} {...linie} />
      <path d="M58 92 C56 52 74 38 100 38 C126 38 144 52 142 92 C142 120 124 140 100 140 C76 140 58 120 58 92 Z" fill={PIELE} {...linie} />
      <path d="M58 92 C56 52 74 38 100 38 C126 38 144 52 142 92 C142 120 124 140 100 140 C76 140 58 120 58 92 Z" fill="url(#av-umbra-fata)" />
      <ellipse cx="84" cy="54" rx="14" ry="7" fill="#fff" opacity="0.6" transform="rotate(-20 84 54)" />
      <path d="M74 76 q4 -3 8 0 M118 76 q4 -3 8 0" stroke={PIELE_UMBRA} strokeWidth="2.5" fill="none" />
      {/* o sprânceană ridicată, privire vicleană */}
      <path d="M74 88 Q84 84 92 88" stroke={C} strokeWidth="4.5" fill="none" strokeLinecap="round" />
      <path d="M108 84 Q118 76 128 82" stroke={C} strokeWidth="4.5" fill="none" strokeLinecap="round" />
      <Ochi tip="viclean" y={100} privire={3} />
      <path d="M100 98 Q104 112 96 116" fill="none" stroke={C} strokeWidth="3.5" strokeLinecap="round" />
      <Gura y={124} tip="smirk" />
      <path d="M112 120 l4 -2" stroke={C} strokeWidth="3" strokeLinecap="round" />
    </g>
  );
}

function Setila() {
  return (
    <g>
      {[[34, 48], [160, 38], [30, 120], [176, 112]].map(([x, y], i) => (
        <path key={i} d={`M${x} ${y - 8} C${x + 6} ${y} ${x + 6} ${y + 6} ${x} ${y + 6} C${x - 6} ${y + 6} ${x - 6} ${y} ${x} ${y - 8} Z`} fill="#7cc8f0" stroke="#2a6a9a" strokeWidth="2.5" />
      ))}
      <Camasa pieptar="#5a7a9a" />
      <Urechi />
      <Cap rx={38} ry={48} />
      {/* păr ciufulit */}
      <path d="M62 70 L58 52 L72 60 L74 40 L86 54 L94 34 L102 52 L114 36 L118 56 L132 44 L130 62 L144 58 L138 76 C120 66 80 66 62 70 Z" fill="#7a5230" {...linie} strokeWidth={3} />
      <Sprancene y={82} unghi={-2} />
      <Ochi tip="mari" y={94} dx={15} />
      <path d="M128 70 C132 78 132 82 128 84 C124 82 124 78 128 70 Z" fill="#7cc8f0" stroke="#2a6a9a" strokeWidth="2" />
      <Nas y={110} />
      {/* gura mare, însetată, cu buze groase */}
      <ellipse cx="100" cy="128" rx="18" ry="14" fill="#5a1e22" stroke="#c66a6a" strokeWidth="7" />
      <ellipse cx="100" cy="128" rx="18" ry="14" fill="none" stroke={C} strokeWidth="2.5" />
      <path d="M92 134 Q100 144 108 134" fill="#e86a7a" />
      {/* ciutura cu apă */}
      <g transform="rotate(-24 160 160)">
        <path d="M132 136 L188 136 L180 196 L140 196 Z" fill="#a8743f" {...linie} />
        <path d="M134 150 H186 M137 178 H183" stroke="#5a5a5a" strokeWidth="5" />
        <ellipse cx="160" cy="136" rx="28" ry="7" fill="#7cc8f0" {...linie} strokeWidth={3} />
      </g>
      <path d="M128 128 C122 132 118 136 116 134" stroke="#7cc8f0" strokeWidth="5" fill="none" strokeLinecap="round" />
      <Mana x={148} y={186} r={11} />
    </g>
  );
}

function Flamanzila() {
  return (
    <g>
      <Camasa pieptar="#b0703a" />
      <Urechi dx={50} cy={102} />
      {/* față rotundă, plinuță */}
      <rect x="88" y="140" width="24" height="10" fill={PIELE_UMBRA} {...linie} />
      <ellipse cx="100" cy="100" rx="52" ry="46" fill={PIELE} {...linie} />
      <ellipse cx="100" cy="100" rx="50" ry="44" fill="url(#av-umbra-fata)" />
      <path d="M70 64 C76 50 92 46 98 56 C102 44 120 46 124 58 C114 56 108 60 104 64 C92 58 80 60 70 64 Z" fill="#5a3a22" {...linie} strokeWidth={3} />
      <Sprancene y={84} dx={20} unghi={3} />
      <Ochi tip="veseli" y={98} dx={20} />
      <Obraji y={116} dx={34} />
      <Nas y={110} />
      <Gura y={124} tip="larga" />
      <path d="M118 132 C120 140 122 144 118 146 C114 144 116 138 118 132 Z" fill="#9fd8f2" stroke="#2a6a9a" strokeWidth="2" />
      {/* colacul de pâine, mușcat */}
      <g>
        <path d="M18 170 C18 146 44 134 66 140 C90 146 96 168 84 186 C72 204 34 206 22 190 Z" fill="#d99a4a" {...linie} />
        <path d="M30 160 Q46 150 64 156 M28 176 Q48 166 76 172" stroke="#a8692a" strokeWidth="4" fill="none" strokeLinecap="round" />
        <path d="M66 140 C72 146 80 144 82 150 C88 152 92 158 90 164 Z" fill="#f6dfb5" {...linie} strokeWidth={3} />
        <Mana x={44} y={196} r={11} />
        {[[96, 176], [102, 186], [92, 194]].map(([x, y], i) => <circle key={i} cx={x} cy={y} r="2.5" fill="#d99a4a" />)}
      </g>
    </g>
  );
}

function Gerila() {
  const ger = "#d6ecfa";
  return (
    <g>
      {[[28, 40], [170, 30], [22, 112], [178, 98], [160, 160]].map(([x, y], i) => (
        <g key={i} stroke="#fff" strokeWidth="2.5" strokeLinecap="round">
          <path d={`M${x - 7} ${y} H${x + 7} M${x} ${y - 7} V${y + 7} M${x - 5} ${y - 5} L${x + 5} ${y + 5} M${x - 5} ${y + 5} L${x + 5} ${y - 5}`} />
        </g>
      ))}
      <Camasa pieptar="#5e7ea0" blana />
      {/* urechi uriașe */}
      <ellipse cx="44" cy="96" rx="22" ry="30" fill={ger} {...linie} />
      <ellipse cx="156" cy="96" rx="22" ry="30" fill={ger} {...linie} />
      <ellipse cx="46" cy="96" rx="10" ry="16" fill="#b6d8ee" />
      <ellipse cx="154" cy="96" rx="10" ry="16" fill="#b6d8ee" />
      <Cap piele={ger} rx={40} />
      {/* păr de chiciură */}
      <path d="M62 74 C64 50 84 42 100 42 C118 42 136 50 138 74 C128 64 116 60 100 62 C84 60 72 64 62 74 Z" fill="#ffffff" {...linie} strokeWidth={3} />
      <Sprancene y={84} unghi={1} culoare="#ffffff" grosime={7} />
      <Sprancene y={84} unghi={1} culoare="#8fb8d6" grosime={2} />
      <Ochi y={98} />
      <path d="M98 100 Q93 112 101 113" fill="none" stroke={C} strokeWidth="3.5" strokeLinecap="round" />
      {/* buze uriașe */}
      <path d="M72 124 C80 112 92 116 100 120 C108 116 120 112 128 124 C120 138 80 138 72 124 Z" fill="#7a8fd6" {...linie} />
      <path d="M76 124 C88 128 112 128 124 124" fill="none" stroke={C} strokeWidth="3" />
      {/* țurțuri */}
      {[82, 94, 108, 118].map((x, i) => <path key={x} d={`M${x - 3} 134 L${x} ${144 + (i % 2) * 6} L${x + 3} 134 Z`} fill="#eaf6ff" stroke="#8fb8d6" strokeWidth="1.5" />)}
      {/* suflarea de ger */}
      <path d="M128 124 C146 118 156 124 168 118 C178 114 186 120 196 116" fill="none" stroke="#fff" strokeWidth="5" strokeLinecap="round" opacity="0.9" />
    </g>
  );
}

function Ochila() {
  return (
    <g>
      <Camasa pieptar="#4f7a3a" />
      <Urechi />
      <Cap />
      <path d="M60 84 C58 54 78 42 100 44 C124 42 144 56 140 84 C134 70 122 62 114 64 L112 54 L104 62 L98 50 L92 62 L84 54 L84 66 C74 66 64 74 60 84 Z" fill="#3a2a1e" {...linie} strokeWidth={3} />
      {/* un singur ochi, mare, în frunte */}
      <path d="M72 76 Q100 58 128 76" fill="none" stroke={C} strokeWidth="6" strokeLinecap="round" />
      <ellipse cx="100" cy="94" rx="24" ry="17" fill="#fff" {...linie} />
      <circle cx="100" cy="94" r="12" fill="#3aa36b" stroke={C} strokeWidth="2.5" />
      <circle cx="100" cy="94" r="6" fill={C} />
      <circle cx="104" cy="90" r="3" fill="#fff" />
      {[-18, -9, 0, 9, 18].map((dx) => <path key={dx} d={`M${100 + dx} ${78 + Math.abs(dx) * 0.18} l${dx * 0.15} -6`} stroke={C} strokeWidth="2.5" strokeLinecap="round" />)}
      <Nas y={114} />
      <Obraji y={118} />
      <Gura y={128} tip="zambet" />
    </g>
  );
}

function Lungila() {
  return (
    <g>
      <Camasa pieptar="#8a3a6a" />
      {/* gât foarte lung, care se întinde */}
      <path d="M90 150 C88 120 92 90 92 72 L108 72 C108 90 112 120 110 150 Z" fill={PIELE_UMBRA} {...linie} />
      {[132, 116, 100, 86].map((y) => <path key={y} d={`M92 ${y} Q100 ${y + 4} 108 ${y}`} fill="none" stroke="#c98a5e" strokeWidth="2.5" />)}
      {/* cap mic, sus de tot */}
      <ellipse cx="100" cy="50" rx="27" ry="31" fill={PIELE} {...linie} />
      <ellipse cx="73" cy="54" rx="5" ry="7" fill={PIELE} {...linie} strokeWidth={3} />
      <ellipse cx="127" cy="54" rx="5" ry="7" fill={PIELE} {...linie} strokeWidth={3} />
      {/* pălărie de vânător, cu pană */}
      <path d="M68 32 C70 12 86 6 100 6 C114 6 130 12 132 32 Z" fill="#4a6a3a" {...linie} />
      <path d="M62 32 H138" stroke={C} strokeWidth="8" strokeLinecap="round" />
      <path d="M62 32 H138" stroke="#4a6a3a" strokeWidth="3" strokeLinecap="round" />
      <path d="M126 24 C150 2 170 6 172 14 C160 14 146 18 130 28 Z" fill="#e05a5a" {...linie} strokeWidth={3} />
      <Ochi y={52} dx={11} />
      <path d="M98 54 Q95 62 101 63" fill="none" stroke={C} strokeWidth="3" strokeLinecap="round" />
      <path d="M90 70 Q100 77 110 70" fill="none" stroke={C} strokeWidth="3.5" strokeLinecap="round" />
      <Obraji y={64} dx={17} />
      {/* săgeți de mișcare: se întinde */}
      <path d="M150 120 V70 M144 78 L150 68 L156 78 M50 120 V70 M44 78 L50 68 L56 78" stroke="#fff" strokeWidth="5" strokeLinecap="round" fill="none" opacity="0.85" />
      {/* o păsărică pe mână */}
      <Mana x={160} y={168} r={11} />
      <g transform="translate(160 150)">
        <ellipse cx="0" cy="0" rx="11" ry="8" fill="#5ab0e0" {...linie} strokeWidth={2.5} />
        <circle cx="8" cy="-6" r="6" fill="#5ab0e0" {...linie} strokeWidth={2.5} />
        <path d="M13 -6 l6 1 l-6 2 Z" fill="#f2b43c" />
        <circle cx="9" cy="-7" r="1.6" fill={C} />
        <path d="M-10 -2 L-18 -8 L-14 2 Z" fill="#3a8ac0" {...linie} strokeWidth={2} />
      </g>
    </g>
  );
}

function Luceafarul({ uid }: { uid: string }) {
  const puncte = Array.from({ length: 10 }, (_, i) => {
    const r = i % 2 === 0 ? 74 : 32;
    const a = -Math.PI / 2 + (i * Math.PI) / 5;
    return `${(100 + r * Math.cos(a)).toFixed(1)},${(106 + r * Math.sin(a)).toFixed(1)}`;
  }).join(" ");
  return (
    <g>
      <defs>
        <radialGradient id={`${uid}-aura`}>
          <stop offset="0" stopColor="#fff6c0" stopOpacity="0.9" />
          <stop offset="1" stopColor="#fff6c0" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`${uid}-steaua`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffe680" />
          <stop offset="1" stopColor="#ffb020" />
        </linearGradient>
      </defs>
      {[[24, 30], [176, 26], [30, 170], [172, 168], [150, 58], [46, 78]].map(([x, y], i) => (
        <circle key={i} cx={x} cy={y} r={i % 2 ? 2 : 3} fill="#fff" />
      ))}
      <path d="M170 34 A14 14 0 1 0 184 50 A11 11 0 1 1 170 34 Z" fill="#f4f1d0" />
      <circle cx="100" cy="106" r="96" fill={`url(#${uid}-aura)`} />
      <polygon points={puncte} fill={`url(#${uid}-steaua)`} {...linie} />
      <Ochi tip="inchisi" y={104} dx={14} />
      <path d="M90 120 Q100 128 110 120" fill="none" stroke={C} strokeWidth="3.5" strokeLinecap="round" />
      <ellipse cx="78" cy="116" rx="7" ry="4.5" fill="#ff8f8f" opacity="0.5" />
      <ellipse cx="122" cy="116" rx="7" ry="4.5" fill="#ff8f8f" opacity="0.5" />
      <path d="M100 18 l3 8 l8 3 l-8 3 l-3 8 l-3 -8 l-8 -3 l8 -3 Z" fill="#fff" opacity="0.9" />
    </g>
  );
}

const FUNDALURI: Record<AvatarId, string> = {
  ion: "#cfe8b8", ghita: "#f6dfa8", lica: "#a9b98a", "harap-alb": "#a9c8f0", calul: "#e9d6f5",
  spanul: "#c8b7d8", setila: "#bfe6fa", flamanzila: "#ffd2a8", gerila: "#a9d6f5", ochila: "#c8eab8",
  lungila: "#f5d0e6", luceafarul: "#1d2550",
};

/* ---------------- componenta folosită peste tot ---------------- */
export function Avatar({ id = "ion", size = 96, className = "" }: { id?: AvatarId; size?: number; className?: string }) {
  const uid = useId().replace(/:/g, "");
  return (
    <svg viewBox="0 0 200 200" width={size} height={size} className={`bq-avatar-svg ${className}`} role="img"
      aria-label={`Avatar: ${AVATARE.find((a) => a.id === id)?.nume ?? id}`}>
      <defs>
        <clipPath id={`${uid}-cerc`}>
          <circle cx="100" cy="100" r="100" />
        </clipPath>
      </defs>
      <g clipPath={`url(#${uid}-cerc)`}>
        <rect width="200" height="200" fill={FUNDALURI[id]} />
        {id === "ion" && <Ion />}
        {id === "ghita" && <Ghita />}
        {id === "lica" && <Lica />}
        {id === "harap-alb" && <HarapAlb />}
        {id === "calul" && <Calul />}
        {id === "spanul" && <Spanul />}
        {id === "setila" && <Setila />}
        {id === "flamanzila" && <Flamanzila />}
        {id === "gerila" && <Gerila />}
        {id === "ochila" && <Ochila />}
        {id === "lungila" && <Lungila />}
        {id === "luceafarul" && <Luceafarul uid={uid} />}
        {/* lumină moale din stânga-sus și umbră în dreapta-jos, ca pe hartă */}
        <rect width="200" height="200" fill="url(#av-lumina)" />
        <rect width="200" height="200" fill="url(#av-umbra)" />
      </g>
    </svg>
  );
}

/* ---------------- grila de alegere, pentru pagina de profil ---------------- */
export function AlegeAvatar({ ales, onAlege }: { ales: AvatarId; onAlege: (a: AvatarId) => void }) {
  return (
    <div className="bq-avatare">
      {AVATARE.map((a) => (
        <button key={a.id} type="button" className={`bq-avatar-opt ${ales === a.id ? "activ" : ""}`}
          onClick={() => onAlege(a.id)} aria-pressed={ales === a.id}>
          <Avatar id={a.id} size={84} />
          <strong>{a.nume}</strong>
          <small>{a.opera}</small>
        </button>
      ))}
    </div>
  );
}

/* ==================================================================
   HARTA OPERELOR — insule văzute de sus, poduri, nivele.
   Se schimbă singură după temă (Acvatic, Realist, Pixel).
   Folosire:  <HartaOperelor opere={...} onNivel={(opera, nivel) => ...} />
   Fără opere date, arată un exemplu cu Harap-Alb, Moara cu noroc, Luceafărul.
   ================================================================== */

export type StareNivel = "terminat" | "curent" | "blocat";
export type OperaHarta = {
  id: string;
  titlu: string;
  autor: string;
  forma: "castel" | "moara" | "turn";
  x: number; y: number; scara?: number;       // poziția insulei pe hartă (hartă 1000×600)
  blocata?: boolean;
  noapte?: boolean;                           // insula e în „zona de noapte”
  nivele: { id: string; stare: StareNivel; x: number; y: number }[];  // poziții pe hartă
};

export const OPERE_EXEMPLU: OperaHarta[] = [
  { id: "harap-alb", titlu: "Harap-Alb", autor: "Ion Creangă", forma: "castel", x: 18, y: 26, scara: 1.18,
    nivele: [{ id: "ha-1", stare: "terminat", x: 150, y: 210 }, { id: "ha-2", stare: "curent", x: 250, y: 196 }] },
  { id: "moara", titlu: "Moara cu noroc", autor: "Ioan Slavici", forma: "moara", x: 330, y: 300, scara: 1.08, blocata: true,
    nivele: [{ id: "mn-1", stare: "blocat", x: 500, y: 520 }] },
  { id: "luceafarul", titlu: "Luceafărul", autor: "Mihai Eminescu", forma: "turn", x: 690, y: 50, scara: 1.0, blocata: true, noapte: true,
    nivele: [{ id: "lu-1", stare: "blocat", x: 832, y: 262 }] },
];

const PODURI_EXEMPLU: [number, number, number, number][] = [
  [318, 232, 392, 352],
  [612, 368, 738, 252],
];

const FORMA_INSULA = {
  castel: "M24 112 C18 62 70 24 146 22 C222 18 288 58 284 114 C288 168 228 206 148 202 C72 206 30 164 24 112 Z",
  moara: "M30 104 C26 56 86 26 156 30 C226 28 280 66 276 112 C280 166 222 200 150 198 C80 200 34 156 30 104 Z",
  turn: "M34 110 C30 64 84 30 150 30 C218 30 272 66 268 112 C270 160 220 196 152 194 C86 196 38 158 34 110 Z",
};

function micsorat(scara: number) {
  return `translate(155 112) scale(${scara}) translate(-155 -112)`;
}

function Brad({ x, y, s = 1 }: { x: number; y: number; s?: number }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse className="hm-umbra" cx="3" cy="2" rx="10" ry="4" />
      <path className="hm-copac" d="M0 -26 L11 -6 L6 -7 L13 2 L-13 2 L-6 -7 L-11 -6 Z" />
      <path className="hm-copac-lumina" d="M0 -26 L-11 -6 L-6 -7 L-13 2 L0 2 Z" />
      <rect className="hm-trunchi" x="-1.5" y="1" width="3" height="4" />
    </g>
  );
}

function Pom({ x, y, r = 11 }: { x: number; y: number; r?: number }) {
  return (
    <g>
      <ellipse className="hm-umbra" cx={x + 3} cy={y + r * 0.7} rx={r} ry={r * 0.4} />
      <circle className="hm-pom" cx={x} cy={y} r={r} />
      <circle className="hm-pom-lumina" cx={x - r * 0.3} cy={y - r * 0.3} r={r * 0.5} />
    </g>
  );
}

function Castel() {
  return (
    <g transform="translate(176 52)">
      <ellipse className="hm-umbra" cx="40" cy="62" rx="52" ry="9" />
      <rect className="hm-zid" x="10" y="22" width="60" height="38" />
      <rect className="hm-zid-umbra" x="48" y="22" width="22" height="38" />
      {[10, 20, 30, 40, 50, 60].map((x) => <rect key={x} className="hm-zid" x={x} y="17" width="6" height="6" />)}
      <path className="hm-poarta" d="M32 60 V46 a8 8 0 0 1 16 0 V60 Z" />
      {[0, 64].map((x) => (
        <g key={x}>
          <rect className="hm-zid" x={x} y="10" width="16" height="50" />
          <rect className="hm-zid-umbra" x={x + 9} y="10" width="7" height="50" />
          <path className="hm-acoperis" d={`M${x - 2} 11 L${x + 8} -12 L${x + 18} 11 Z`} />
          <path className="hm-acoperis-umbra" d={`M${x + 8} -12 L${x + 18} 11 L${x + 8} 11 Z`} />
          <rect className="hm-fereastra" x={x + 5} y="24" width="5" height="8" rx="2" />
          <path className="hm-steag" d={`M${x + 8} -12 V-22 L${x + 16} -19 L${x + 8} -16`} />
        </g>
      ))}
      <rect className="hm-fereastra" x="22" y="30" width="5" height="7" rx="2" />
      <rect className="hm-fereastra" x="54" y="30" width="5" height="7" rx="2" />
    </g>
  );
}

function Fantana({ x, y }: { x: number; y: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse className="hm-umbra" cx="2" cy="10" rx="14" ry="4" />
      <rect className="hm-piatra" x="-10" y="-2" width="20" height="12" rx="3" />
      <ellipse className="hm-apa-mica" cx="0" cy="-2" rx="10" ry="4" />
      <path className="hm-lemn" d="M14 10 L16 -26" strokeWidth="3" />
      <path className="hm-lemn" d="M-14 -40 L30 -10" strokeWidth="3" />
      <path className="hm-funie" d="M-12 -38 V-4" />
    </g>
  );
}

function Casa({ x, y, s = 1 }: { x: number; y: number; s?: number }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${s})`}>
      <ellipse className="hm-umbra" cx="16" cy="24" rx="24" ry="5" />
      <rect className="hm-perete" x="0" y="6" width="32" height="18" />
      <rect className="hm-zid-umbra" x="20" y="6" width="12" height="18" />
      <path className="hm-acoperis" d="M-4 8 L16 -8 L36 8 Z" />
      <path className="hm-acoperis-umbra" d="M16 -8 L36 8 L16 8 Z" />
      <rect className="hm-poarta" x="8" y="14" width="7" height="10" />
      <rect className="hm-fereastra" x="22" y="12" width="6" height="5" />
    </g>
  );
}

function MoaraVant({ x, y }: { x: number; y: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse className="hm-umbra" cx="4" cy="34" rx="20" ry="5" />
      <path className="hm-perete" d="M-12 34 L-7 -6 L7 -6 L12 34 Z" />
      <path className="hm-zid-umbra" d="M2 -6 L7 -6 L12 34 L4 34 Z" />
      <path className="hm-acoperis" d="M-10 -5 L0 -18 L10 -5 Z" />
      <rect className="hm-poarta" x="-4" y="22" width="8" height="12" />
      <g className="hm-palete">
        {[0, 90, 180, 270].map((u) => (
          <g key={u} transform={`rotate(${u})`}>
            <rect className="hm-lemn-plin" x="-1.5" y="-34" width="3" height="32" />
            <rect className="hm-panza" x="1.5" y="-33" width="9" height="24" />
          </g>
        ))}
        <circle className="hm-lemn-plin" r="3.5" />
        <animateTransform attributeName="transform" type="rotate" from="0" to="360" dur="9s" repeatCount="indefinite" additive="sum" />
      </g>
    </g>
  );
}

function Turn() {
  return (
    <g transform="translate(184 46)">
      <ellipse className="hm-umbra" cx="18" cy="74" rx="34" ry="7" />
      <rect className="hm-turn" x="0" y="14" width="36" height="60" />
      <rect className="hm-turn-umbra" x="22" y="14" width="14" height="60" />
      {[0, 9, 18, 27].map((x) => <rect key={x} className="hm-turn" x={x} y="8" width="6" height="7" />)}
      <path className="hm-acoperis" d="M-4 9 L18 -22 L40 9 Z" />
      <rect className="hm-fereastra-lumina" x="12" y="30" width="7" height="11" rx="3" />
      <rect className="hm-fereastra-lumina" x="12" y="52" width="7" height="9" rx="3" />
      <path className="hm-poarta" d="M10 74 V64 a6 6 0 0 1 12 0 V74 Z" />
    </g>
  );
}

function InsulaHarta({ forma }: { forma: OperaHarta["forma"] }) {
  const d = FORMA_INSULA[forma];
  return (
    <g>
      <path className="hm-apa-mica" d={d} transform={micsorat(1.1)} />
      <path className="hm-nisip" d={d} />
      <path className="hm-iarba" d={d} transform={micsorat(0.84)} />
      <path className="hm-iarba-2" d="M80 80 C100 66 140 70 150 84 C140 100 96 100 80 80 Z" />
      <path className="hm-iarba-2" d="M170 140 C190 126 230 132 236 146 C222 160 180 156 170 140 Z" />
      <path className="hm-iarba-3" d={d} transform={micsorat(0.84)} fill="none" />
      {forma === "castel" && (
        <g>
          <path className="hm-drum" d="M120 176 C140 150 170 160 190 140 C206 124 210 118 214 112" />
          {[[64, 70, 1], [82, 58, 1.1], [98, 76, 0.9], [56, 96, 1], [114, 60, 0.8], [76, 92, 0.85]].map(([x, y, s], i) => <Brad key={i} x={x} y={y} s={s} />)}
          <Pom x={122} y={104} r={9} /><Pom x={244} y={142} r={10} /><Pom x={228} y={160} r={7} />
          <Fantana x={70} y={150} />
          <Castel />
        </g>
      )}
      {forma === "moara" && (
        <g>
          <path className="hm-drum" d="M90 110 C120 130 150 120 170 140 C186 156 176 172 160 186" />
          <path className="hm-drum" d="M170 140 C200 132 230 120 270 120" />
          <rect className="hm-lemn-plin" x="262" y="114" width="40" height="10" />
          {[268, 278, 288, 298].map((x) => <rect key={x} className="hm-trunchi" x={x} y="124" width="2.5" height="7" />)}
          <Casa x={64} y={84} />
          <MoaraVant x={176} y={72} />
          <Pom x={120} y={80} r={11} /><Pom x={134} y={98} r={8} /><Pom x={234} y={150} r={10} /><Pom x={88} y={150} r={9} />
          <Brad x={238} y={70} /><Brad x={252} y={88} s={0.85} />
        </g>
      )}
      {forma === "turn" && (
        <g>
          <path className="hm-drum" d="M110 170 C140 150 170 140 202 122" />
          {[[66, 80, 1], [84, 70, 1.1], [80, 100, 0.9], [252, 120, 0.9], [240, 140, 1]].map(([x, y, s], i) => <Brad key={i} x={x} y={y} s={s} />)}
          <Pom x={120} y={128} r={9} /><Pom x={102} y={150} r={7} />
          <Turn />
        </g>
      )}
    </g>
  );
}

function Pod({ x1, y1, x2, y2, blocat }: { x1: number; y1: number; x2: number; y2: number; blocat?: boolean }) {
  const lung = Math.hypot(x2 - x1, y2 - y1);
  const unghi = (Math.atan2(y2 - y1, x2 - x1) * 180) / Math.PI;
  const n = Math.floor(lung / 9);
  return (
    <g transform={`translate(${x1} ${y1}) rotate(${unghi})`} className={blocat ? "hm-pod blocat" : "hm-pod"}>
      <rect className="hm-umbra" x="0" y="-6" width={lung} height="16" />
      {Array.from({ length: n }, (_, i) => (
        <rect key={i} className="hm-scandura" x={i * 9 + 2} y="-9" width="6.5" height="18" rx="1" />
      ))}
      <path className="hm-funie" d={`M0 -9 Q${lung / 2} -4 ${lung} -9 M0 9 Q${lung / 2} 14 ${lung} 9`} />
      {[0, lung].map((x) => (
        <g key={x}>
          <rect className="hm-lemn-plin" x={x - 3} y="-13" width="6" height="7" />
          <rect className="hm-lemn-plin" x={x - 3} y="7" width="6" height="7" />
        </g>
      ))}
    </g>
  );
}

function Iconite({ x, y, activ }: { x: number; y: number; activ: boolean }) {
  return (
    <g transform={`translate(${x} ${y})`} className={activ ? "hm-iconite activ" : "hm-iconite"}>
      {[-22, 0, 22].map((dx, i) => (
        <g key={i} transform={`translate(${dx} 0)`}>
          <circle className={`hm-ic ${i === 1 && activ ? "galben" : i === 2 && activ ? "galben" : ""}`} r="9" />
          {i === 0 && <path className="hm-ic-semn" d="M-5 -3 h6 v6 h-6 Z M1 -1 l4 -2 v6 l-4 -2 Z" />}
          {i === 1 && <path className="hm-ic-semn" d="M-4 -4 h8 v8 h-8 Z M-2 -1 h4 M-2 1.5 h4" />}
          {i === 2 && <path className="hm-ic-semn" d="M-2.5 -4 L4.5 0 L-2.5 4 Z" />}
        </g>
      ))}
    </g>
  );
}

function NodNivel({ n, stare, x, y, onClick }: { n: number; stare: StareNivel; x: number; y: number; onClick?: () => void }) {
  const apasabil = stare !== "blocat" && !!onClick;
  return (
    <g transform={`translate(${x} ${y})`} className={`hm-nod ${stare}`}
      role={apasabil ? "button" : undefined} tabIndex={apasabil ? 0 : undefined}
      aria-label={`Nivelul ${n}${stare === "terminat" ? ", terminat" : stare === "blocat" ? ", blocat" : ""}`}
      onClick={apasabil ? onClick : undefined}
      onKeyDown={apasabil ? (e) => { if (e.key === "Enter" || e.key === " ") onClick?.(); } : undefined}>
      {stare === "curent" && <circle className="hm-nod-inel" r="31" />}
      <circle className="hm-nod-cerc" r="22" />
      {stare === "terminat" && <path className="hm-nod-semn" d="M-9 0 L-3 7 L10 -7" />}
      {stare === "curent" && <text className="hm-nod-nr" y="8">{n}</text>}
      {stare === "blocat" && (
        <g>
          <path className="hm-lacat-arc" d="M-4.5 -1 v-4 a4.5 4.5 0 0 1 9 0 v4" />
          <rect className="hm-lacat" x="-7.5" y="-1" width="15" height="11" rx="2" />
        </g>
      )}
      <g transform="translate(0 36)">
        <rect className="hm-nod-eticheta" x="-34" y="-11" width="68" height="22" rx="11" />
        <text className="hm-nod-text" y="5">Nivelul {n}</text>
      </g>
      <Iconite x={0} y={60} activ={stare !== "blocat"} />
    </g>
  );
}

function Eticheta({ x, y, titlu, autor, blocata }: { x: number; y: number; titlu: string; autor: string; blocata?: boolean }) {
  const w = Math.max(titlu.length * 11 + 40, autor.length * 7 + 30);
  return (
    <g transform={`translate(${x} ${y})`} className={blocata ? "hm-eticheta blocata" : "hm-eticheta"}>
      <rect className="hm-eticheta-umbra" x={-w / 2 + 2} y="-20" width={w} height="48" rx="24" />
      <rect className="hm-eticheta-bg" x={-w / 2} y="-24" width={w} height="48" rx="24" />
      <text className="hm-eticheta-titlu" y="2">{titlu}</text>
      <text className="hm-eticheta-autor" y="17">{autor}</text>
    </g>
  );
}

function BarcaHarta({ x, y, d = 0 }: { x: number; y: number; d?: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <g className="hm-barca" style={{ animationDelay: `${d}s` }}>
        <ellipse className="hm-apa-mica" cx="0" cy="10" rx="34" ry="6" />
        <path className="hm-lemn-plin" d="M-30 2 H30 L22 12 H-22 Z" />
        <path className="hm-lemn-umbra" d="M-26 8 H26 L22 12 H-22 Z" />
        <rect className="hm-lemn-plin" x="-1.5" y="-44" width="3" height="46" />
        <path className="hm-panza" d="M2 -42 C18 -28 22 -12 20 0 H2 Z" />
        <path className="hm-panza-umbra" d="M2 -42 C10 -28 12 -12 11 0 H2 Z" />
        <path className="hm-panza" d="M-2 -32 C-14 -22 -18 -10 -18 0 H-2 Z" />
        <path className="hm-steag" d="M0 -44 V-52 L8 -49 L0 -46" />
      </g>
    </g>
  );
}

function Pescarus({ x, y }: { x: number; y: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <ellipse className="hm-apa-mica" cx="0" cy="20" rx="14" ry="4" />
      <rect className="hm-lemn-plin" x="-4" y="0" width="8" height="20" />
      <path className="hm-pasare" d="M-12 0 C-14 -10 -4 -18 4 -16 C12 -22 18 -16 14 -10 L22 -10 L14 -6 C10 2 -4 4 -12 0 Z" />
      <path className="hm-cioc" d="M14 -10 L24 -9 L14 -7 Z" />
      <circle className="hm-ochi" cx="10" cy="-13" r="1.4" />
      <path className="hm-aripa" d="M-8 -4 C-2 -10 6 -10 8 -4 C2 -2 -4 -1 -8 -4 Z" />
    </g>
  );
}

const VALURI_HARTA = Array.from({ length: 34 }, (_, i) => ({
  x: (i * 197 + 41) % 980 + 10,
  y: (i * 131 + 23) % 580 + 10,
  d: (i * 0.37) % 4,
}));

export function HartaOperelor({ opere = OPERE_EXEMPLU, poduri = PODURI_EXEMPLU, onNivel }: {
  opere?: OperaHarta[];
  poduri?: [number, number, number, number][];
  onNivel?: (opera: OperaHarta, nivelId: string) => void;
}) {
  const uid = useId().replace(/:/g, "");
  return (
    <div className="hm">
    <svg className="hm-svg" viewBox="0 0 1000 600" aria-hidden="true">
      <defs>
        <linearGradient id={`${uid}-apa`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" className="hm-apa-sus" />
          <stop offset="1" className="hm-apa-jos" />
        </linearGradient>
        <radialGradient id={`${uid}-noapte`}>
          <stop offset="0" stopColor="#0b1238" stopOpacity="0.95" />
          <stop offset="0.65" stopColor="#141c50" stopOpacity="0.8" />
          <stop offset="1" stopColor="#141c50" stopOpacity="0" />
        </radialGradient>
        <radialGradient id={`${uid}-stea`}>
          <stop offset="0" stopColor="#fff" stopOpacity="0.9" />
          <stop offset="1" stopColor="#fff" stopOpacity="0" />
        </radialGradient>
      </defs>

      <rect width="1000" height="600" fill={`url(#${uid}-apa)`} />
      <g className="hm-valuri">
        {VALURI_HARTA.map((v, i) => (
          <path key={i} d={`M${v.x} ${v.y} q8 -6 16 0 q8 6 16 0`} style={{ animationDelay: `${v.d}s` }} />
        ))}
      </g>

      {/* zona de noapte (Luceafărul) */}
      {opere.some((o) => o.noapte) && (
        <g>
          <circle cx="850" cy="190" r="260" fill={`url(#${uid}-noapte)`} />
          {[[760, 30], [930, 120], [700, 120], [980, 260], [880, 20], [960, 380], [720, 240]].map(([x, y], i) => (
            <circle key={i} className="hm-steluta" cx={x} cy={y} r={i % 2 ? 1.6 : 2.4} style={{ animationDelay: `${i * 0.5}s` }} />
          ))}
          <path className="hm-luna" d="M806 32 A20 20 0 1 0 828 58 A15 15 0 1 1 806 32 Z" />
          <circle cx="958" cy="48" r="40" fill={`url(#${uid}-stea)`} className="hm-luceafar" />
          <path d="M958 22 L963 43 L984 48 L963 53 L958 74 L953 53 L932 48 L953 43 Z" fill="#fff" className="hm-luceafar" />
          {[100, 128, 156].map((y) => <rect key={y} x="950" y={y} width={y === 128 ? 20 : 30} height="3" rx="1.5" fill="#fff" opacity="0.6" />)}
        </g>
      )}

      {/* stânci, geamandură, bărci, pescăruși */}
      <g className="hm-decor">
        {[[820, 330, 1], [52, 560, 0.8], [210, 585, 0.7], [960, 470, 0.9]].map(([x, y, s], i) => (
          <g key={i} transform={`translate(${x} ${y}) scale(${s})`}>
            <ellipse className="hm-apa-mica" cx="0" cy="6" rx="26" ry="7" />
            <path className="hm-piatra" d="M-20 6 C-18 -8 -6 -14 4 -12 C14 -10 22 -2 20 6 Z" />
            <path className="hm-piatra-lumina" d="M-14 0 C-10 -8 -2 -10 4 -9 C-2 -5 -8 -2 -14 0 Z" />
          </g>
        ))}
        <g transform="translate(540 236)">
          <ellipse className="hm-apa-mica" cx="0" cy="8" rx="12" ry="4" />
          <path className="hm-geamandura" d="M-7 8 L-4 -10 H4 L7 8 Z" />
          <rect x="-5" y="-2" width="10" height="4" fill="#fff" />
          <circle cx="0" cy="-13" r="3" fill="#ffd166" />
        </g>
        <BarcaHarta x={455} y={108} />
        <BarcaHarta x={150} y={430} d={-1.5} />
        <Pescarus x={692} y={386} />
        <Pescarus x={776} y={452} />
      </g>

      {poduri.map(([a, b, c, d], i) => <Pod key={i} x1={a} y1={b} x2={c} y2={d} blocat={i > 0} />)}

      {/* insulele */}
      {opere.map((o) => (
        <g key={o.id} transform={`translate(${o.x} ${o.y}) scale(${o.scara ?? 1})`}
          className={`hm-insula ${o.blocata ? "blocata" : ""} ${o.noapte ? "noapte" : ""}`}>
          <InsulaHarta forma={o.forma} />
          {o.blocata && (
            <g transform="translate(160 112)" className="hm-lacat-mare">
              <circle r="32" />
              <path className="arc" d="M-7 -2 v-6 a7 7 0 0 1 14 0 v6" />
              <rect x="-12" y="-2" width="24" height="18" rx="3" />
            </g>
          )}
        </g>
      ))}

    </svg>
    {/* etichete și nivele (deasupra, nepixelate) */}
    <svg className="hm-ui" viewBox="0 0 1000 600" role="group" aria-label="Harta operelor">
      {opere.map((o) => (
        <g key={o.id + "-ui"}>
          <Eticheta x={o.x + 160 * (o.scara ?? 1)} y={o.y + 4} titlu={o.titlu} autor={o.autor} blocata={o.blocata} />
          {o.nivele.map((nv, i) => (
            <NodNivel key={nv.id} n={i + 1} stare={nv.stare} x={nv.x} y={nv.y} onClick={() => onNivel?.(o, nv.id)} />
          ))}
        </g>
      ))}
    </svg>
    </div>
  );
}

/* ---------------- Pagina de profil ---------------- */
export default function ProfilTeme({ nume = "Ana Popescu", xp = 120 }: { nume?: string; xp?: number }) {
  const { tema, setTema } = useTema();
  const { avatar, setAvatar } = useAvatar();
  // dacă layout.tsx nu are <TemaSite>, profilul își aduce singur stilurile și fundalul
  const inSite = useContext(TemaContext) !== null;
  const nivel = Math.floor(xp / 100) + 1;
  const progres = xp % 100;

  return (
    <div className="bq-pagina">
      {!inSite && (
        <>
          <style>{CSS}</style>
          <TemaFundal />
          <ButonSunet tema={tema} />
        </>
      )}
      <div className="bq-continut">
        <section className="bq-card bq-profil">
          <div className="bq-avatar">
            <Avatar id={avatar} size={132} />
            <span className="bq-nivel">Nivel {nivel}</span>
          </div>
          <div className="bq-info">
            <h1>{nume}</h1>
            <p className="bq-personaj">{AVATARE.find((a) => a.id === avatar)?.nume} · <em>{AVATARE.find((a) => a.id === avatar)?.opera}</em></p>
            <div className="bq-xp-text">
              <span>⭐ {xp} XP</span>
              <span>{100 - progres} XP până la nivelul {nivel + 1}</span>
            </div>
            <div className="bq-xp-bara">
              <div style={{ width: `${progres}%` }} />
            </div>
          </div>
        </section>

        <section className="bq-card">
          <h2>Alege avatarul</h2>
          <p className="bq-sub">Personaje din operele de bac.</p>
          <AlegeAvatar ales={avatar} onAlege={setAvatar} />
        </section>

        <section className="bq-card">
          <h2>Alege tema</h2>
          <p className="bq-sub">Tema se aplică pe tot site-ul: profil, hartă, video și jocuri.</p>
          <div className="bq-teme">
            {TEME.map((t) => (
              <button
                key={t.id}
                type="button"
                onClick={() => setTema(t.id)}
                className={`bq-tema ${tema === t.id ? "activ" : ""}`}
                data-theme={t.id}
                aria-pressed={tema === t.id}
              >
                <div className="bq-mini">
                  <div className="bq-mini-cer" />
                  <div className="bq-mini-mare" />
                  <div className="bq-mini-insula"><Insula /></div>
                </div>
                <div className="bq-tema-nume">
                  <strong>{t.nume}</strong>
                  {tema === t.id && <span className="bq-bifa">✓</span>}
                </div>
                <small>{t.descriere}</small>
              </button>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}

/* ==================================================================
   STILURILE TEMELOR
   ================================================================== */
export const CSS = `
@import url('https://fonts.googleapis.com/css2?family=Baloo+2:wght@500;700;800&family=Inter:wght@400;500;600&family=Lora:wght@600;700&family=Fredoka:wght@500;600;700&family=Nunito:wght@600;700;800&family=Pixelify+Sans:wght@400;500;600;700&display=swap');

/* ---------- variabilele fiecărei teme ---------- */
:root, [data-theme="acvatic"] {
  --cer-1:#c6ecff; --cer-2:#a6dcfb; --cer-3:#8ccdf3; --mare-1:#3aa2e6; --mare-2:#0e4f96;
  --val:rgba(255,250,230,.75); --nisip:#ecd6a3; --iarba:#5daf4b; --iarba-2:#3f8f3a;
  --copac:#2e7d3c; --perete:#f3ead8; --acoperis:#cf4436; --drum:#e6c98e;
  --contur:#2a4a5a; --contur-w:2.5px; --nor:#ffffff;
  --card-bg:#fff7e2; --card-border:4px solid #1d3b3f; --card-umbra:0 8px 0 rgba(14,79,150,.45);
  --card-raza:28px; --card-blur:none;
  --text:#1d3b3f; --text-2:#3d6b6b; --accent:#ffc93d; --accent-2:#22b573; --accent-umbra:#d99a0b;
  --font-titlu:'Fredoka', 'Baloo 2', system-ui, sans-serif; --font-text:'Nunito', system-ui, sans-serif;
  --greutate-titlu:700; --marime-text:15px;
  --afis-cartoon:inline; --afis-real:none; --afis-pixel:none;
  --usa:#4a3426; --umbra:rgba(10,40,80,.22); --lumina:rgba(255,255,255,.3); --insula-filtru:none;
  --orizont:24%; --ins1-top:calc(24% - 74px); --ins2-top:calc(24% - 52px); --barca-top:calc(24% - 46px);
  --mini-orizont:30%;
  --mini-decor:radial-gradient(circle at 20% 70%, rgba(255,255,255,.7) 0 3px, transparent 4px),
    radial-gradient(circle at 80% 55%, rgba(255,255,255,.7) 0 5px, transparent 6px),
    radial-gradient(circle at 70% 85%, rgba(255,255,255,.6) 0 3px, transparent 4px),
    linear-gradient(100deg, transparent 40%, rgba(255,255,255,.18) 45%, transparent 55%);
}
[data-theme="realist"] {
  --cer-1:#1c2a39; --cer-2:#465868; --cer-3:#a98466; --mare-1:#24404d; --mare-2:#040d15;
  --val:rgba(200,215,225,.10); --nisip:#8f7c5e; --iarba:#34472d; --iarba-2:#24331f;
  --copac:#1b2a1c; --perete:#bdb6a8; --acoperis:#5e3326; --drum:#7c6a4e;
  --contur:transparent; --contur-w:0px; --nor:rgba(150,162,178,.38);
  --card-bg:rgba(14,22,31,.6); --card-border:1px solid rgba(255,255,255,.12);
  --card-umbra:0 24px 60px rgba(0,0,0,.5); --card-raza:18px; --card-blur:blur(18px) saturate(1.1);
  --text:#e6ecf0; --text-2:#97a8b5; --accent:#cf9f48; --accent-2:#5f9a7c; --accent-umbra:transparent;
  --font-titlu:'Lora', Georgia, serif; --font-text:'Inter', system-ui, sans-serif;
  --greutate-titlu:700;
  --afis-cartoon:none; --afis-real:inline; --afis-pixel:none; --marime-text:15px;
  --orizont:40%; --ins1-top:58%; --ins2-top:46%; --barca-top:47%; --mini-orizont:42%; --mini-decor:none;
  --usa:#2a1d15; --umbra:rgba(0,0,0,.4); --lumina:rgba(255,255,255,.05); --insula-filtru:drop-shadow(0 8px 10px rgba(0,0,0,.5));
}
[data-theme="pixel"] {
  --cer-1:#1c2ba8; --cer-2:#2d47d6; --cer-3:#4a6cf0; --mare-1:#3d7ff2; --mare-2:#1f3fae;
  --val:rgba(255,255,255,.6); --nisip:#f7e27a; --iarba:#8be05a; --iarba-2:#3fae38;
  --copac:#2a8f3a; --perete:#fff; --acoperis:#e8433a; --drum:#c0782f;
  --contur:#14143a; --contur-w:0px; --nor:#ffffff;
  --card-bg:#fdf6e3; --card-border:none;
  --card-umbra:0 -4px 0 #14143a, 0 4px 0 #14143a, -4px 0 0 #14143a, 4px 0 0 #14143a, 10px 10px 0 2px rgba(10,10,40,.45);
  --card-raza:0px; --card-blur:none;
  --text:#14143a; --text-2:#3a3a6a; --accent:#ffd23f; --accent-2:#e8433a; --accent-umbra:#b38f10;
  --font-titlu:'Pixelify Sans', monospace; --font-text:'Pixelify Sans', monospace;
  --greutate-titlu:700; --marime-text:17px;
  --afis-cartoon:none; --afis-real:none; --afis-pixel:inline;
  --usa:#14143a; --umbra:transparent; --lumina:transparent; --insula-filtru:none;
  --orizont:60%; --ins1-top:63%; --ins2-top:56%; --barca-top:calc(60% - 52px);
  --mini-orizont:55%; --mini-decor:none;
}

html, body { margin:0; }
body { font-family:var(--font-text); color:var(--text); background:var(--mare-2); }

/* ---------- scena de fundal ---------- */
.bq-scena { position:fixed; inset:0; overflow:hidden; z-index:0; pointer-events:none; }
.bq-cer { position:absolute; inset:0 0 calc(100% - var(--orizont) - 2%) 0; background:linear-gradient(var(--cer-1) 0%, var(--cer-2) 60%, var(--cer-3) 100%); }
.bq-mare { position:absolute; inset:var(--orizont) 0 0 0; overflow:hidden; background:linear-gradient(var(--mare-1), var(--mare-2)); }

/* soarele */
.bq-soare { position:absolute; display:none; }
[data-theme="realist"] .bq-soare { display:block; width:200px; height:200px; right:8%; top:calc(42% - 120px); border-radius:50%;
  background:radial-gradient(circle, rgba(255,214,160,.95) 0%, rgba(240,170,110,.55) 14%, rgba(200,130,90,.18) 40%, transparent 68%);
  filter:blur(2px); animation:respira 8s ease-in-out infinite; }
.bq-luna { display:none; }

/* nori */
.bq-nori { position:absolute; inset:0 0 60% 0; }
.bq-nor { position:absolute; width:150px; animation:pluteste 60s linear infinite; }
.bq-nor path { fill:var(--nor); stroke:var(--contur); stroke-width:var(--contur-w); stroke-linejoin:round; }
.bq-nor.n1 { top:12%; animation-duration:70s; animation-delay:-10s; }
.bq-nor.n2 { top:30%; width:110px; animation-duration:90s; animation-delay:-50s; }
.bq-nor.n3 { top:4%; width:190px; animation-duration:110s; animation-delay:-80s; }
[data-theme="realist"] .bq-nor { filter:blur(8px); width:300px; }

/* valuri */
.bq-val { position:absolute; left:0; width:200%; height:40px; animation:valuri 14s linear infinite; }
.bq-val path { fill:var(--val); }
.bq-val.v1 { top:-12px; }
.bq-val.v2 { top:-4px; opacity:.6; animation-duration:22s; animation-direction:reverse; }
.bq-valuri-mici span { position:absolute; width:26px; height:8px; border-top:3px solid var(--val);
  border-radius:50% 50% 0 0; animation:sclipeste 4s ease-in-out infinite; }
[data-theme="realist"] .bq-valuri-mici { display:none; }

/* sclipirea apei (realist) */
.bq-sclipire { position:absolute; inset:0; width:100%; height:100%; display:none; mix-blend-mode:soft-light; opacity:.35; }
[data-theme="realist"] .bq-sclipire { display:block; }
.bq-reflex { display:none; }
[data-theme="realist"] .bq-reflex { display:block; position:absolute; top:0; right:calc(8% + 30px); width:140px; height:100%;
  background:linear-gradient(rgba(240,180,120,.38), transparent 70%); filter:blur(16px);
  animation:respira 5s ease-in-out infinite; }

/* insule */
.bq-insula { position:absolute; animation:leagana 7s ease-in-out infinite; }
.bq-insula.ins1 { width:300px; left:-40px; top:var(--ins1-top); }
.bq-insula.ins2 { width:200px; right:-30px; top:var(--ins2-top); animation-delay:-3s; }
.bq-insula-svg { width:100%; display:block; overflow:visible; }
.desen-cartoon { display:var(--afis-cartoon); }
.desen-real { display:var(--afis-real); }
.desen-pixel { display:var(--afis-pixel); }
.bq-insula-svg .desen-cartoon * { stroke:var(--contur); stroke-width:var(--contur-w); stroke-linejoin:round; }
.i-umbra { fill:var(--umbra); stroke:none !important; }
.i-nisip { fill:var(--nisip); }
.i-iarba { fill:var(--iarba); }
.i-lumina { fill:var(--lumina); stroke:none !important; }
.i-drum { fill:none; stroke:var(--drum) !important; stroke-width:5px !important; stroke-linecap:round; }
.i-copac { fill:var(--copac); }
.i-copac-2 { fill:var(--iarba-2); }
.i-perete { fill:var(--perete); }
.i-acoperis { fill:var(--acoperis); }
.i-usa { fill:var(--usa); }
.r-spuma { stroke:rgba(230,238,242,.35); stroke-width:2.5px; stroke-linecap:round; }
.r-far-lumina { animation:respira 2.5s ease-in-out infinite; }
.bq-insula-svg { filter:var(--insula-filtru); }

/* barca și pescăruși */
.bq-barca { position:absolute; width:70px; top:var(--barca-top); animation:navigheaza 40s linear infinite; }
.bq-barca-svg { width:100%; animation:leagana 3s ease-in-out infinite; }
.bq-barca-svg .desen-cartoon * { stroke:var(--contur); stroke-width:var(--contur-w); stroke-linejoin:round; }
.b-vela { fill:#fff; } .b-vela-2 { fill:#f1f1f1; }
.b-catarg { stroke:#5a3a22 !important; stroke-width:3px !important; }
.b-corp { fill:#a0522d; }
[data-theme="realist"] .bq-barca-svg { filter:drop-shadow(0 4px 4px rgba(0,0,0,.35)); }

.bq-pescarus { position:absolute; width:26px; height:10px; border-top:3px solid #fff;
  border-radius:50% 50% 0 0; animation:zboara 26s linear infinite; }
.bq-pescarus::after { content:""; position:absolute; left:100%; top:-3px; width:26px; height:10px;
  border-top:3px solid #fff; border-radius:50% 50% 0 0; }
.bq-pescarus.p1 { top:18%; } .bq-pescarus.p2 { top:24%; animation-delay:-13s; transform:scale(.7); }
[data-theme="realist"] .bq-pescarus, [data-theme="realist"] .bq-pescarus::after { border-color:#0d151b; border-top-width:2px; }

/* ---------- insule în plus, valuri, orizont, vânt ---------- */
.bq-insula.ins3, .bq-insula.ins4, .bq-insula.ins5,
.bq-orizont-real, .bq-valuri-real, .bq-valuri-acv, .bq-vant { display:none; }
[data-theme="realist"] .bq-orizont-real, [data-theme="realist"] .bq-valuri-real,
[data-theme="realist"] .bq-insula.ins3, [data-theme="realist"] .bq-insula.ins4, [data-theme="realist"] .bq-insula.ins5,
[data-theme="acvatic"] .bq-valuri-acv, [data-theme="acvatic"] .bq-vant,
[data-theme="acvatic"] .bq-insula.ins3, [data-theme="acvatic"] .bq-insula.ins4 { display:block; }

/* realist: orizont neregulat */
[data-theme="realist"] .bq-val { display:none; }
.bq-orizont-real { position:absolute; left:0; width:100%; top:calc(var(--orizont) - 46px); height:58px; }
.or-tarm { position:absolute; left:0; bottom:8px; width:100%; height:50px; opacity:.9; filter:blur(.6px); }
.or-hula { position:absolute; left:0; bottom:0; width:200%; height:14px; animation:valuri 40s linear infinite; }
/* realist: straturi de valuri */
.bq-valuri-real { position:absolute; inset:0; }
.vr-strat { position:absolute; left:0; width:100%; overflow:hidden; animation:hula 6s ease-in-out infinite; }
.vr-strat svg { width:200%; height:100%; display:block; animation:valuri 30s linear infinite; }
/* realist: pozițiile insulelor */
[data-theme="realist"] .bq-insula.ins3 { width:80px; left:4%; top:calc(var(--orizont) - 22px); opacity:.8; }
[data-theme="realist"] .bq-insula.ins4 { width:170px; left:23%; top:72%; animation-delay:-2s; }
[data-theme="realist"] .bq-insula.ins5 { width:360px; right:-2%; top:69%; animation-delay:-5s; }
[data-theme="realist"] .r-spuma { animation:spuma 3.5s ease-in-out infinite; }

/* acvatic: insule de mărimi diferite, valuri la suprafață */
[data-theme="acvatic"] .bq-insula.ins3 { width:80px; left:13%; top:calc(var(--orizont) - 36px); animation-delay:-1s; }
[data-theme="acvatic"] .bq-insula.ins4 { width:115px; right:14%; top:calc(var(--orizont) - 48px); animation-delay:-4s; }
[data-theme="acvatic"] .bq-insula.ins2 .bq-insula-svg { transform:scaleX(-1); }
[data-theme="acvatic"] .bq-scena .desen-cartoon .i-copac { transform-box:fill-box; transform-origin:50% 100%; animation:unduire 2.6s ease-in-out infinite; }
.bq-valuri-acv { position:absolute; left:0; width:100%; top:calc(var(--orizont) - 4px); height:80px; }
.va-strat { position:absolute; left:0; width:100%; overflow:hidden; animation:hula 3.5s ease-in-out infinite; }
.va-strat svg { width:200%; height:100%; display:block; animation:valuri 16s linear infinite; }
.va-strat.s0 { top:0; height:34px; }
.va-strat.s1 { top:14px; height:34px; animation-delay:-1.2s; }
.va-strat.s1 svg { animation-duration:22s; animation-direction:reverse; }
.va-strat.s2 { top:28px; height:52px; animation-delay:-2.4s; }
.va-strat.s2 svg { animation-duration:12s; }
/* acvatic: vânt cu sclipici */
.bq-vant { position:absolute; left:0; top:0; width:100%; height:calc(var(--orizont) + 14%); overflow:hidden; }
.vant-rafala { position:absolute; width:220px; left:0; animation:rafala 7s linear infinite; }
.vant-rafala path { fill:none; stroke:#fff; stroke-width:4.5; stroke-linecap:round; stroke-dasharray:260 400;
  animation:deseneaza 7s linear infinite; animation-delay:inherit; }
.vant-rafala path.scurt { stroke-width:2.5; opacity:.7; }
.vant-rafala.r0 { top:6%; } .vant-rafala.r1 { top:34%; animation-delay:-2.3s; width:170px; }
.vant-rafala.r2 { top:62%; animation-delay:-4.6s; } .vant-rafala.r3 { top:18%; animation-delay:-1.1s; width:140px; animation-duration:9s; }
.vant-rafala.r4 { top:48%; animation-delay:-5.8s; width:190px; animation-duration:8s; }
.sclipici { position:absolute; left:0; clip-path:polygon(50% 0, 62% 38%, 100% 50%, 62% 62%, 50% 100%, 38% 62%, 0 50%, 38% 38%);
  filter:drop-shadow(0 0 4px #fff) drop-shadow(0 0 2px #ffd84a); animation:sclipici 11s linear infinite; }

/* ---------- harta operelor ---------- */
.hm { position:relative; }
.hm-svg { display:block; width:100%; height:auto; border-radius:calc(var(--card-raza) - 6px); }
.hm-ui { position:absolute; inset:0; width:100%; height:100%; }
:root, [data-theme="acvatic"] {
  --hm-apa-1:#2a8fd8; --hm-apa-2:#1663ad; --hm-val:rgba(255,255,255,.55); --hm-apa-mica:rgba(140,215,255,.45);
  --hm-nisip:#ecd6a3; --hm-nisip-2:#c9a96c; --hm-iarba:#5daf4b; --hm-iarba-2:#78c35b; --hm-iarba-3:#3f8f3a;
  --hm-copac:#2e7d3c; --hm-copac-2:#43a049; --hm-pom:#4c9c3e; --hm-pom-2:#6cbd52; --hm-trunchi:#7a4a26;
  --hm-drum:#e6c98e; --hm-zid:#ece4d4; --hm-zid-umbra:#c8bca6; --hm-acoperis:#cf4436; --hm-acoperis-umbra:#a8302a;
  --hm-perete:#f3ead8; --hm-fereastra:#4a5a78; --hm-turn:#6b6488; --hm-turn-umbra:#4f4870;
  --hm-lemn:#9a6b3e; --hm-lemn-umbra:#6e4826; --hm-panza:#fbf8f0; --hm-panza-umbra:#ddd6c6; --hm-piatra:#a9adb3; --hm-piatra-lumina:#d6d9dd;
  --hm-umbra:rgba(10,40,60,.22); --hm-eticheta-bg:#ffffff; --hm-eticheta-titlu:#6b2e12; --hm-eticheta-autor:#b0603a;
  --hm-font:'Fredoka', 'Nunito', sans-serif;
}
[data-theme="realist"] {
  --hm-apa-1:#1f3e4f; --hm-apa-2:#0a1d2a; --hm-val:rgba(200,220,232,.22); --hm-apa-mica:rgba(120,160,180,.18);
  --hm-nisip:#9b896a; --hm-nisip-2:#6c5e48; --hm-iarba:#3e5a33; --hm-iarba-2:#4c6a3e; --hm-iarba-3:#2a4024;
  --hm-copac:#1d3121; --hm-copac-2:#2a4530; --hm-pom:#2c4a2c; --hm-pom-2:#3e5f3a; --hm-trunchi:#3a2a1c;
  --hm-drum:#8a7a5c; --hm-zid:#b8b0a0; --hm-zid-umbra:#7e7668; --hm-acoperis:#6e3426; --hm-acoperis-umbra:#4e2419;
  --hm-perete:#c9bfae; --hm-fereastra:#ffd27a; --hm-turn:#55516a; --hm-turn-umbra:#3c394e;
  --hm-lemn:#5a4632; --hm-lemn-umbra:#3c2e20; --hm-panza:#d9d4c8; --hm-panza-umbra:#a9a397; --hm-piatra:#6a6e72; --hm-piatra-lumina:#8e9296;
  --hm-umbra:rgba(0,0,0,.4); --hm-eticheta-bg:rgba(14,22,31,.82); --hm-eticheta-titlu:#e6ecf0; --hm-eticheta-autor:#c9a14e;
  --hm-font:'Lora', Georgia, serif;
}
[data-theme="pixel"] {
  --hm-apa-1:#3d7ff2; --hm-apa-2:#2448c8; --hm-val:rgba(255,255,255,.8); --hm-apa-mica:rgba(150,200,255,.55);
  --hm-nisip:#f7e27a; --hm-nisip-2:#c9a23a; --hm-iarba:#5cc93e; --hm-iarba-2:#8be05a; --hm-iarba-3:#2e9e2e;
  --hm-copac:#1f8a3a; --hm-copac-2:#3fae38; --hm-pom:#2e9e3a; --hm-pom-2:#7ee05a; --hm-trunchi:#6b3a17;
  --hm-drum:#e0b060; --hm-zid:#ffffff; --hm-zid-umbra:#c0c0d8; --hm-acoperis:#e8433a; --hm-acoperis-umbra:#b02a2a;
  --hm-perete:#fff6e0; --hm-fereastra:#14143a; --hm-turn:#7a6ad0; --hm-turn-umbra:#5446a8;
  --hm-lemn:#a0522d; --hm-lemn-umbra:#6b3417; --hm-panza:#ffffff; --hm-panza-umbra:#d6d6ee; --hm-piatra:#a0a0b8; --hm-piatra-lumina:#e0e0f0;
  --hm-umbra:rgba(10,10,60,.3); --hm-eticheta-bg:#fdf6e3; --hm-eticheta-titlu:#14143a; --hm-eticheta-autor:#c4302b;
  --hm-font:'Pixelify Sans', monospace;
}
.hm-apa-sus { stop-color:var(--hm-apa-1); } .hm-apa-jos { stop-color:var(--hm-apa-2); }
.hm-valuri path { fill:none; stroke:var(--hm-val); stroke-width:2.5; stroke-linecap:round; animation:sclipeste 4s ease-in-out infinite; }
.hm-apa-mica { fill:var(--hm-apa-mica); }
.hm-nisip { fill:var(--hm-nisip); stroke:var(--hm-nisip-2); stroke-width:4; }
.hm-iarba { fill:var(--hm-iarba); }
.hm-iarba-2 { fill:var(--hm-iarba-2); opacity:.7; }
.hm-iarba-3 { stroke:var(--hm-iarba-3); stroke-width:3; opacity:.6; }
.hm-copac { fill:var(--hm-copac); } .hm-copac-lumina { fill:var(--hm-copac-2); }
.hm-pom { fill:var(--hm-pom); } .hm-pom-lumina { fill:var(--hm-pom-2); }
.hm-trunchi { fill:var(--hm-trunchi); }
.hm-umbra { fill:var(--hm-umbra); }
.hm-drum { fill:none; stroke:var(--hm-drum); stroke-width:9; stroke-linecap:round; }
.hm-zid { fill:var(--hm-zid); } .hm-zid-umbra { fill:var(--hm-zid-umbra); }
.hm-acoperis { fill:var(--hm-acoperis); } .hm-acoperis-umbra { fill:var(--hm-acoperis-umbra); }
.hm-perete { fill:var(--hm-perete); }
.hm-poarta { fill:#4a3426; }
.hm-fereastra { fill:var(--hm-fereastra); }
.hm-fereastra-lumina { fill:#ffd96a; animation:respira 2.5s ease-in-out infinite; }
.hm-turn { fill:var(--hm-turn); } .hm-turn-umbra { fill:var(--hm-turn-umbra); }
.hm-steag { fill:#e8433a; stroke:#7a4a26; stroke-width:1.5; }
.hm-piatra { fill:var(--hm-piatra); } .hm-piatra-lumina { fill:var(--hm-piatra-lumina); }
.hm-lemn { fill:none; stroke:var(--hm-lemn); stroke-linecap:round; }
.hm-lemn-plin { fill:var(--hm-lemn); } .hm-lemn-umbra { fill:var(--hm-lemn-umbra); }
.hm-funie { fill:none; stroke:#5a3a1e; stroke-width:1.6; }
.hm-panza { fill:var(--hm-panza); } .hm-panza-umbra { fill:var(--hm-panza-umbra); }
.hm-scandura { fill:var(--hm-lemn); stroke:var(--hm-lemn-umbra); stroke-width:1; }
.hm-pod.blocat { opacity:.55; }
.hm-geamandura { fill:#e04848; }
.hm-pasare { fill:#fafafa; stroke:#7a8590; stroke-width:1; } .hm-aripa { fill:#d8dde2; }
.hm-cioc { fill:#f2a93b; } .hm-ochi { fill:#222; }
.hm-barca { animation:leagana 3.5s ease-in-out infinite; transform-box:fill-box; transform-origin:50% 100%; }
.hm-steluta { fill:#fff; animation:sclipeste 3s ease-in-out infinite; }
.hm-luna { fill:#f6eec0; }
.hm-luceafar { animation:respira 3s ease-in-out infinite; }
.hm-insula.blocata { filter:saturate(.35) brightness(.62); }
.hm-insula.blocata.noapte { filter:saturate(.45) brightness(.55) hue-rotate(10deg); }
.hm-lacat-mare circle { fill:#1f2638; stroke:#6a7390; stroke-width:5; }
.hm-lacat-mare .arc { fill:none; stroke:#fff; stroke-width:4.5; stroke-linecap:round; }
.hm-lacat-mare rect { fill:#fff; }
.hm-eticheta-umbra { fill:rgba(0,0,0,.2); }
.hm-eticheta-bg { fill:var(--hm-eticheta-bg); }
.hm-eticheta-titlu { fill:var(--hm-eticheta-titlu); font:700 21px var(--hm-font); text-anchor:middle; }
.hm-eticheta-autor { fill:var(--hm-eticheta-autor); font:600 12px var(--hm-font); text-anchor:middle; }
.hm-eticheta.blocata .hm-eticheta-bg { fill:rgba(40,50,72,.88); }
.hm-eticheta.blocata .hm-eticheta-titlu { fill:#fff; } .hm-eticheta.blocata .hm-eticheta-autor { fill:#b8c0d4; }
.hm-nod { cursor:default; }
.hm-nod[role="button"] { cursor:pointer; }
.hm-nod[role="button"]:hover .hm-nod-cerc { transform:scale(1.08); }
.hm-nod[role="button"]:focus { outline:none; }
.hm-nod[role="button"]:focus-visible .hm-nod-cerc { stroke:#ffd166; }
.hm-nod-cerc { stroke:#fff; stroke-width:5; transition:transform .15s; transform-box:fill-box; transform-origin:center; }
.hm-nod.terminat .hm-nod-cerc { fill:#22b573; }
.hm-nod.curent .hm-nod-cerc { fill:#ffb800; }
.hm-nod.blocat .hm-nod-cerc { fill:#2a3348; stroke:#8a93a8; }
.hm-nod-inel { fill:none; stroke:#fff3a0; stroke-width:5; opacity:.8; animation:respira 1.6s ease-in-out infinite; }
.hm-nod-semn { fill:none; stroke:#fff; stroke-width:5; stroke-linecap:round; stroke-linejoin:round; }
.hm-nod-nr { fill:#3a2400; font:800 22px var(--hm-font); text-anchor:middle; }
.hm-lacat { fill:#fff; }
.hm-lacat-arc { fill:none; stroke:#fff; stroke-width:2.8; stroke-linecap:round; }
.hm-nod-eticheta { fill:rgba(30,42,62,.82); }
.hm-nod-text { fill:#fff; font:700 13px var(--hm-font); text-anchor:middle; }
.hm-ic { fill:rgba(30,42,62,.82); stroke:rgba(255,255,255,.7); stroke-width:1.5; }
.hm-ic.galben { fill:#ffb800; }
.hm-ic-semn { fill:none; stroke:#fff; stroke-width:1.6; stroke-linejoin:round; }
.hm-iconite:not(.activ) { opacity:.7; }
/* pixel: insulele și decorul se pixelează */
[data-theme="pixel"] .hm-svg { filter:url(#hm-pix); }
[data-theme="pixel"] .hm-svg { border-radius:0; }
[data-theme="pixel"] .hm-eticheta-bg, [data-theme="pixel"] .hm-nod-eticheta { rx:0; }
[data-theme="pixel"] .hm-nod-cerc { stroke:#14143a; stroke-width:4; }
[data-theme="realist"] .hm-nod.terminat .hm-nod-cerc { fill:#3c7a5a; } [data-theme="realist"] .hm-nod.curent .hm-nod-cerc { fill:#cf9f48; }
[data-theme="realist"] .hm-nod-cerc { stroke:rgba(255,255,255,.75); stroke-width:3; }
[data-theme="realist"] .hm-pasare { fill:#cfd3d6; }

/* realist: păsări */
.bq-pasari { display:none; }
[data-theme="realist"] .bq-pasari { display:block; }
[data-theme="realist"] .bq-pescarus { display:none; }
.pasare { position:absolute; left:0; animation:zbor 40s linear infinite; }
.pasare svg { display:block; animation:planare 6s ease-in-out infinite; opacity:.9; }
.pasare.inv { animation-name:zbor-invers; }
.pasare.inv svg { transform:scaleX(-1); animation-name:planare-inv; }

/* butonul de sunet */
.bq-sunet { all:unset; box-sizing:border-box; position:fixed; left:16px; bottom:16px; z-index:50; cursor:pointer;
  display:flex; align-items:center; gap:8px; padding:10px 16px; font-family:var(--font-text); font-weight:700; font-size:15px;
  color:var(--text); background:var(--card-bg); border:var(--card-border); border-radius:999px; box-shadow:var(--card-umbra);
  backdrop-filter:var(--card-blur); -webkit-backdrop-filter:var(--card-blur); transition:transform .15s; }
.bq-sunet:hover { transform:translateY(-2px); }
.bq-sunet:focus-visible { outline:3px solid var(--accent); outline-offset:3px; }
.bq-sunet-icon { font-size:18px; }
.bq-sunet[aria-pressed="true"] .bq-sunet-icon { animation:respira 1.2s ease-in-out infinite; }
[data-theme="pixel"] .bq-sunet { border-radius:0; font-size:17px; }

/* ---------- lumea acvatică ---------- */
.bq-acvatic, .bq-pixel-decor { display:none; }
[data-theme="acvatic"] .bq-acvatic { display:block; }
[data-theme="acvatic"] .bq-pescarus { display:none; }
[data-theme="acvatic"] .bq-valuri-mici { display:none; }
[data-theme="acvatic"] .bq-nor { width:120px; }
[data-theme="acvatic"] .bq-insula.ins1 { width:220px; left:2%; }
[data-theme="acvatic"] .bq-insula.ins2 { width:150px; right:3%; }
.bq-raze span { position:absolute; top:var(--orizont); width:90px; height:80%; transform:skewX(-18deg); transform-origin:top;
  background:linear-gradient(rgba(255,255,240,.32), transparent 85%); animation:raza 7s ease-in-out infinite; }
.bq-bule span { position:absolute; bottom:-40px; border-radius:50%; border:2px solid rgba(255,255,255,.85);
  background:radial-gradient(circle at 30% 30%, rgba(255,255,255,.9) 0 18%, rgba(255,255,255,.12) 20%);
  animation:bula 10s linear infinite; }
.bq-alge { position:absolute; left:0; bottom:0; width:100%; height:200px; }
.alga { fill:none; stroke-width:16; stroke-linecap:round; animation:unduire 5s ease-in-out infinite; }
.alga.a0 { stroke:#2fa36b; } .alga.a1 { stroke:#55c98a; animation-delay:-2s; } .alga.a2 { stroke:#1e7f5c; animation-delay:-3.5s; }
.bq-peste { position:absolute; animation:inoata 26s linear infinite; }
.bq-peste.p1 { width:120px; top:58%; }
.bq-peste.p2 { width:70px; top:76%; animation-duration:34s; animation-delay:-15s; }
.bq-banc { position:absolute; top:40%; width:120px; height:80px; animation:inoata-invers 30s linear infinite; }
.bq-banc svg { position:absolute; width:34px; }
.bq-pisica { position:absolute; right:4%; bottom:-14px; width:150px; animation:leagana 4s ease-in-out infinite; }
@media (max-width:640px) { .bq-pisica { width:100px; } .bq-alge { height:120px; } }

/* ---------- tema pixel ---------- */
[data-theme="pixel"] .bq-pixel-decor { display:block; }
[data-theme="pixel"] .bq-nori, [data-theme="pixel"] .bq-pescarus { display:none; }
[data-theme="pixel"] .bq-cer { background:linear-gradient(var(--cer-1) 0 30%, var(--cer-2) 30% 62%, var(--cer-3) 62% 100%); }
[data-theme="pixel"] .bq-mare { background:repeating-linear-gradient(var(--mare-1) 0 18px, #5b95f5 18px 22px, var(--mare-1) 22px 40px, var(--mare-2) 40px 44px); }
[data-theme="pixel"] .bq-val { display:none; }
[data-theme="pixel"] .bq-valuri-mici span { border-radius:0; border-top:4px dotted rgba(255,255,255,.8); width:28px; animation-timing-function:steps(2); }
[data-theme="pixel"] .bq-insula, [data-theme="pixel"] .bq-barca-svg { animation-timing-function:steps(2); }
[data-theme="pixel"] .bq-barca { animation-timing-function:steps(80); }
[data-theme="pixel"] .bq-insula.ins1 { width:330px; }
[data-theme="pixel"] .bq-insula.ins2 { width:240px; }
.px-stea { position:absolute; width:4px; height:4px; background:#fff;
  box-shadow:4px 0 #cfe0ff, -4px 0 #cfe0ff, 0 4px #cfe0ff, 0 -4px #cfe0ff, 8px 0 #7f9cf0, -8px 0 #7f9cf0, 0 8px #7f9cf0, 0 -8px #7f9cf0; animation:clipire 1.6s steps(2) infinite; }
.px-nor { position:absolute; width:170px; animation:pluteste 80s steps(160) infinite; }
.px-nor.n1 { top:8%; animation-delay:-20s; } .px-nor.n2 { top:22%; width:110px; animation-delay:-55s; animation-duration:100s; }
.px-nor.n3 { top:36%; width:180px; animation-delay:-70s; animation-duration:120s; }
.px-platforma { position:absolute; width:170px; left:2%; top:20%; animation:leagana 3s steps(2) infinite; }
.px-soare { position:absolute; width:110px; right:4%; top:4%; animation:clipire-lent 3s steps(2) infinite; }
.px-dealuri { position:absolute; left:0; width:100%; height:130px; top:calc(var(--orizont) - 128px); }
[data-theme="pixel"] .bq-sclipire { display:none; }
[data-theme="pixel"] .bq-card h1, [data-theme="pixel"] .tema-card h1 { font-size:36px; color:#fff6c8; letter-spacing:1px;
  text-shadow:3px 0 #1a1230, -3px 0 #1a1230, 0 3px #1a1230, 0 -3px #1a1230, 3px 3px #1a1230, -3px -3px #1a1230, 3px -3px #1a1230, -3px 3px #1a1230,
  0 6px 0 #1a1230, 6px 6px 0 #e8433a; }
[data-theme="pixel"] .bq-card h2, [data-theme="pixel"] .tema-card h2 { font-size:24px; color:#1a1230; text-shadow:2px 2px 0 #ffd23f; }
[data-theme="pixel"] .bq-card, [data-theme="pixel"] .tema-card { outline:3px solid #ffe9a8; outline-offset:-10px;
  background:linear-gradient(#fffaf0 0 50%, #f6ead0 50% 100%); }
[data-theme="pixel"] .bq-xp-text { font-weight:600; }
.bq-tema[data-theme="pixel"] .bq-tema-nume strong { font-size:20px; }
[data-theme="pixel"] .bq-avatar-svg { border-radius:0; border:none; box-shadow:0 -4px 0 #14143a, 0 4px 0 #14143a, -4px 0 0 #14143a, 4px 0 0 #14143a; }
[data-theme="pixel"] .bq-nivel { border-radius:0; font-family:'Pixelify Sans', monospace; font-size:14px; font-weight:700; padding:3px 10px; color:var(--text);
  box-shadow:0 -3px 0 #14143a, 0 3px 0 #14143a, -3px 0 0 #14143a, 3px 0 0 #14143a; }
[data-theme="pixel"] .bq-xp-bara { border-radius:0; border:none; height:22px; background:#2a2a5a;
  box-shadow:0 -4px 0 #14143a, 0 4px 0 #14143a, -4px 0 0 #14143a, 4px 0 0 #14143a; }
[data-theme="pixel"] .bq-xp-bara div { border-radius:0; animation:none;
  background:linear-gradient(#b6f58a 0 25%, transparent 25% 70%, rgba(0,0,0,.18) 70%),
    repeating-linear-gradient(90deg, #4cd137 0 14px, #2e9e27 14px 16px, transparent 16px 20px); }
.bq-tema[data-theme="pixel"] .bq-mini { border-radius:0; }
.bq-tema[data-theme="pixel"] .bq-bifa { border-radius:0; }

/* titlurile în stil „bulă” la tema Acvatic */
[data-theme="acvatic"] .bq-card h1, [data-theme="acvatic"] .tema-card h1 { color:#fff7e2; -webkit-text-stroke:0; text-shadow:
  3px 0 #1d3b3f, -3px 0 #1d3b3f, 0 3px #1d3b3f, 0 -3px #1d3b3f, 2px 2px #1d3b3f, -2px -2px #1d3b3f, 2px -2px #1d3b3f, -2px 2px #1d3b3f, 0 6px 0 #1d3b3f; letter-spacing:.5px; }
[data-theme="acvatic"] .bq-card, [data-theme="acvatic"] .tema-card { outline:3px dashed #a9d6f2; outline-offset:-12px; }
[data-theme="acvatic"] .bq-nivel { background:#1d3b3f; color:#fff7e2; box-shadow:none; border:none; }

/* ---------- conținutul paginii ---------- */
.bq-pagina { position:relative; min-height:100vh; }
.bq-site { position:relative; z-index:1; min-height:100vh; }

/* ---------- clase gata făcute pentru colegi ---------- */
.tema-card { color:var(--text); font-family:var(--font-text); }
.tema-text-2 { color:var(--text-2); }
.tema-titlu { font-family:var(--font-titlu); font-weight:var(--greutate-titlu); color:var(--text); }
.tema-buton { all:unset; box-sizing:border-box; display:inline-flex; align-items:center; justify-content:center; gap:8px; cursor:pointer;
  padding:12px 22px; font-family:var(--font-text); font-weight:800; font-size:16px; transition:transform .12s, box-shadow .12s;
  background:var(--accent); color:var(--text); border:3px solid #1d3b3f; border-radius:999px; box-shadow:0 5px 0 #1d3b3f; }
.tema-buton:hover { transform:translateY(-2px); }
.tema-buton:active { transform:translateY(4px); box-shadow:0 1px 0 #1d3b3f; }
.tema-buton:focus-visible { outline:3px solid var(--accent-2); outline-offset:3px; }
.tema-buton:disabled { opacity:.5; pointer-events:none; }
[data-theme="realist"] .tema-buton { background:rgba(255,255,255,.1); color:var(--text); border:1px solid rgba(255,255,255,.28);
  border-radius:12px; box-shadow:0 10px 24px rgba(0,0,0,.35); backdrop-filter:blur(10px); font-weight:600; }
[data-theme="realist"] .tema-buton:active { box-shadow:0 4px 10px rgba(0,0,0,.35); }
[data-theme="pixel"] .tema-buton { background:#ffd23f; color:#14143a; border:none; border-radius:0; font-size:18px;
  box-shadow:0 -4px 0 #14143a, 0 4px 0 #14143a, -4px 0 0 #14143a, 4px 0 0 #14143a, 6px 6px 0 2px rgba(10,10,40,.45); }
[data-theme="pixel"] .tema-buton:active { box-shadow:0 -4px 0 #14143a, 0 4px 0 #14143a, -4px 0 0 #14143a, 4px 0 0 #14143a; }
.bq-continut { position:relative; z-index:1; max-width:860px; margin:0 auto; padding:32px 16px 60px;
  display:flex; flex-direction:column; gap:22px; }
.bq-card, .tema-card { font-size:var(--marime-text); background:var(--card-bg); border:var(--card-border); box-shadow:var(--card-umbra);
  border-radius:var(--card-raza); padding:24px; backdrop-filter:var(--card-blur); -webkit-backdrop-filter:var(--card-blur); }
.bq-card h1, .bq-card h2, .tema-card h1, .tema-card h2 { font-family:var(--font-titlu); font-weight:var(--greutate-titlu); margin:0; line-height:1.15; }
.bq-card h1, .tema-card h1 { font-size:34px; } .bq-card h2, .tema-card h2 { font-size:24px; }
.bq-sub { margin:6px 0 18px; color:var(--text-2); font-size:1em; }

.bq-profil { display:flex; align-items:center; gap:26px; }
.bq-avatar { position:relative; flex-shrink:0; }
.bq-avatar .bq-avatar-svg { width:132px; height:132px; }
.bq-avatar-svg { display:block; border-radius:50%;
  border:var(--contur-w) solid var(--contur); }
[data-theme="realist"] .bq-avatar-svg { box-shadow:0 10px 30px rgba(0,0,0,.45); filter:brightness(.88) saturate(.85); }
.bq-personaj { margin:2px 0 0; font-weight:700; color:var(--text-2); }
.bq-avatare { display:grid; grid-template-columns:repeat(auto-fill, minmax(118px, 1fr)); gap:12px; }
.bq-avatar-opt { all:unset; box-sizing:border-box; cursor:pointer; display:flex; flex-direction:column; align-items:center; gap:4px;
  padding:10px 6px 12px; text-align:center; border-radius:calc(var(--card-raza) - 8px); border:2px solid transparent;
  transition:transform .15s, background .15s; }
.bq-avatar-opt:hover { transform:translateY(-3px); background:color-mix(in srgb, var(--accent) 14%, transparent); }
.bq-avatar-opt:focus-visible { outline:3px solid var(--accent); outline-offset:2px; }
.bq-avatar-opt.activ { background:color-mix(in srgb, var(--accent-2) 18%, transparent); border-color:var(--accent-2); }
.bq-avatar-opt strong { font-family:var(--font-titlu); font-weight:var(--greutate-titlu); font-size:14px; line-height:1.15; margin-top:4px; }
.bq-avatar-opt small { font-size:12px; color:var(--text-2); line-height:1.2; }
.bq-avatar-opt .bq-avatar-svg { transition:transform .2s; }
.bq-avatar-opt.activ .bq-avatar-svg { transform:scale(1.06); }
[data-theme="pixel"] .bq-avatar-opt { border-radius:0; }
[data-theme="pixel"] .bq-avatar-opt .bq-avatar-svg { border-radius:0; box-shadow:0 -3px 0 #14143a, 0 3px 0 #14143a, -3px 0 0 #14143a, 3px 0 0 #14143a; border:none; }
[data-theme="pixel"] .bq-avatar-opt strong { font-size:15px; }
.bq-nivel { position:absolute; left:50%; bottom:-10px; transform:translateX(-50%); white-space:nowrap;
  background:var(--accent); color:#fff; font-weight:800; font-size:13px; padding:4px 12px; border-radius:999px;
  box-shadow:0 3px 0 var(--accent-umbra); }
.bq-info { flex:1; min-width:0; }
.bq-xp-text { display:flex; justify-content:space-between; gap:10px; margin:14px 0 8px; font-weight:700; font-size:.93em; color:var(--text-2); }
.bq-xp-bara { height:18px; border-radius:999px; overflow:hidden; background:rgba(0,0,0,.08);
  border:var(--contur-w) solid var(--contur); }
.bq-xp-bara div { height:100%; border-radius:999px; transition:width .7s;
  background:repeating-linear-gradient(-45deg, var(--accent) 0 12px, color-mix(in srgb, var(--accent) 80%, #fff) 12px 24px);
  animation:dungi 1.2s linear infinite; background-size:34px 34px; }
[data-theme="realist"] .bq-xp-bara { height:8px; border:none; background:rgba(255,255,255,.1); }
[data-theme="realist"] .bq-xp-bara div { background:linear-gradient(90deg, #b8862e, #e8c46c); animation:none; }

.bq-teme { display:grid; grid-template-columns:repeat(3, 1fr); gap:16px; }
.bq-tema { all:unset; box-sizing:border-box; cursor:pointer; display:flex; flex-direction:column; gap:8px;
  padding:10px 10px 14px; background:var(--card-bg); border:var(--card-border); border-radius:var(--card-raza);
  box-shadow:var(--card-umbra); color:var(--text); font-family:var(--font-text);
  transition:transform .2s; backdrop-filter:var(--card-blur); }
[data-theme="realist"].bq-tema { background:#16212b; }
.bq-tema:hover { transform:translateY(-4px); }
.bq-tema:focus-visible { outline:3px solid var(--accent); outline-offset:3px; }
.bq-tema.activ { outline:4px solid var(--accent-2); outline-offset:4px; }
.bq-mini { position:relative; height:110px; border-radius:calc(var(--card-raza) - 8px); overflow:hidden; }
.bq-mini-cer { position:absolute; inset:0 0 calc(100% - var(--mini-orizont) - 2%) 0; background:linear-gradient(var(--cer-1), var(--cer-2), var(--cer-3)); }
.bq-mini-mare { position:absolute; inset:var(--mini-orizont) 0 0 0; background:linear-gradient(var(--mare-1), var(--mare-2)); }
.bq-mini::after { content:""; position:absolute; inset:0; background:var(--mini-decor); pointer-events:none; }
.bq-mini-insula { position:absolute; left:50%; bottom:6px; width:72%; transform:translateX(-50%); }
.bq-tema-nume { display:flex; justify-content:space-between; align-items:center; padding:0 4px; }
.bq-tema-nume strong { font-family:var(--font-titlu); font-weight:var(--greutate-titlu); font-size:19px; }
.bq-tema small { padding:0 4px; color:var(--text-2); font-size:calc(var(--marime-text) * .88); line-height:1.3; }
.bq-bifa { background:var(--accent-2); color:#fff; border-radius:50%; width:24px; height:24px; display:grid; place-items:center; font-weight:800; }

@media (max-width:640px) {
  .bq-profil { flex-direction:column; text-align:center; }
  .bq-xp-text { flex-direction:column; gap:2px; }
  .bq-teme { grid-template-columns:1fr; }
  .bq-mini { height:120px; }
  .bq-insula.ins1 { width:200px; top:70%; }
}

/* ---------- animații ---------- */
@keyframes valuri { to { transform:translateX(-50%); } }
@keyframes pluteste { from { transform:translateX(-260px); } to { transform:translateX(110vw); } }
@keyframes leagana { 0%,100% { transform:translateY(0) rotate(0); } 50% { transform:translateY(-5px) rotate(-1.5deg); } }
@keyframes navigheaza { from { left:-80px; } to { left:105%; } }
@keyframes zboara { from { left:-60px; } to { left:110%; } }
@keyframes sclipeste { 0%,100% { opacity:.15; transform:scale(.8); } 50% { opacity:1; transform:scale(1.1); } }
@keyframes respira { 0%,100% { opacity:.75; } 50% { opacity:1; } }
@keyframes cad { 0% { transform:translate(0,0) rotate(0); opacity:0; } 10% { opacity:1; }
  100% { transform:translate(-120px,110vh) rotate(540deg); opacity:.8; } }
@keyframes pluteste-sus { 0%,100% { transform:translateY(0) rotate(var(--rot,0deg)); } 50% { transform:translateY(-10px) rotate(var(--rot,0deg)); } }
@keyframes raza { 0%,100% { opacity:.35; } 50% { opacity:1; } }
@keyframes bula { 0% { transform:translateY(0) translateX(0); opacity:0; } 10% { opacity:1; } 50% { transform:translateY(-50vh) translateX(14px); }
  100% { transform:translateY(-105vh) translateX(-8px); opacity:.2; } }
@keyframes unduire { 0%,100% { transform:rotate(-4deg); } 50% { transform:rotate(4deg); } }
@keyframes inoata { from { left:-140px; } to { left:105%; } }
@keyframes inoata-invers { from { left:105%; transform:scaleX(-1); } to { left:-140px; transform:scaleX(-1); } }
@keyframes clipire-lent { 0%,100% { transform:scale(1); } 50% { transform:scale(1.04); } }
@keyframes clipire { 0%,100% { opacity:1; } 50% { opacity:0; } }
@keyframes hula { 0%,100% { transform:translateY(0); } 50% { transform:translateY(-3px); } }
@keyframes spuma { 0%,100% { opacity:.35; } 50% { opacity:1; } }
@keyframes rafala { from { transform:translateX(-260px); } to { transform:translateX(105vw); } }
@keyframes deseneaza { 0% { stroke-dashoffset:260; } 45% { stroke-dashoffset:0; } 100% { stroke-dashoffset:-400; } }
@keyframes sclipici { 0% { transform:translate(-20px,0) rotate(0) scale(.6); opacity:0; } 10% { opacity:1; }
  30% { transform:translate(30vw,-14px) rotate(120deg) scale(1); opacity:.4; } 50% { opacity:1; }
  70% { transform:translate(70vw,10px) rotate(260deg) scale(.8); opacity:.5; }
  100% { transform:translate(105vw,-6px) rotate(360deg) scale(1); opacity:0; } }
@keyframes zbor { from { transform:translateX(-80px); } to { transform:translateX(110vw); } }
@keyframes zbor-invers { from { transform:translateX(110vw); } to { transform:translateX(-80px); } }
@keyframes planare { 0%,100% { transform:translateY(0) rotate(-2deg); } 50% { transform:translateY(16px) rotate(3deg); } }
@keyframes planare-inv { 0%,100% { transform:scaleX(-1) translateY(0) rotate(-2deg); } 50% { transform:scaleX(-1) translateY(16px) rotate(3deg); } }
@keyframes dungi { to { background-position:34px 0; } }

@media (prefers-reduced-motion: reduce) {
  .bq-scena *, .bq-xp-bara div { animation:none !important; }
  .pasare { display:none; }
}
::view-transition-old(root), ::view-transition-new(root) { animation-duration:.45s; }
`;
