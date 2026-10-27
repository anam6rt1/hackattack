import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import PaginaMaterie from "@/app/materiale/materie/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals, raspuns } from "../ajutor";

const lectie = (id, titlu, completata) => ({
  id, titlu, completata, clasa: "XII", descriere: null, vizibilitate: "public", publicat: true, autor: "Irina Marin", organizatie: null,
  loc: null, inLocalitateaTa: false, numarCapitole: 2,
});

const ELEV = { id: 1, nume: "Ana", rol: "elev", clasa: "XII", judetId: 40, localitateId: 179141, organizatii: [] };

function porneste({ utilizator = ELEV, ascunde = false, lectii = [lectie(1, "Luceafărul", true), lectie(2, "Plumb", false)] } = {}) {
  window.history.replaceState(null, "", "/materiale/materie?id=1");
  const cereri = apiFals({
    "GET /utilizatori/me": utilizator,
    "GET /materii/1": { id: 1, denumire: "Română", lectiiCompletate: 1, numarLectii: 2 },
    // Like the server: ascundeTerminate leaves out the finished ones; the total follows.
    "GET /materii/1/parcursuri": (c) => {
      const elemente = new URLSearchParams(c.query).get("ascundeTerminate") === "true" ? lectii.filter((l) => !l.completata) : lectii;
      return { elemente, total: elemente.length };
    },
    "GET /utilizatori/me/preferinte": { mod: "auto", tema: null, interfata: null, avatar: null, ascundeLectiiTerminate: ascunde },
    "PUT /utilizatori/me/preferinte": (c) => ({ mod: "auto", ascundeLectiiTerminate: c.corp.ascundeLectiiTerminate }),
  });
  render(<SesiuneProvider><PaginaMaterie /></SesiuneProvider>);
  return { u: userEvent.setup(), cereri, puneri: () => cereri.filter((c) => c.metoda === "PUT").map((c) => c.corp) };
}

describe("lecțiile unei materii: „Ascunde lecțiile terminate”", () => {
  it("implicit arată și lecțiile terminate", async () => {
    porneste();
    expect(await screen.findByRole("heading", { name: "Luceafărul" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Plumb" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Ascunde lecțiile terminate" })).not.toBeChecked();
  });

  it("alegerea salvată în cont e folosită la deschidere", async () => {
    porneste({ ascunde: true });
    expect(await screen.findByRole("heading", { name: "Plumb" })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("checkbox", { name: "Ascunde lecțiile terminate" })).toBeChecked());
    expect(screen.queryByRole("heading", { name: "Luceafărul" })).not.toBeInTheDocument();
  });

  it("bifarea ascunde lecțiile terminate și se salvează în cont", async () => {
    const { u, puneri } = porneste();
    await screen.findByRole("heading", { name: "Luceafărul" });
    await u.click(screen.getByRole("checkbox", { name: "Ascunde lecțiile terminate" }));
    expect(screen.queryByRole("heading", { name: "Luceafărul" })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Plumb" })).toBeInTheDocument();
    await waitFor(() => expect(puneri()).toEqual([{ ascundeLectiiTerminate: true }]));
    await u.click(screen.getByRole("checkbox", { name: "Ascunde lecțiile terminate" }));
    expect(screen.getByRole("heading", { name: "Luceafărul" })).toBeInTheDocument();
    await waitFor(() => expect(puneri()).toEqual([{ ascundeLectiiTerminate: true }, { ascundeLectiiTerminate: false }]));
  });

  it("când toate sunt terminate și ascunse, spune asta și le poate arăta din nou", async () => {
    const { u, puneri, cereri } = porneste({ ascunde: true, lectii: [lectie(1, "Luceafărul", true), lectie(2, "Plumb", true)] });
    expect(await screen.findByText(/Nicio lecție neterminată aici/)).toBeInTheDocument();
    expect(screen.queryByText("Materia nu are încă lecții.")).not.toBeInTheDocument();
    expect(new URLSearchParams(cereri.filter((c) => c.cale === "/materii/1/parcursuri").at(-1).query).get("ascundeTerminate")).toBe("true");
    await u.click(screen.getByRole("button", { name: "Arată și lecțiile terminate" }));
    expect(await screen.findByRole("heading", { name: "Luceafărul" })).toBeInTheDocument();
    await waitFor(() => expect(puneri()).toEqual([{ ascundeLectiiTerminate: false }]));
  });

  it("o materie fără lecții nu pretinde că le-ai terminat", async () => {
    porneste({ ascunde: false, lectii: [] });
    expect(await screen.findByText(/Nu există lecții pentru clasa aleasă/)).toBeInTheDocument();
    expect(screen.queryByText(/neterminată/)).not.toBeInTheDocument();
  });

  it("administratorul nu are comutatorul și nu i se cer preferințele", async () => {
    const { cereri } = porneste({ utilizator: { id: 9, nume: "Admin", rol: "admin", organizatii: [] } });
    expect(await screen.findByRole("heading", { name: "Luceafărul" })).toBeInTheDocument();
    expect(screen.queryByRole("checkbox", { name: "Ascunde lecțiile terminate" })).not.toBeInTheDocument();
    expect(cereri.some((c) => c.cale === "/utilizatori/me/preferinte")).toBe(false);
  });

  it("dacă salvarea în cont nu merge, pagina rămâne folosibilă", async () => {
    window.history.replaceState(null, "", "/materiale/materie?id=1");
    apiFals({
      "GET /utilizatori/me": ELEV,
      "GET /materii/1": { id: 1, denumire: "Română", lectiiCompletate: 1, numarLectii: 2 },
      "GET /materii/1/parcursuri": (c) => {
        const toate = [lectie(1, "Luceafărul", true), lectie(2, "Plumb", false)];
        const elemente = new URLSearchParams(c.query).get("ascundeTerminate") === "true" ? toate.filter((l) => !l.completata) : toate;
        return { elemente, total: elemente.length };
      },
      "GET /utilizatori/me/preferinte": { mod: "auto", ascundeLectiiTerminate: false },
      "PUT /utilizatori/me/preferinte": raspuns(500, { mesaj: "Eroare." }),
    });
    const u = userEvent.setup();
    render(<SesiuneProvider><PaginaMaterie /></SesiuneProvider>);
    await u.click(await screen.findByRole("checkbox", { name: "Ascunde lecțiile terminate" }));
    await waitFor(() => expect(screen.queryByRole("heading", { name: "Luceafărul" })).not.toBeInTheDocument());
    expect(screen.getByRole("heading", { name: "Plumb" })).toBeInTheDocument();
  });
});

describe("lecțiile unei materii: căutarea", () => {
  it("trimite textul cu filtrele, de la prima pagină, și poate fi ștearsă când nu găsește nimic", async () => {
    const { u, cereri } = porneste();
    await screen.findByRole("heading", { name: "Luceafărul" });
    await u.type(screen.getByLabelText("Caută în Română"), "zz");
    await waitFor(() => expect(new URLSearchParams(cereri.filter((c) => c.cale === "/materii/1/parcursuri").at(-1).query).get("q")).toBe("zz"));
    const q = new URLSearchParams(cereri.filter((c) => c.cale === "/materii/1/parcursuri").at(-1).query);
    expect(q.get("clasa")).toBe("XII,Bac"); expect(q.get("pagina")).toBeNull();
  });

  it("fără rezultate spune ce s-a căutat și oferă ștergerea căutării", async () => {
    window.history.replaceState(null, "", "/materiale/materie?id=1");
    apiFals({
      "GET /utilizatori/me": ELEV,
      "GET /materii/1": { id: 1, denumire: "Română", lectiiCompletate: 0, numarLectii: 1 },
      "GET /materii/1/parcursuri": (c) => (new URLSearchParams(c.query).get("q") ? { elemente: [], total: 0 } : { elemente: [lectie(1, "Plumb", false)], total: 1 }),
      "GET /utilizatori/me/preferinte": { ascundeLectiiTerminate: false },
    });
    render(<SesiuneProvider><PaginaMaterie /></SesiuneProvider>);
    const u = userEvent.setup();
    await screen.findByRole("heading", { name: "Plumb" });
    await u.type(screen.getByLabelText("Caută în Română"), "zz");
    expect(await screen.findByText(/Nicio lecție pentru „zz” cu filtrele alese/)).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Șterge căutarea" }));
    expect(await screen.findByRole("heading", { name: "Plumb" })).toBeInTheDocument();
    expect(screen.getByLabelText("Caută în Română")).toHaveValue("");
  });
});
