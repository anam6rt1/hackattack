"use client";
// components/IntrebareNivel.jsx — testul lecției: câteva întrebări alese aleator de server.
// După fiecare răspuns afli doar dacă a fost corect; răspunsurile corecte apar la final, iar XP-ul se primește
// doar dacă treci testul (de ex. 4 din 5). Între două teste e o pauză scurtă.
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { T } from "@/lib/tema";

const LITERE = ["A", "B", "C", "D", "E", "F"];

export default function IntrebareNivel({ cursId }) {
  const [curs, setCurs] = useState(null);
  const [test, setTest] = useState(null); // { id, total, necesare, raspunse, intrebari }
  const [pas, setPas] = useState(0); // indexul întrebării curente în test.intrebari
  const [ales, setAles] = useState(null); // id-ul variantei apăsate
  const [rezultat, setRezultat] = useState(null); // { corect, ramase, final }
  const [texte, setTexte] = useState({}); // intrebareId → întrebarea afișată, pentru rezumatul de la final
  const [eroare, setEroare] = useState("");
  const [seIncarca, setSeIncarca] = useState(true);

  // Pornește un test nou (sau îl reia pe cel neterminat); starea se schimbă doar după răspunsul serverului.
  const incepe = useCallback(
    () =>
      api(`/cursuri/${cursId}/quiz`, { metoda: "POST" })
        .then((t) => {
          setTest(t);
          setTexte((v) => ({ ...v, ...Object.fromEntries(t.intrebari.map((q) => [q.id, q])) }));
          setPas(0);
          setAles(null);
          setRezultat(null);
          setEroare("");
        })
        .catch((e) => {
          setTest(null);
          setEroare(e.message);
        })
        .finally(() => setSeIncarca(false)),
    [cursId],
  );

  useEffect(() => {
    api(`/cursuri/${cursId}`).then(setCurs).catch((e) => setEroare(e.message));
    incepe();
  }, [cursId, incepe]);

  function reia() {
    setSeIncarca(true);
    incepe();
  }

  async function alege(variantaId) {
    if (ales !== null) return;
    setAles(variantaId);
    try {
      const intrebare = test.intrebari[pas];
      setRezultat(await api(`/cursuri/${cursId}/quiz/${test.id}/raspuns`, { metoda: "POST", corp: { intrebareId: intrebare.id, variantaId } }));
    } catch (e) {
      setAles(null);
      setEroare(e.message);
    }
  }

  function urmatoarea() {
    setPas((p) => p + 1);
    setAles(null);
    setRezultat(null);
  }

  const inapoi = curs ? `/materiale/${curs.materieId}` : "/materiale";
  const final = rezultat?.final;
  const intrebare = test?.intrebari[pas];
  const numar = test ? test.raspunse + pas + 1 : 0;

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
          <span className={`rounded-full px-3 py-1 text-sm font-black ${T.accent}`}>{curs?.progres?.quizPromovat ? "Recapitulare" : "+100 XP"}</span>
        </div>

        {seIncarca ? (
          <p className={`py-10 text-center ${T.textSecundar}`}>Se pregătește testul…</p>
        ) : !test ? (
          <div className="py-6 text-center">
            <p className="text-lg font-bold">{eroare || "Testul nu poate fi început acum."}</p>
            <div className="mt-4 flex flex-wrap justify-center gap-2">
              <button onClick={reia} className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>Încearcă din nou</button>
              <Link href={inapoi} className={`rounded-xl px-4 py-2 font-bold ${T.suprafata} ${T.suprafataHover}`}>Înapoi la hartă</Link>
            </div>
          </div>
        ) : final ? (
          <Rezumat final={final} texte={texte} inapoi={inapoi} onReia={reia} />
        ) : intrebare ? (
          <>
            <p className={`mb-2 text-sm font-bold ${T.textSecundar}`}>
              Întrebarea {numar} din {test.total} · ai nevoie de {test.necesare} răspunsuri corecte
            </p>
            <p className="mb-5 text-lg font-bold leading-snug sm:text-xl">{intrebare.text}</p>

            <div className="grid gap-3">
              {intrebare.variante.map((varianta, i) => {
                let stil = `${T.suprafata} ${T.contur} ${T.suprafataHover}`;
                // Doar varianta ta e colorată: răspunsul corect se vede abia la finalul testului.
                if (rezultat && varianta.id === ales) stil = rezultat.corect ? "bg-emerald-500 ring-emerald-300 text-white" : "bg-rose-500 ring-rose-300 text-white";
                else if (rezultat) stil = `${T.suprafataSlaba} ${T.conturSlab} opacity-50`;
                else if (varianta.id === ales) stil = `${T.suprafata} ring-amber-300`;
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
              <div className={`mt-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl p-4 ring-1 ${rezultat.corect ? "bg-emerald-500/15 ring-emerald-400/40" : "bg-rose-500/15 ring-rose-400/40"}`}>
                <p className="text-lg font-extrabold">{rezultat.corect ? "Corect!" : "Greșit. Vezi răspunsul corect la final."}</p>
                <button onClick={urmatoarea} className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>Următoarea →</button>
              </div>
            )}
          </>
        ) : (
          <p className={`py-10 text-center ${T.textSecundar}`}>Nu mai sunt întrebări în acest test.</p>
        )}
      </div>
    </Ecran>
  );
}

function Rezumat({ final, texte, inapoi, onReia }) {
  return (
    <>
      <div className={`rounded-2xl p-4 ring-1 ${final.promovat ? "bg-emerald-500/15 ring-emerald-400/40" : "bg-rose-500/15 ring-rose-400/40"}`}>
        <p className="text-lg font-extrabold">
          {final.promovat
            ? final.xpCastigat ? `Felicitări! ${final.corecte} din ${final.total} corecte. Ai câștigat ${final.xpCastigat} XP.` : `Felicitări! ${final.corecte} din ${final.total} corecte.`
            : `${final.corecte} din ${final.total} corecte. Ai nevoie de ${final.necesare} ca să treci lecția.`}
        </p>
        {!final.promovat && <p className={`mt-1 text-sm ${T.textSecundar}`}>Recitește materialele și reia testul peste un minut.</p>}
      </div>

      <ol className="mt-5 grid gap-3">
        {final.corectii.map((c, i) => {
          const q = texte[c.intrebareId];
          const text = (id) => q?.variante.find((v) => v.id === id)?.text;
          const bine = c.variantaAleasaId === c.variantaCorectaId;
          return (
            <li key={c.intrebareId} className={`rounded-2xl p-4 ring-1 ${T.suprafata} ${T.contur}`}>
              <p className="font-bold">{i + 1}. {q?.text ?? "Întrebare la care ai răspuns mai devreme"}</p>
              {q && (
                <p className={`mt-1 text-sm ${bine ? "text-emerald-400" : "text-rose-400"}`}>
                  {bine ? `✓ ${text(c.variantaCorectaId)}` : `✗ Ai ales: ${text(c.variantaAleasaId)} · Corect: ${text(c.variantaCorectaId)}`}
                </p>
              )}
              {c.explicatie && <p className={`mt-1 text-sm ${T.textSecundar}`}>{c.explicatie}</p>}
            </li>
          );
        })}
      </ol>

      <div className="mt-5 flex flex-wrap gap-2">
        <Link href={inapoi} className={`rounded-xl px-4 py-2 font-black ${T.accent}`}>Continuă pe hartă →</Link>
        <button onClick={onReia} className={`rounded-xl px-4 py-2 font-bold ${T.suprafata} ${T.suprafataHover}`}>Test nou</button>
      </div>
    </>
  );
}

function Ecran({ children }) {
  return (
    <main className={`flex min-h-screen items-center justify-center p-4 ${T.pagina}`}>
      <div className="w-full max-w-xl">{children}</div>
    </main>
  );
}
