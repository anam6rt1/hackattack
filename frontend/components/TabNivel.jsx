"use client";
// components/TabNivel.jsx — fereastra care se deschide când apeși un nivel pe hartă:
// Videoclip, Materiale, Întrebări. Culorile vin din tema aplicației (lib/tema.js).
import Link from "next/link";
import { useEffect, useState } from "react";
import NIVELURI from "@/data/niveluri.json";
import { marcheazaActivitate } from "@/lib/progres";
import { ACTIVITATI } from "@/components/Iconite";
import { T } from "@/lib/tema";

const DESCRIERI = {
  video: "Urmărește lecția explicată pe scurt",
  materiale: "Citește notițele lecției",
  play: "Răspunde la întrebare și câștigă XP",
};

// cerculețele rămân gri → galben, ca pe hartă
const CERC_FACUT = "border-white bg-amber-400 text-amber-950";
const CERC_NEFACUT = "border-white/30 bg-slate-500 text-white/80";

export default function TabNivel({ insula, nivel, activitati = {}, onInchide, onActualizare }) {
  const continut = NIVELURI[nivel.id] ?? {};
  const [vedere, setVedere] = useState("meniu"); // "meniu" | "video" | "materiale"

  useEffect(() => {
    const tasta = (e) => e.key === "Escape" && onInchide();
    window.addEventListener("keydown", tasta);
    return () => window.removeEventListener("keydown", tasta);
  }, [onInchide]);

  function termina(tip) {
    marcheazaActivitate(nivel.id, tip);
    onActualizare();
    setVedere("meniu");
  }

  const parcurse = ACTIVITATI.filter((a) => activitati[a.tip]).length;
  const butonRotund = `grid h-9 w-9 shrink-0 place-items-center rounded-full font-black ${T.suprafata} ${T.suprafataHover}`;
  const butonPrincipal = `mt-4 w-full rounded-xl px-4 py-2.5 font-black ${T.accent}`;

  return (
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
              {nivel.titlu}
              {continut.tema ? `: ${continut.tema}` : ""}
            </h2>
          </div>
          <button onClick={onInchide} className={`${butonRotund} text-lg`} aria-label="Închide">
            ×
          </button>
        </div>

        {vedere === "meniu" && (
          <>
            <div className="grid gap-3">
              {ACTIVITATI.map(({ tip, Icon, eticheta }) => {
                const facut = !!activitati[tip];
                const interior = (
                  <>
                    <span className={`grid h-12 w-12 shrink-0 place-items-center rounded-full border-2 transition ${facut ? CERC_FACUT : CERC_NEFACUT}`}>
                      <Icon className="h-6 w-6" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block font-extrabold">{eticheta}</span>
                      <span className={`block text-sm ${T.textSecundar}`}>{DESCRIERI[tip]}</span>
                    </span>
                    <span
                      className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-bold ${
                        facut ? "bg-amber-400 text-amber-950" : `${T.suprafata} ${T.textSlab}`
                      }`}
                    >
                      {facut ? "Parcurs ✓" : "→"}
                    </span>
                  </>
                );
                const stil = `flex w-full items-center gap-4 rounded-2xl p-3 text-left ring-1 transition ${T.suprafataSlaba} ${T.conturSlab} ${T.suprafataHover}`;
                return tip === "play" ? (
                  <Link key={tip} href={`/nivel/${nivel.id}`} className={stil}>
                    {interior}
                  </Link>
                ) : (
                  <button key={tip} onClick={() => setVedere(tip)} className={stil}>
                    {interior}
                  </button>
                );
              })}
            </div>
            <p className={`mt-4 text-center text-sm ${T.textSlab}`}>
              {parcurse} din 3 parcurse · nivelul se termină când răspunzi corect la întrebare
            </p>
          </>
        )}

        {vedere === "video" && (
          <>
            {continut.video?.youtubeId ? (
              <iframe
                className="aspect-video w-full rounded-2xl bg-black"
                src={`https://www.youtube-nocookie.com/embed/${continut.video.youtubeId}`}
                title={continut.video.titlu ?? "Videoclipul lecției"}
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
                  <p className="font-bold">{continut.video?.titlu ?? "Videoclipul lecției"}</p>
                  <p className="text-sm">Videoclipul va apărea aici.</p>
                </div>
              </div>
            )}
            <button onClick={() => termina("video")} className={butonPrincipal}>
              Am vizionat ✓
            </button>
          </>
        )}

        {vedere === "materiale" && (
          <>
            <div className="space-y-4">
              {(continut.materiale ?? []).map((m, i) => (
                <section key={i} className={`rounded-2xl p-4 ring-1 ${T.suprafataSlaba} ${T.conturSlab}`}>
                  <h3 className={`mb-1 font-extrabold ${T.accentText}`}>{m.titlu}</h3>
                  <p className={`text-sm leading-relaxed ${T.textSecundar}`}>{m.text}</p>
                </section>
              ))}
              {!continut.materiale?.length && <p className={T.textSecundar}>Materialele vor apărea aici.</p>}
            </div>
            <button onClick={() => termina("materiale")} className={butonPrincipal}>
              Am citit ✓
            </button>
          </>
        )}
      </div>
    </div>
  );
}
