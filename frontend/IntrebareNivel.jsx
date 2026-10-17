"use client";
// components/IntrebareNivel.jsx — o întrebare aleasă aleator dintre cele 3 ale nivelului
import Link from "next/link";
import { useEffect, useState } from "react";
import { alegeIntrebare } from "@/lib/intrebari";
import { INSULE } from "@/components/HartaInsule";
import { marcheazaNivelTerminat } from "@/lib/progres";
import NIVELURI from "@/data/niveluri.json";
import { T } from "@/lib/tema";

const LITERE = ["A", "B", "C", "D"];

export default function IntrebareNivel({ nivelId }) {
  const insula = INSULE.find((ins) => ins.nivele.some((n) => n.id === nivelId));
  const nivel = insula?.nivele.find((n) => n.id === nivelId);

  const [intrebare, setIntrebare] = useState(null);
  const [ales, setAles] = useState(null); // indexul variantei apăsate
  const [incarcat, setIncarcat] = useState(false);

  // alegem întrebarea abia în browser (evită erori de hidratare); vine din Supabase, deci așteptăm
  useEffect(() => {
    let anulat = false;
    setIncarcat(false);
    setAles(null);
    alegeIntrebare(nivelId).then((q) => {
      if (anulat) return;
      setIntrebare(q);
      setIncarcat(true);
    });
    return () => {
      anulat = true;
    };
  }, [nivelId]);

  const aRaspuns = ales !== null;
  const corect = aRaspuns && intrebare && ales === intrebare.corect;

  function alege(i) {
    if (aRaspuns) return;
    setAles(i);
    if (i === intrebare.corect) {
      marcheazaNivelTerminat(nivelId);
      // TODO P1: aici se adaugă XP-ul în profil (ex. +100 XP în Supabase)
    }
  }

  async function incearcaDinNou() {
    // alegere complet nouă, fără să țină cont de întrebarea anterioară
    setIncarcat(false);
    setAles(null);
    setIntrebare(await alegeIntrebare(nivelId));
    setIncarcat(true);
  }

  if (!insula || !nivel) {
    return (
      <Ecran>
        <p className="text-lg font-bold">Nivelul acesta nu există.</p>
        <Link href="/harta" className={`mt-4 inline-block rounded-xl px-4 py-2 font-black ${T.accent}`}>
          Înapoi la hartă
        </Link>
      </Ecran>
    );
  }

  return (
    <Ecran>
      <Link href="/harta" className={`mb-4 inline-flex items-center gap-1 text-sm font-semibold ${T.textSecundar} hover:opacity-100`}>
        ← Înapoi la hartă
      </Link>

      <div className={`rounded-3xl p-5 shadow-2xl ring-1 sm:p-7 ${T.card} ${T.contur}`}>
        <div className="mb-5 flex flex-wrap items-center justify-between gap-2">
          <div>
            <p className={`text-xs font-bold uppercase tracking-wider ${T.accentText}`}>
              {insula.titlu} · {insula.autor}
            </p>
            <h1 className="text-2xl font-extrabold">
              {nivel.titlu}
              {NIVELURI[nivelId]?.tema ? `: ${NIVELURI[nivelId].tema}` : ""}
            </h1>
          </div>
          <span className={`rounded-full px-3 py-1 text-sm font-black ${T.accent}`}>+100 XP</span>
        </div>

        {!incarcat ? (
          <p className={`py-10 text-center ${T.textSecundar}`}>Se încarcă întrebarea…</p>
        ) : !intrebare ? (
          <p className={`py-10 text-center ${T.textSecundar}`}>Nu există încă întrebări pentru acest nivel.</p>
        ) : (
          <>
            <p className="mb-5 text-lg font-bold leading-snug sm:text-xl">{intrebare.text}</p>

            <div className="grid gap-3">
              {intrebare.variante.map((varianta, i) => {
                let stil = `${T.suprafata} ${T.contur} ${T.suprafataHover}`;
                if (aRaspuns) {
                  if (i === intrebare.corect) stil = "bg-emerald-500 ring-emerald-300 text-white";
                  else if (i === ales) stil = "bg-rose-500 ring-rose-300 text-white";
                  else stil = `${T.suprafataSlaba} ${T.conturSlab} opacity-50`;
                }
                return (
                  <button
                    key={i}
                    onClick={() => alege(i)}
                    disabled={aRaspuns}
                    className={`flex items-center gap-3 rounded-2xl px-4 py-3 text-left font-semibold ring-1 transition ${stil} ${
                      aRaspuns ? "cursor-default" : "cursor-pointer"
                    }`}
                  >
                    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-black/20 text-sm font-black">
                      {LITERE[i]}
                    </span>
                    {varianta}
                  </button>
                );
              })}
            </div>

            {aRaspuns && (
              <div className={`mt-5 rounded-2xl p-4 ring-1 ${corect ? "bg-emerald-500/15 ring-emerald-400/40" : "bg-rose-500/15 ring-rose-400/40"}`}>
                <p className="text-lg font-extrabold">{corect ? "Corect! Ai câștigat 100 XP." : "Greșit, mai încearcă."}</p>
                {intrebare.explicatie && <p className={`mt-1 text-sm ${T.textSecundar}`}>{intrebare.explicatie}</p>}
                <div className="mt-4 flex flex-wrap gap-2">
                  {corect ? (
                    <Link href="/harta" className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>
                      Continuă pe hartă →
                    </Link>
                  ) : (
                    <>
                      <button onClick={incearcaDinNou} className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>
                        Încearcă din nou
                      </button>
                      <Link href="/harta" className={`rounded-xl px-4 py-2 font-bold ${T.suprafata} ${T.suprafataHover}`}>
                        Înapoi la hartă
                      </Link>
                    </>
                  )}
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </Ecran>
  );
}

function Ecran({ children }) {
  return (
    <main className={`flex min-h-screen items-center justify-center p-4 ${T.pagina}`}>
      <div className="w-full max-w-xl">{children}</div>
    </main>
  );
}
