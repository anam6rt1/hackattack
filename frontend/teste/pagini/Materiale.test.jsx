import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import PaginaMateriale from "@/app/materiale/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { MATERII_PE_PAGINA_MATERIALE_ADMIN, MATERII_PE_PAGINA_MATERIALE_ELEV } from "@/lib/pagini";
import { apiFals } from "../ajutor";

const materie = (id) => ({ id, cod: `m${id}`, denumire: `Materia ${id}`, descriere: null, ordine: id, numarLectii: 4, lectiiCompletate: 1 });

function porneste(rol = "elev") {
  const cereri = apiFals({
    "GET /utilizatori/me": { id: 1, nume: "Ana", rol, organizatii: [] },
    "GET /materii": (c) => {
      const q = new URLSearchParams(c.query);
      const pagina = Number(q.get("pagina") ?? 1), marime = Number(q.get("marime"));
      return { elemente: Array.from({ length: 25 }, (_, i) => materie(i + 1)).slice((pagina - 1) * marime, pagina * marime), total: 25 };
    },
  });
  render(<SesiuneProvider><PaginaMateriale /></SesiuneProvider>);
  return { u: userEvent.setup(), interogari: () => cereri.filter((c) => c.cale === "/materii").map((c) => new URLSearchParams(c.query)) };
}

describe("Materiale: lista materiilor pe pagini", () => {
  it("elevul vede o pagină cu mărimea lui și bara de pagini", async () => {
    const { interogari } = porneste();
    expect(await screen.findByRole("heading", { name: "Materia 1" })).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 3 })).toHaveLength(MATERII_PE_PAGINA_MATERIALE_ELEV);
    expect(interogari().at(-1).get("marime")).toBe(String(MATERII_PE_PAGINA_MATERIALE_ELEV));
    expect(screen.getByRole("navigation", { name: "Pagini" })).toBeInTheDocument();
  });

  it("pagina următoare aduce materiile ei", async () => {
    const { u, interogari } = porneste();
    await u.click(await screen.findByRole("button", { name: "Pagina 2" }));
    expect(await screen.findByRole("heading", { name: `Materia ${MATERII_PE_PAGINA_MATERIALE_ELEV + 1}` })).toBeInTheDocument();
    await waitFor(() => expect(interogari().at(-1).get("pagina")).toBe("2"));
  });

  it("adminul are constanta lui", async () => {
    const { interogari } = porneste("admin");
    await screen.findByRole("heading", { name: "Materia 1" });
    expect(interogari().at(-1).get("marime")).toBe(String(MATERII_PE_PAGINA_MATERIALE_ADMIN));
  });
});

describe("Materiale: căutarea pe pagini", () => {
  const lectieGasita = (id) => ({ id, titlu: `Lecția ${id}`, clasa: "XII", descriere: null, vizibilitate: "public", autor: "Irina", organizatie: null });
  const capitolGasit = (id) => ({ id, titlu: `Capitolul ${id}`, capitol: null, parcursId: 1, parcurs: "Lecția 1", clasa: "XII", materieId: 1, materie: "Română" });

  it("arată câte 9 lecții pe pagină, capitolele separat, iar un text nou pornește de la prima pagină", async () => {
    const cereri = apiFals({
      "GET /utilizatori/me": { id: 1, nume: "Ana", rol: "elev", organizatii: [] },
      "GET /materii": { elemente: [materie(1)], total: 1 },
      "GET /cautare": (c) => {
        const q = new URLSearchParams(c.query);
        const p = Number(q.get("pagina") ?? 1), m = Number(q.get("marime"));
        const pc = Number(q.get("paginaCapitole") ?? 1), mc = Number(q.get("marimeCapitole"));
        const lectii = Array.from({ length: 20 }, (_, i) => lectieGasita(i + 1)), capitole = Array.from({ length: 12 }, (_, i) => capitolGasit(i + 1));
        return { lectii: lectii.slice((p - 1) * m, p * m), capitole: capitole.slice((pc - 1) * mc, pc * mc), totalLectii: 20, totalCapitole: 12 };
      },
    });
    render(<SesiuneProvider><PaginaMateriale /></SesiuneProvider>);
    const u = userEvent.setup();
    await u.type(await screen.findByLabelText("Caută o lecție sau un capitol"), "lec");
    expect(await screen.findByRole("heading", { name: "Lecția 1" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "20 lecții" })).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { name: /^Lecția \d+$/ })).toHaveLength(9);
    expect(screen.getAllByRole("link", { name: /Capitolul/ })).toHaveLength(10);

    expect(screen.getByRole("navigation", { name: "Paginile capitolelor găsite" })).toBeInTheDocument();
    await u.click(within(screen.getByRole("navigation", { name: "Paginile lecțiilor găsite" })).getByRole("button", { name: "Pagina 3" }));
    expect(await screen.findByRole("heading", { name: "Lecția 19" })).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { name: /^Lecția \d+$/ })).toHaveLength(2);

    await u.type(screen.getByLabelText("Caută o lecție sau un capitol"), "t");
    await waitFor(() => expect(screen.getByRole("heading", { name: "Lecția 1" })).toBeInTheDocument());
    const ultima = new URLSearchParams(cereri.filter((c) => c.cale === "/cautare").at(-1).query);
    expect(ultima.get("q")).toBe("lect"); expect(ultima.get("pagina")).toBeNull(); expect(ultima.get("marime")).toBe("9");
  });
});
