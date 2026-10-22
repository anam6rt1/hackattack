import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import PaginaProfesori from "@/app/profesori/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals } from "../ajutor";

const MAINE = new Date(Date.now() + 86400000).toISOString();

const sedinta = (id, extra = {}) => ({
  id, titlu: `Ședința ${id}`, materie: "Română", inceputLa: MAINE, tip: "online", loc: null, notaLoc: null, capacitate: 5, locuriLibere: 3,
  proBono: false, rezervatDeMine: false, linkIntalnire: null, ...extra,
});

const IRINA = {
  id: 2, nume: "Irina Marin", descriere: "Profesoară de română.", pozaUrl: null, materii: [{ id: 1, denumire: "Română" }], oferaProBono: false,
  loc: { organizatie: "Liceul de Test", localitate: "Sectorul 1", judet: "București" }, inLocalitateaTa: true,
  sloturi: [sedinta(10), sedinta(11, { rezervatDeMine: true, linkIntalnire: "https://meet.example.invalid/x" })],
};

const ELEV = { id: 1, nume: "Ana", rol: "elev", judetId: 40, localitateId: 179141, organizatii: [{ id: 1, nume: "Liceul de Test", rol: "elev", stare: "aprobat" }] };

function porneste(utilizator = ELEV, profesori = [IRINA]) {
  const cereri = apiFals({
    "GET /utilizatori/me": utilizator,
    "GET /profesori": (c) => (typeof profesori === "function" ? profesori(c) : { elemente: profesori }),
    "GET /materii": { elemente: [{ id: 1, denumire: "Română" }] },
    "GET /tarif": { suma: 80, moneda: "RON", durataMinute: 60 },
    "GET /rezervari": { elemente: [{ id: 99, status: "confirmata", slot: { id: 11 } }] },
  });
  render(<SesiuneProvider><PaginaProfesori /></SesiuneProvider>);
  return { u: userEvent.setup(), cereri, interogari: () => cereri.filter((c) => c.cale === "/profesori").map((c) => new URLSearchParams(c.query)) };
}

describe("pagina Profesori (elev)", () => {
  it("arată profesorii cu ședințele lor", async () => {
    porneste();
    expect(await screen.findByRole("heading", { name: "Irina Marin" })).toBeInTheDocument();
    expect(screen.getByText("Ședința 10")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rezervă" })).toBeEnabled();
  });

  it("la o ședință rezervată nu mai e „Intră în ședință” (e pe pagina de start)", async () => {
    porneste();
    const rand = (await screen.findByText("Ședința 11")).closest("li");
    expect(within(rand).getByText("Rezervat")).toBeInTheDocument();
    expect(within(rand).queryByRole("link", { name: /Intră în ședință/ })).not.toBeInTheDocument();
    expect(within(rand).getByRole("button", { name: /Anulează/ })).toBeInTheDocument();
  });

  it("căutarea trimite textul după o scurtă pauză, o singură dată", async () => {
    const { u, interogari } = porneste();
    await screen.findByRole("heading", { name: "Irina Marin" });
    await u.type(screen.getByRole("searchbox", { name: "Caută un profesor" }), "  irina ");
    await waitFor(() => expect(interogari().at(-1).get("q")).toBe("irina"));
    expect(interogari().filter((q) => q.has("q"))).toHaveLength(1);
  });

  it("căutarea se combină cu filtrele", async () => {
    const { u, interogari } = porneste();
    await screen.findByRole("heading", { name: "Irina Marin" });
    await u.click(screen.getByRole("button", { name: "Aproape de mine" }));
    await u.type(screen.getByRole("searchbox"), "mate");
    await waitFor(() => expect(interogari().at(-1).toString()).toBe("filtru=local&q=mate"));
  });

  it("fără localitate „Aproape de mine” e oprit și trimite la Setări", async () => {
    porneste({ ...ELEV, localitateId: null });
    const buton = await screen.findByRole("button", { name: "Aproape de mine" });
    expect(buton).toBeDisabled();
    expect(screen.getByRole("link", { name: /Alege-ți localitatea în Setări/ })).toHaveAttribute("href", "/setari");
  });

  it("doar județul nu ajunge pentru „Aproape de mine”", async () => {
    porneste({ ...ELEV, judetId: 40, localitateId: null });
    expect(await screen.findByRole("button", { name: "Aproape de mine" })).toBeDisabled();
  });

  it("fără o organizație aprobată „Din organizația mea” e oprit", async () => {
    porneste({ ...ELEV, organizatii: [{ id: 1, nume: "Liceul de Test", rol: "elev", stare: "in_asteptare" }] });
    expect(await screen.findByRole("button", { name: "Din organizația mea" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Toți profesorii" })).toBeEnabled();
  });

  it.each([
    ["Aproape de mine", "", "Niciun profesor din localitatea ta nu are ședințe programate momentan."],
    ["Din organizația mea", "", "Niciun profesor din organizația ta nu are ședințe programate momentan."],
    ["Toți profesorii", "", "Niciun profesor nu are ședințe programate momentan."],
    ["Toți profesorii", "zzz", "Niciun profesor cu ședințe programate nu se potrivește căutării „zzz”."],
    ["Aproape de mine", "zzz", "Niciun profesor din localitatea ta cu ședințe programate nu se potrivește căutării „zzz”."],
  ])("lista goală la „%s” %s", async (filtru, text, mesaj) => {
    const { u } = porneste(ELEV, []);
    await u.click(await screen.findByRole("button", { name: filtru }));
    if (text) await u.type(screen.getByRole("searchbox"), text);
    expect(await screen.findByText(mesaj)).toBeInTheDocument();
  });

  it("lista goală cu o materie aleasă vorbește de filtre", async () => {
    const { u } = porneste(ELEV, []);
    await screen.findByRole("option", { name: "Română" });
    await u.selectOptions(screen.getByRole("combobox", { name: "Materie" }), "1");
    expect(await screen.findByText("Niciun profesor nu are ședințe programate care să se potrivească filtrelor momentan.")).toBeInTheDocument();
    await u.type(screen.getByRole("searchbox"), "ion");
    expect(await screen.findByText("Niciun profesor cu ședințe programate nu se potrivește căutării „ion” și filtrelor alese.")).toBeInTheDocument();
  });
});
