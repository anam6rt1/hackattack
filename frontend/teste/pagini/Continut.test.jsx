import { render, screen, waitFor, within } from "@testing-library/react";
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

  it("lecția se arată și în alte organizații, alese într-o fereastră cu căutare", async () => {
    const posibile = [{ id: 1, nume: "Liceul de Test", poatePublicaPublic: true }, { id: 2, nume: "Centrul Alba", poatePublicaPublic: true },
      { id: 3, nume: "Școala Bărăbanț", poatePublicaPublic: true }];
    const l = { ...lectie(1, "Luceafărul"), organizatiiPosibile: posibile, partajari: [3] };
    const cereri = apiFals({
      "GET /utilizatori/me": { id: 2, nume: "Irina", rol: "profesor", stareProfesor: "aprobat", organizatii: [] },
      "GET /materii": { elemente: [{ id: 1, denumire: "Română" }] },
      "GET /profesori/me": { profil: { materii: [{ id: 1, denumire: "Română" }] } },
      "GET /continut/organizatii": { elemente: [] },
      "GET /continut/parcursuri": { elemente: [l], total: 1 },
      "GET /continut/cursuri": { elemente: [] },
      "GET /continut/parcursuri/1/fisiere": { elemente: [], maxMb: 10 },
      "GET /continut/parcursuri/1/intrebari": { elemente: [] },
      "GET /parcursuri/1/generare": { activ: false },
      "PUT /parcursuri/1": (c) => ({ ...l, partajari: c.corp.partajari }),
    });
    render(<SesiuneProvider><AdminContinut /></SesiuneProvider>);
    const u = userEvent.setup();
    await u.click(await screen.findByRole("button", { name: /Luceafărul/ }));
    // The lesson's own organisation is not offered; the existing share shows as a chip.
    expect(screen.getByRole("button", { name: "Scoate Școala Bărăbanț" })).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "+ Adaugă organizație" }));
    const fereastra = screen.getByRole("dialog", { name: "Arată lecția și în" });
    expect(within(fereastra).queryByRole("checkbox", { name: "Liceul de Test" })).not.toBeInTheDocument();
    await u.type(within(fereastra).getByRole("searchbox"), "centrul");
    await waitFor(() => expect(within(fereastra).queryByRole("checkbox", { name: /Bărăbanț/ })).not.toBeInTheDocument());
    await u.click(within(fereastra).getByRole("checkbox", { name: "Centrul Alba" }));
    await u.click(within(fereastra).getByRole("button", { name: "Gata" }));
    await u.click(screen.getByRole("button", { name: "Salvează lecția" }));
    await waitFor(() => expect(cereri.find((c) => c.metoda === "PUT" && c.cale === "/parcursuri/1")?.corp.partajari).toEqual([3, 2]));
  });
});
