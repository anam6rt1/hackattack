import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import IntrebareNivel from "@/components/IntrebareNivel";
import { apiFals, raspuns } from "../ajutor";

// A button's name is its letter, a space and the option's text ("A Mihai Eminescu").
const v = (litera, text) => `${litera} ${text}`;

const LECTIE = { id: 5, titlu: "Luceafărul", materie: "Limba și literatura română", progres: null };
const intrebare = (id, text, variante) => ({ id, text, variante: variante.map((t, i) => ({ id: id * 10 + i, text: t })) });
const TEST = {
  id: 77, total: 3, necesare: 3, raspunse: 0,
  intrebari: [
    intrebare(1, "Cine a scris „Luceafărul”?", ["Mihai Eminescu", "Ion Creangă", "George Coșbuc", "Lucian Blaga"]),
    intrebare(2, "În ce an a apărut?", ["1883", "1920"]),
    intrebare(3, "Cum se numește fata de împărat?", ["Cătălina", "Ileana", "Ana"]),
  ],
};

function porneste({ test = TEST, lectie = LECTIE, raspunsuri = {} } = {}) {
  const cereri = apiFals({
    "GET /parcursuri/5": lectie,
    "POST /parcursuri/5/quiz": test,
    "POST /parcursuri/5/quiz/77/raspuns": (c) => raspunsuri[c.corp.intrebareId] ?? { corect: true, ramase: 2, final: null },
  });
  const u = userEvent.setup();
  render(<IntrebareNivel parcursId={5} />);
  return { u, cereri };
}

const FINAL_PICAT = {
  promovat: false, corecte: 1, total: 3, necesare: 3, xpCastigat: 0, promovatLa: null,
  corectii: [
    { intrebareId: 1, variantaAleasaId: 10, variantaCorectaId: 10, explicatie: "Poemul e al lui Eminescu." },
    { intrebareId: 2, variantaAleasaId: 21, variantaCorectaId: 20, explicatie: null },
    { intrebareId: 3, variantaAleasaId: 31, variantaCorectaId: 30, explicatie: "Cătălina." },
  ],
};

describe("testul lecției", () => {
  it("arată lecția, prima întrebare și ce trebuie pentru a trece", async () => {
    porneste();
    expect(await screen.findByText("Cine a scris „Luceafărul”?")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Luceafărul" })).toBeInTheDocument();
    expect(screen.getByText("Întrebarea 1 din 3 · ai nevoie de 3 răspunsuri corecte")).toBeInTheDocument();
    expect(screen.getByText("+100 XP")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /^[A-D] / })).toHaveLength(4);
  });

  it("variantele au litere A, B, C, D", async () => {
    porneste();
    expect(await screen.findByRole("button", { name: v("A", "Mihai Eminescu") })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: v("D", "Lucian Blaga") })).toBeInTheDocument();
  });

  it("un răspuns bun e verde; celelalte variante se blochează", async () => {
    const { u, cereri } = porneste({ raspunsuri: { 1: { corect: true, ramase: 2, final: null } } });
    await u.click(await screen.findByRole("button", { name: v("A", "Mihai Eminescu") }));
    expect(await screen.findByText("Corect!")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: v("A", "Mihai Eminescu") })).toHaveClass("bg-emerald-500");
    expect(screen.getByRole("button", { name: v("B", "Ion Creangă") })).toBeDisabled();
    expect(cereri.at(-1).corp).toEqual({ intrebareId: 1, variantaId: 10 });
  });

  it("un răspuns greșit e roșu și nu arată varianta corectă", async () => {
    const { u } = porneste({ raspunsuri: { 1: { corect: false, ramase: 2, final: null } } });
    await u.click(await screen.findByRole("button", { name: v("B", "Ion Creangă") }));
    expect(await screen.findByText("Greșit. Vezi răspunsul corect la final.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: v("B", "Ion Creangă") })).toHaveClass("bg-rose-500");
    expect(screen.getByRole("button", { name: v("A", "Mihai Eminescu") })).not.toHaveClass("bg-emerald-500");
  });

  it("al doilea clic nu mai trimite nimic", async () => {
    const { u, cereri } = porneste();
    await u.click(await screen.findByRole("button", { name: v("A", "Mihai Eminescu") }));
    await screen.findByText("Corect!");
    await u.click(screen.getByRole("button", { name: v("C", "George Coșbuc") }));
    expect(cereri.filter((c) => c.cale.endsWith("/raspuns"))).toHaveLength(1);
  });

  it("„Următoarea” trece la întrebarea următoare", async () => {
    const { u } = porneste();
    await u.click(await screen.findByRole("button", { name: v("A", "Mihai Eminescu") }));
    await u.click(await screen.findByRole("button", { name: "Următoarea →" }));
    expect(screen.getByText("În ce an a apărut?")).toBeInTheDocument();
    expect(screen.getByText(/Întrebarea 2 din 3/)).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /^[AB] \d/ })).toHaveLength(2);
  });

  it("un test reluat numără și întrebările deja răspunse", async () => {
    porneste({ test: { ...TEST, raspunse: 2, intrebari: [TEST.intrebari[2]] } });
    expect(await screen.findByText(/Întrebarea 3 din 3/)).toBeInTheDocument();
  });

  async function pana_la_final(u, final) {
    await u.click(await screen.findByRole("button", { name: v("A", "Mihai Eminescu") }));
    await u.click(await screen.findByRole("button", { name: "Următoarea →" }));
    await u.click(screen.getByRole("button", { name: v("B", "1920") }));
    await u.click(await screen.findByRole("button", { name: "Următoarea →" }));
    await u.click(screen.getByRole("button", { name: v("B", "Ileana") }));
    return final;
  }

  it("la final, picat: corecte din total, câte trebuiau și corecțiile", async () => {
    const { u } = porneste({ raspunsuri: { 3: { corect: false, ramase: 0, final: FINAL_PICAT } } });
    await pana_la_final(u);
    expect(await screen.findByText("1 din 3 corecte. Ai nevoie de 3 ca să treci lecția.")).toBeInTheDocument();
    expect(screen.getByText("Recitește materialele și reia testul peste un minut.")).toBeInTheDocument();
    expect(screen.getByText("✓ Mihai Eminescu")).toBeInTheDocument();
    expect(screen.getByText("✗ Ai ales: 1920 · Corect: 1883")).toBeInTheDocument();
    expect(screen.getByText("✗ Ai ales: Ileana · Corect: Cătălina")).toBeInTheDocument();
    expect(screen.getByText("Poemul e al lui Eminescu.")).toBeInTheDocument();
  });

  it("la final, trecut prima dată: XP-ul câștigat", async () => {
    const final = { ...FINAL_PICAT, promovat: true, corecte: 3, xpCastigat: 100 };
    const { u } = porneste({ raspunsuri: { 3: { corect: true, ramase: 0, final } } });
    await pana_la_final(u);
    expect(await screen.findByText("Felicitări! 3 din 3 corecte. Ai câștigat 100 XP.")).toBeInTheDocument();
    expect(screen.queryByText(/Recitește materialele/)).not.toBeInTheDocument();
  });

  it("la final, trecut din nou: fără XP", async () => {
    const final = { ...FINAL_PICAT, promovat: true, corecte: 3, xpCastigat: 0 };
    const { u } = porneste({ raspunsuri: { 3: { corect: true, ramase: 0, final } } });
    await pana_la_final(u);
    expect(await screen.findByText("Felicitări! 3 din 3 corecte.")).toBeInTheDocument();
  });

  it("o întrebare din altă sesiune a testului are un text de rezervă", async () => {
    const final = { ...FINAL_PICAT, corectii: [{ intrebareId: 99, variantaAleasaId: 1, variantaCorectaId: 2, explicatie: null }, ...FINAL_PICAT.corectii] };
    const { u } = porneste({ raspunsuri: { 3: { corect: false, ramase: 0, final } } });
    await pana_la_final(u);
    expect(await screen.findByText("1. Întrebare la care ai răspuns mai devreme")).toBeInTheDocument();
  });

  it("„Test nou” cere un test nou", async () => {
    const { u, cereri } = porneste({ raspunsuri: { 3: { corect: false, ramase: 0, final: FINAL_PICAT } } });
    await pana_la_final(u);
    await u.click(await screen.findByRole("button", { name: "Test nou" }));
    await waitFor(() => expect(cereri.filter((c) => c.cale === "/parcursuri/5/quiz")).toHaveLength(2));
  });

  it("fără întrebări arată mesajul serverului și „Încearcă din nou”", async () => {
    apiFals({ "GET /parcursuri/5": LECTIE, "POST /parcursuri/5/quiz": raspuns(409, { mesaj: "Lecția nu are încă întrebări." }) });
    render(<IntrebareNivel parcursId={5} />);
    expect(await screen.findByText("Lecția nu are încă întrebări.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Încearcă din nou" })).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: /Înapoi la lecție/ })[0]).toHaveAttribute("href", "/parcurs?id=5");
  });

  it("pauza dintre teste e explicată", async () => {
    apiFals({ "GET /parcursuri/5": LECTIE, "POST /parcursuri/5/quiz": raspuns(409, { mesaj: "Poți începe un test nou peste 42 secunde." }) });
    render(<IntrebareNivel parcursId={5} />);
    expect(await screen.findByText("Poți începe un test nou peste 42 secunde.")).toBeInTheDocument();
  });

  it("o eroare la răspuns lasă alegerea din nou", async () => {
    const { u } = porneste({ raspunsuri: { 1: raspuns(409, { mesaj: "Testul s-a încheiat. Începe unul nou." }) } });
    await u.click(await screen.findByRole("button", { name: v("A", "Mihai Eminescu") }));
    expect(await screen.findByText("Testul s-a încheiat. Începe unul nou.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: v("A", "Mihai Eminescu") })).toBeEnabled();
  });

  it("lecția deja trecută e o recapitulare", async () => {
    porneste({ lectie: { ...LECTIE, progres: { promovatLa: "2026-10-20T10:00:00Z" } } });
    expect(await screen.findByText("Recapitulare")).toBeInTheDocument();
  });

  it("până răspunde serverul spune că se pregătește", () => {
    apiFals({ "GET /parcursuri/5": () => new Promise(() => {}), "POST /parcursuri/5/quiz": () => new Promise(() => {}) });
    render(<IntrebareNivel parcursId={5} />);
    expect(screen.getByText("Se pregătește testul…")).toBeInTheDocument();
  });

  it("întrebările cu 6 variante primesc literele până la F", async () => {
    porneste({ test: { ...TEST, total: 1, intrebari: [intrebare(1, "Șase?", ["a", "b", "c", "d", "e", "f"])] } });
    expect(await screen.findByRole("button", { name: v("F", "f") })).toBeInTheDocument();
  });
});
