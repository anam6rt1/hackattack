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

function porneste(utilizator = ELEV, profesori = [IRINA], rute = {}) {
  const cereri = apiFals({
    ...rute,
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
  it("cere fotografia mică fără schimbarea dimensiunilor cardului", async () => {
    porneste(ELEV, [{ ...IRINA, pozaUrl: "/profesori/2/poza" }]);
    await screen.findByRole("heading", { name: "Irina Marin" });
    const imagine = document.querySelector("img.tv-poza");
    expect(imagine).toHaveAttribute("src", expect.stringContaining("/profesori/2/poza?marime=mica"));
    expect(imagine).toHaveAttribute("width", "64");
    expect(imagine).toHaveAttribute("height", "64");
    expect(imagine).toHaveAttribute("loading", "lazy");
  });

  it("arată profesorii cu ședințele lor", async () => {
    porneste();
    expect(await screen.findByRole("heading", { name: "Irina Marin" })).toBeInTheDocument();
    expect(screen.getByText("Ședința 10")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Rezervă" })).toBeEnabled();
  });

  it("arată doar următoarea ședință; „și alte 2 sesiuni” le deschide pe toate într-o fereastră, de unde se poate rezerva", async () => {
    const { u } = porneste(ELEV, [{ ...IRINA, sloturi: [sedinta(10)], alteSesiuni: 2 }], {
      "GET /profesori/2": { ...IRINA, sloturi: [sedinta(10), sedinta(12), sedinta(13)], alteSesiuni: 0 },
    });
    expect(await screen.findByText("Ședința 10")).toBeInTheDocument();
    expect(screen.queryByText("Ședința 12")).not.toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "și alte 2 sesiuni" }));
    const fereastra = await screen.findByRole("dialog", { name: "Ședințele lui Irina Marin" });
    expect(await within(fereastra).findByText("Ședința 13")).toBeInTheDocument();
    expect(within(fereastra).getByText("3 ședințe viitoare")).toBeInTheDocument();
    // The card itself stays short.
    expect(screen.getAllByText("Ședința 12")).toHaveLength(1);
    await u.click(within(fereastra).getAllByRole("button", { name: "Rezervă" })[2]);
    expect(await screen.findByRole("dialog", { name: "" })).toHaveTextContent("Ședința 13");
    // Escape closes the booking first, then the list.
    await u.keyboard("{Escape}");
    expect(screen.getByRole("dialog", { name: "Ședințele lui Irina Marin" })).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Renunț" }));
    await u.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("celelalte sesiuni respectă filtrul online / față în față", async () => {
    const { u, cereri } = porneste(ELEV, [{ ...IRINA, sloturi: [sedinta(10)], alteSesiuni: 1 }], {
      "GET /profesori/2": { ...IRINA, sloturi: [sedinta(10), sedinta(12, { tip: "hibrid" })] },
    });
    await screen.findByText("Ședința 10");
    await u.click(screen.getByRole("button", { name: "Față în față" }));
    await u.click(await screen.findByRole("button", { name: "și încă o sesiune" }));
    await screen.findByText("Ședința 12");
    expect(cereri.filter((c) => c.cale === "/profesori/2").at(-1).query).toBe("?tip=fata_in_fata");
  });

  it("fără alte sesiuni nu apare butonul", async () => {
    porneste(ELEV, [{ ...IRINA, sloturi: [sedinta(10)], alteSesiuni: 0 }]);
    await screen.findByText("Ședința 10");
    expect(screen.queryByRole("button", { name: /și (alte|încă)/ })).not.toBeInTheDocument();
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
    await waitFor(() => expect(interogari().at(-1).toString()).toBe("filtru=local&q=mate&marime=6"));
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

  it("ștergerea textului din căutare scoate filtrul", async () => {
    const { u, interogari } = porneste();
    const cauta = await screen.findByRole("searchbox", { name: "Caută un profesor" });
    await u.type(cauta, "irina");
    await waitFor(() => expect(interogari().at(-1).get("q")).toBe("irina"));
    await u.clear(cauta);
    await waitFor(() => expect(interogari().at(-1).has("q")).toBe(false));
  });

  it("doar spații nu pornesc o căutare", async () => {
    const { u, interogari } = porneste();
    await u.type(await screen.findByRole("searchbox"), "    ");
    await new Promise((r) => setTimeout(r, 400));
    expect(interogari().some((q) => q.has("q"))).toBe(false);
  });

  it("bara de pagini cere pagina aleasă și arată doar profesorii ei", async () => {
    const pe = (start, n) => Array.from({ length: n }, (_, i) => ({ ...IRINA, id: start + i, nume: `Profesor ${start + i}`, sloturi: [sedinta(100 + start + i)] }));
    const { u, interogari } = porneste(ELEV, (c) => (new URLSearchParams(c.query).get("pagina") === "2"
      ? { elemente: pe(41, 5), total: 45 } : { elemente: pe(1, 20), total: 45 }));
    expect(await screen.findByText("45 profesori")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pagina 1" })).toHaveAttribute("aria-current", "page");
    await u.click(screen.getByRole("button", { name: "Pagina 2" }));
    expect(await screen.findByRole("heading", { name: "Profesor 41" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Profesor 1" })).not.toBeInTheDocument();
    expect(interogari().at(-1).get("pagina")).toBe("2");
    expect(interogari().at(-1).get("marime")).toBe("6");
    expect(screen.getByRole("button", { name: "Pagina 2" })).toHaveAttribute("aria-current", "page");
  });

  it("o singură pagină nu are bară", async () => {
    porneste();
    await screen.findByRole("heading", { name: "Irina Marin" });
    expect(screen.queryByRole("navigation", { name: "Pagini" })).not.toBeInTheDocument();
  });

  it("o schimbare de filtru pornește de la prima pagină", async () => {
    const { u, interogari } = porneste(ELEV, () => ({ elemente: [IRINA], total: 60 }));
    await u.click(await screen.findByRole("button", { name: "Pagina 2" }));
    await waitFor(() => expect(interogari().at(-1).get("pagina")).toBe("2"));
    await u.click(screen.getByRole("button", { name: "Online" }));
    await waitFor(() => expect(interogari().at(-1).toString()).toBe("tip=online&marime=6"));
  });
});
