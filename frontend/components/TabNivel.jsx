"use client";
// components/TabNivel.jsx — fereastra care se deschide când apeși un capitol pe hartă: Videoclip și Notițe.
// Conținutul vine din API (GET /cursuri/{id}). Capitolul e ținut minte ca citit („continuă de unde ai rămas”, iar „Recomandat
// acum” trece la următorul) abia după ce elevul a deschis notițele și, dacă are, videoclipul. Fără link, butonul video lipsește.
// Testul și fișierele sunt ale întregii Lecții (pe pagina ei); culorile din lib/tema.js.
import Link from "next/link";
import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { api, idYoutube } from "@/lib/api";
import { ACTIVITATI } from "@/components/Iconite";
import { T } from "@/lib/tema";

const DESCRIERI = {
  video: "Urmărește capitolul explicat pe scurt",
  materiale: "Citește notițele capitolului",
};
const ACTIVITATI_CAPITOL = ACTIVITATI.filter((a) => a.tip !== "play");

export default function TabNivel({ insula, nivel, parcursId, onInchide, onCitit }) {
  const [continut, setContinut] = useState(null);
  const [eroare, setEroare] = useState("");
  const [vedere, setVedere] = useState("meniu"); // "meniu" | "video" | "materiale"
  const [vazute, setVazute] = useState(() => new Set()); // părțile deschise: "video", "materiale"

  useEffect(() => {
    api(`/cursuri/${nivel.id}`).then(setContinut).catch((e) => setEroare(e.message));
  }, [nivel.id]);
  const youtube = idYoutube(continut?.videoUrl);
  const activitati = ACTIVITATI_CAPITOL.filter((a) => a.tip !== "video" || continut?.videoUrl);

  const deschide = (tip) => {
    setVedere(tip);
    setVazute((v) => (v.has(tip) ? v : new Set(v).add(tip)));
  };

  // Citit = notițele deschise și, dacă există, videoclipul. Doar pentru elevi (profesorii și adminii nu lasă urme).
  const citit = !!continut && vazute.has("materiale") && (!continut.videoUrl || vazute.has("video"));
  useEffect(() => {
    if (citit) api(`/cursuri/${nivel.id}/acces`, { metoda: "POST" }).then(() => onCitit?.(nivel.id)).catch(() => {});
  }, [citit, nivel.id, onCitit]);

  useEffect(() => {
    const tasta = (e) => e.key === "Escape" && onInchide();
    window.addEventListener("keydown", tasta);
    return () => window.removeEventListener("keydown", tasta);
  }, [onInchide]);

  const butonRotund = `grid h-9 w-9 shrink-0 place-items-center rounded-full font-black ${T.suprafata} ${T.suprafataHover}`;
  const butonPrincipal = `mt-4 w-full rounded-xl px-4 py-2.5 font-black ${T.accent}`;

  // Randat direct în <body>: harta are un `filter` (temele de noapte/iarnă), care altfel ar închide fereastra
  // `fixed` în cutia hărții și i-ar tăia conținutul.
  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/55 p-3 backdrop-blur-sm sm:items-center"
      onClick={onInchide}
      role="dialog"
      aria-modal="true"
    >
      <div
        className={`max-h-[92vh] w-full max-w-lg overflow-y-auto rounded-3xl p-5 shadow-2xl ring-1 sm:p-6 ${T.card} ${T.contur}`}
        onClick={(e) => e.stopPropagation()}
      >
        {/* antet */}
        <div className="mb-4 flex items-start gap-3">
          {vedere !== "meniu" && (
            <button onClick={() => setVedere("meniu")} className={butonRotund} aria-label="Înapoi">
              ←
            </button>
          )}
          <div className="min-w-0 flex-1">
            <p className={`text-xs font-bold uppercase tracking-wider ${T.accentText}`}>
              {insula.titlu} · {insula.autor}
            </p>
            <h2 className="text-xl font-extrabold leading-tight">
              {nivel.titlu}: {nivel.curs.titlu}
            </h2>
          </div>
          <button onClick={onInchide} className={`${butonRotund} text-lg`} aria-label="Închide">
            ×
          </button>
        </div>

        {eroare && <p className="mb-3 rounded-xl bg-rose-500/20 px-3 py-2 text-sm font-bold">{eroare}</p>}

        {vedere === "meniu" && (
          <>
            <div className="grid gap-3">
              {activitati.map(({ tip, Icon, eticheta }) => (
                <button
                  key={tip}
                  onClick={() => deschide(tip)}
                  className={`flex w-full items-center gap-4 rounded-2xl p-3 text-left ring-1 transition ${T.suprafataSlaba} ${T.conturSlab} ${T.suprafataHover}`}
                >
                  <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full border-2 border-white/30 bg-slate-500 text-white/90">
                    <Icon className="h-6 w-6" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block font-extrabold">{eticheta}</span>
                    <span className={`block text-sm ${T.textSecundar}`}>{DESCRIERI[tip]}</span>
                  </span>
                  <span className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-bold ${T.suprafata} ${T.textSlab}`}>{vazute.has(tip) ? "✓" : "→"}</span>
                </button>
              ))}
            </div>
            <p className={`mt-4 text-center text-sm ${T.textSlab}`}>
              Lecția e terminată când treci testul ei, cu întrebări din toate capitolele.{" "}
              <Link href={`/test/${parcursId}`} className="font-bold underline">
                Testul lecției →
              </Link>
            </p>
          </>
        )}

        {vedere === "video" && (
          <>
            {youtube ? (
              <iframe
                className="aspect-video w-full rounded-2xl bg-black"
                src={`https://www.youtube-nocookie.com/embed/${youtube}`}
                title={continut?.titlu ?? "Videoclipul capitolului"}
                allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                allowFullScreen
              />
            ) : (
              <div className={`grid aspect-video w-full place-items-center rounded-2xl ring-1 ${T.suprafata} ${T.conturSlab}`}>
                <div className={`text-center ${T.textSecundar}`}>
                  {(() => {
                    const Icon = ACTIVITATI[0].Icon;
                    return <Icon className="mx-auto mb-2 h-10 w-10" />;
                  })()}
                  <p className="font-bold">{continut?.titlu ?? "Videoclipul capitolului"}</p>
                  <p className="text-sm">Videoclipul se deschide pe site-ul sursei.</p>
                  <a className="mt-2 inline-block font-bold underline" href={continut?.videoUrl} target="_blank" rel="noreferrer">
                    Deschide videoclipul
                  </a>
                </div>
              </div>
            )}
            <button onClick={() => deschide("materiale")} className={butonPrincipal}>
              Mai departe: notițele →
            </button>
          </>
        )}

        {vedere === "materiale" && (
          <>
            <div className="space-y-4">
              {(continut?.materiale ?? []).map((m, i) => (
                <section key={i} className={`rounded-2xl p-4 ring-1 ${T.suprafataSlaba} ${T.conturSlab}`}>
                  <h3 className={`mb-1 font-extrabold ${T.accentText}`}>{m.titlu}</h3>
                  <p className={`text-sm leading-relaxed ${T.textSecundar}`}>{m.text}</p>
                </section>
              ))}
              {continut && !continut.materiale?.length && <p className={T.textSecundar}>Capitolul nu are încă notițe.</p>}
            </div>
            <button onClick={onInchide} className={butonPrincipal}>
              Înapoi la hartă
            </button>
          </>
        )}
      </div>
    </div>,
    document.body
  );
}
