import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import AdminContinut from "@/app/admin/continut/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { LECTII_PE_PAGINA_EDITOR_ADMIN, LECTII_PE_PAGINA_EDITOR_PROFESOR } from "@/lib/pagini";
import { apiFals } from "../ajutor";

const lectie = (id, titlu) => ({
  id, materieId: 1, materie: "Română", titlu, descriere: null, clasa: "XII", vizibilitate: "public", publicat: true, ordine: id, autorId: 2, autor: "Irina",
  organizatieId: 1, organizatie: "Liceul de Test", partajari: [], numarCapitole: 1, numarIntrebari: 3, poatePublicaPublic: true, poateMuta: true, organizatiiPosibile: [],
});

function porneste(utilizator = { id: 2, nume: "Irina", rol: "profesor", stareProfesor: "aprobat", organizatii: [] }, raspuns = () => ({ elemente: [lectie(1, "Luceafărul")], total: 1 })) {
  const cereri = apiFals({
    "GET /utilizatori/me": utilizator,
    "GET /materii": { elemente: [{ id: 1, denumire: "Română" }, { id: 2, denumire: "Matematică" }] },
    "GET /profesori/me": { profil: { materii: [{ id: 1, denumire: "Română" }] } },
    "GET /continut/organizatii": { elemente: [] },
    "GET /continut/parcursuri": raspuns,
  });
  render(<SesiuneProvider><AdminContinut /></SesiuneProvider>);
  return { u: userEvent.setup(), interogari: () => cereri.filter((c) => c.cale === "/continut/parcursuri").map((c) => new URLSearchParams(c.query)) };
}

describe("Conținut: căutarea și filtrele lecțiilor", () => {
  it("profesorul cere mărimea lui de pagină, adminul pe a lui", async () => {
    const { interogari } = porneste();
    await screen.findByText("Luceafărul");
    expect(interogari().at(-1).get("marime")).toBe(String(LECTII_PE_PAGINA_EDITOR_PROFESOR));
  });

  it("adminul are constanta lui", async () => {
    const { interogari } = porneste({ id: 9, nume: "Admin", rol: "admin", organizatii: [] });
    await screen.findByText("Luceafărul");
    expect(interogari().at(-1).get("marime")).toBe(String(LECTII_PE_PAGINA_EDITOR_ADMIN));
  });

  it("căutarea, clasa, vizibilitatea și ciornele ajung la server", async () => {
    const { u, interogari } = porneste();
    await screen.findByText("Luceafărul");
    await u.type(screen.getByRole("searchbox", { name: "Caută o lecție" }), "luceafar");
    await waitFor(() => expect(interogari().at(-1).get("q")).toBe("luceafar"));
    await u.selectOptions(screen.getByRole("combobox", { name: "Clasa" }), "XII");
    await u.selectOptions(screen.getByRole("combobox", { name: "Cine o vede" }), "organizatie");
    await u.click(screen.getByRole("checkbox", { name: "Doar ciornele" }));
    await waitFor(() => expect(interogari().at(-1).get("publicat")).toBe("false"));
    const ultima = interogari().at(-1);
    expect([ultima.get("q"), ultima.get("clasa"), ultima.get("vizibilitate")]).toEqual(["luceafar", "XII", "organizatie"]);
  });

  it("materia aleasă filtrează pe server; un filtru nou pornește de la prima pagină", async () => {
    const { u, interogari } = porneste(undefined, () => ({ elemente: [lectie(1, "Luceafărul")], total: 50 }));
    await u.click(await screen.findByRole("button", { name: "Pagina 2" }));
    await waitFor(() => expect(interogari().at(-1).get("pagina")).toBe("2"));
    await u.selectOptions(screen.getByRole("combobox", { name: "Materia" }), "1");
    await waitFor(() => expect(interogari().at(-1).get("materieId")).toBe("1"));
    expect(interogari().at(-1).has("pagina")).toBe(false);
  });

  it("lista goală cu filtre spune că nimic nu se potrivește", async () => {
    const { u } = porneste(undefined, () => ({ elemente: [], total: 0 }));
    expect(await screen.findByText("Nu ai încă lecții aici.")).toBeInTheDocument();
    await u.click(screen.getByRole("checkbox", { name: "Doar ciornele" }));
    expect(await screen.findByText("Nicio lecție nu se potrivește căutării și filtrelor.")).toBeInTheDocument();
  });
});
