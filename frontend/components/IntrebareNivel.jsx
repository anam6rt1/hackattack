"use client";
// components/IntrebareNivel.jsx — o întrebare aleasă aleator dintre cele ale lecției.
// Răspunsul corect nu ajunge în browser înainte să alegi: serverul verifică varianta și acordă XP-ul.
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { T } from "@/lib/tema";

const LITERE = ["A", "B", "C", "D", "E", "F"];

export default function IntrebareNivel({ cursId }) {
  const [curs, setCurs] = useState(null);
  const [intrebari, setIntrebari] = useState(null);
  const [intrebare, setIntrebare] = useState(null);
  const [ales, setAles] = useState(null); // id-ul variantei apăsate
  const [rezultat, setRezultat] = useState(null); // { corect, variantaCorectaId, explicatie, xpCastigat }
  const [eroare, setEroare] = useState("");

  const alegeAlta = useCallback((lista, anterioara) => {
    // altă întrebare decât cea anterioară, dacă există
    const posibile = lista.length > 1 ? lista.filter((q) => q.id !== anterioara?.id) : lista;
    setIntrebare(posibile[Math.floor(Math.random() * posibile.length)] ?? null);
    setAles(null);
    setRezultat(null);
  }, []);

  useEffect(() => {
    Promise.all([api(`/cursuri/${cursId}`), api(`/cursuri/${cursId}/intrebari`)])
      .then(([c, q]) => {
        setCurs(c);
        setIntrebari(q.elemente);
        alegeAlta(q.elemente, null);
      })
      .catch((e) => setEroare(e.message));
  }, [cursId, alegeAlta]);

  async function alege(variantaId) {
    if (ales !== null) return;
    setAles(variantaId);
    try {
      setRezultat(await api(`/cursuri/${cursId}/raspuns`, { metoda: "POST", corp: { intrebareId: intrebare.id, variantaId } }));
    } catch (e) {
      setAles(null);
      setEroare(e.message);
    }
  }

  const inapoi = curs ? `/materiale/${curs.materieId}` : "/materiale";

  if (eroare && !curs) {
    return (
      <Ecran>
        <p className="text-lg font-bold">{eroare}</p>
        <Link href="/materiale" className={`mt-4 inline-block rounded-xl px-4 py-2 font-black ${T.accent}`}>
          Înapoi la materiale
        </Link>
      </Ecran>
    );
  }

  const corect = rezultat?.corect;
  const dejaTerminat = curs?.progres?.quizPromovat;

  return (
    <Ecran>
      <Link href={inapoi} className={`mb-4 inline-flex items-center gap-1 text-sm font-semibold ${T.textSecundar} hover:opacity-100`}>
        ← Înapoi la hartă
      </Link>

      <div className={`rounded-3xl p-5 shadow-2xl ring-1 sm:p-7 ${T.card} ${T.contur}`}>
        <div className="mb-5 flex flex-wrap items-center justify-between gap-2">
          <div>
            <p className={`text-xs font-bold uppercase tracking-wider ${T.accentText}`}>
              {curs ? `${curs.materie}${curs.capitol ? ` · ${curs.capitol}` : ""}` : " "}
            </p>
            <h1 className="text-2xl font-extrabold">{curs?.titlu ?? "Se încarcă..."}</h1>
          </div>
          <span className={`rounded-full px-3 py-1 text-sm font-black ${T.accent}`}>{dejaTerminat ? "Recapitulare" : "+100 XP"}</span>
        </div>

        {!intrebari ? (
          <p className={`py-10 text-center ${T.textSecundar}`}>Se încarcă întrebarea…</p>
        ) : !intrebare ? (
          <p className={`py-10 text-center ${T.textSecundar}`}>Nu există încă întrebări pentru această lecție.</p>
        ) : (
          <>
            <p className="mb-5 text-lg font-bold leading-snug sm:text-xl">{intrebare.text}</p>

            <div className="grid gap-3">
              {intrebare.variante.map((varianta, i) => {
                let stil = `${T.suprafata} ${T.contur} ${T.suprafataHover}`;
                if (rezultat) {
                  if (varianta.id === rezultat.variantaCorectaId) stil = "bg-emerald-500 ring-emerald-300 text-white";
                  else if (varianta.id === ales) stil = "bg-rose-500 ring-rose-300 text-white";
                  else stil = `${T.suprafataSlaba} ${T.conturSlab} opacity-50`;
                } else if (varianta.id === ales) stil = `${T.suprafata} ring-amber-300`;
                return (
                  <button
                    key={varianta.id}
                    onClick={() => alege(varianta.id)}
                    disabled={ales !== null}
                    className={`flex items-center gap-3 rounded-2xl px-4 py-3 text-left font-semibold ring-1 transition ${stil} ${ales !== null ? "cursor-default" : "cursor-pointer"}`}
                  >
                    <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-black/20 text-sm font-black">{LITERE[i]}</span>
                    {varianta.text}
                  </button>
                );
              })}
            </div>

            {eroare && <p className="mt-4 rounded-xl bg-rose-500/20 px-3 py-2 text-sm font-bold">{eroare}</p>}

            {rezultat && (
              <div className={`mt-5 rounded-2xl p-4 ring-1 ${corect ? "bg-emerald-500/15 ring-emerald-400/40" : "bg-rose-500/15 ring-rose-400/40"}`}>
                <p className="text-lg font-extrabold">
                  {corect ? (rezultat.xpCastigat ? `Corect! Ai câștigat ${rezultat.xpCastigat} XP.` : "Corect!") : "Greșit, mai încearcă."}
                </p>
                {rezultat.explicatie && <p className={`mt-1 text-sm ${T.textSecundar}`}>{rezultat.explicatie}</p>}
                <div className="mt-4 flex flex-wrap gap-2">
                  {corect ? (
                    <>
                      <Link href={inapoi} className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>
                        Continuă pe hartă →
                      </Link>
                      {intrebari.length > 1 && (
                        <button onClick={() => alegeAlta(intrebari, intrebare)} className={`rounded-xl px-4 py-2 font-bold ${T.suprafata} ${T.suprafataHover}`}>
                          Altă întrebare
                        </button>
                      )}
                    </>
                  ) : (
                    <>
                      <button onClick={() => alegeAlta(intrebari, intrebare)} className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>
                        Încearcă din nou
                      </button>
                      <Link href={inapoi} className={`rounded-xl px-4 py-2 font-bold ${T.suprafata} ${T.suprafataHover}`}>
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
