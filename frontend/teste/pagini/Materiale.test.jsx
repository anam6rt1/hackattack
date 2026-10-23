import { render, screen, waitFor } from "@testing-library/react";
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
