import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import PaginaOrganizatie from "@/app/organizatie/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals, raspuns } from "../ajutor";

const membru = (id, nume, extra = {}) => ({
  utilizatorId: id, nume, username: nume.toLowerCase().split(" ")[0], email: `${id}@example.invalid`, rol: "profesor", stare: "aprobat",
  cerutLa: "2026-10-01T10:00:00Z", expiraLa: null, poatePublicaPublic: true, clasa: null, descriere: null, areDovada: false, materii: ["Română"],
  profesorRetras: false, ...extra,
});

function porneste(membri = [membru(2, "Irina Marin"), membru(3, "Radu Șerban", { profesorRetras: true, materii: ["Matematică"] })]) {
  const cereri = apiFals({
    "GET /utilizatori/me": { id: 1, nume: "Dana", rol: "organizatie", stareProfesor: "aprobat", organizatii: [] },
    "GET /organizatia-mea": { id: 1, nume: "Liceul de Test", adresa: "Str. Școlii 1", judetId: 40, localitateId: null, profesori: 2, elevi: 1, cereri: 0 },
    "GET /organizatia-mea/membri": membri === null ? raspuns(500) : { elemente: membri },
    "GET /materii": { elemente: [{ id: 1, denumire: "Română" }, { id: 2, denumire: "Matematică" }] },
    "POST /organizatia-mea/membri/3/reaprobare": raspuns(204),
  });
  render(<SesiuneProvider><PaginaOrganizatie /></SesiuneProvider>);
  return {
    u: userEvent.setup(), cereri,
    interogari: () => cereri.filter((c) => c.cale === "/organizatia-mea/membri").map((c) => new URLSearchParams(c.query)),
  };
}

const deschide = async (u, tab) => u.click(await screen.findByRole("tab", { name: new RegExp(tab) }));

describe("panoul organizației: căutare și filtre", () => {
  it("fiecare tab are filtrele lui", async () => {
    const { u } = porneste();
    await screen.findByRole("searchbox", { name: "Caută un membru" });
    expect(screen.queryByRole("combobox", { name: "Stare" })).not.toBeInTheDocument();
    await deschide(u, "Profesori");
    expect(screen.getByRole("combobox", { name: "Stare" })).toHaveValue("aprobat");
    expect(screen.getByRole("combobox", { name: "Materie" })).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Clasa" })).not.toBeInTheDocument();
    await deschide(u, "Elevi");
    expect(screen.getByRole("combobox", { name: "Clasa" })).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Materie" })).not.toBeInTheDocument();
    expect(within(screen.getByRole("combobox", { name: "Clasa" })).queryByRole("option", { name: /Bac/ })).not.toBeInTheDocument();
  });

  it("filtrele și căutarea ajung în cerere", async () => {
    const { u, interogari } = porneste();
    await deschide(u, "Profesori");
    await screen.findByRole("option", { name: "Matematică" });
    await u.selectOptions(screen.getByRole("combobox", { name: "Materie" }), "2");
    await u.selectOptions(screen.getByRole("combobox", { name: "Stare" }), "");
    await u.type(screen.getByRole("searchbox"), " serban ");
    await waitFor(() => expect(interogari().at(-1).toString()).toBe("rol=profesor&materieId=2&q=serban"));
  });

  it("schimbarea tabului pornește filtrele de la capăt, cu o singură cerere", async () => {
    const { u, interogari } = porneste();
    await deschide(u, "Elevi");
    await u.selectOptions(screen.getByRole("combobox", { name: "Clasa" }), "XI");
    await waitFor(() => expect(interogari().at(-1).get("clasa")).toBe("XI"));
    const inainte = interogari().length;
    await deschide(u, "Profesori");
    await waitFor(() => expect(interogari().length).toBe(inainte + 1));
    expect(interogari().at(-1).toString()).toBe("rol=profesor&stare=aprobat");
  });

  it("cererile se pot căuta", async () => {
    const { u, interogari } = porneste();
    await u.type(await screen.findByRole("searchbox"), "andrei");
    await waitFor(() => expect(interogari().at(-1).toString()).toBe("stare=in_asteptare&q=andrei"));
  });

  it("lista goală cu filtre spune că nimic nu se potrivește", async () => {
    const { u } = porneste([]);
    await deschide(u, "Elevi");
    expect(await screen.findByText("Niciun elev în organizație încă.")).toBeInTheDocument();
    await u.type(screen.getByRole("searchbox"), "zzz");
    expect(await screen.findByText("Niciun elev nu se potrivește căutării și filtrelor.")).toBeInTheDocument();
  });

  it("stările se citesc în română", async () => {
    const { u } = porneste([membru(5, "Mara Lungu", { rol: "elev", stare: "expirat", clasa: "XI" })]);
    await deschide(u, "Elevi");
    const mara = (await screen.findByRole("heading", { name: "Mara Lungu" })).closest("article");
    expect(within(mara).getByText("Cerere expirată")).toBeInTheDocument();
    expect(screen.queryByText(/Stare: expirat/)).not.toBeInTheDocument();
  });
});

describe("panoul organizației: un profesor retras", () => {
  it("e marcat și poate fi aprobat din nou, după confirmare", async () => {
    const { u, cereri } = porneste();
    await deschide(u, "Profesori");
    const radu = (await screen.findByRole("heading", { name: "Radu Șerban" })).closest("article");
    expect(within(radu).getByText(/i-a retras aprobarea de profesor/)).toBeInTheDocument();
    await u.click(within(radu).getByRole("button", { name: "Aprobă din nou" }));
    await u.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Aprobă din nou" }));
    await waitFor(() => expect(cereri.some((c) => c.metoda === "POST" && c.cale === "/organizatia-mea/membri/3/reaprobare")).toBe(true));
  });

  it("un profesor neretras nu are butonul", async () => {
    const { u } = porneste();
    await deschide(u, "Profesori");
    const irina = (await screen.findByRole("heading", { name: "Irina Marin" })).closest("article");
    expect(within(irina).queryByRole("button", { name: "Aprobă din nou" })).not.toBeInTheDocument();
    expect(within(irina).queryByText(/retras/)).not.toBeInTheDocument();
  });

  it("renunțarea nu trimite nimic", async () => {
    const { u, cereri } = porneste();
    await deschide(u, "Profesori");
    const radu = (await screen.findByRole("heading", { name: "Radu Șerban" })).closest("article");
    await u.click(within(radu).getByRole("button", { name: "Aprobă din nou" }));
    await u.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Anulează" }));
    expect(cereri.some((c) => c.metoda === "POST")).toBe(false);
  });

});

describe("panoul organizației: pagini", () => {
  it("bara de pagini aduce pagina aleasă din aceeași listă", async () => {
    const pe = (start, n) => Array.from({ length: n }, (_, i) => membru(100 + start + i, `Elev ${start + i}`, { rol: "elev" }));
    const cereri = apiFals({
      "GET /utilizatori/me": { id: 1, nume: "Dana", rol: "organizatie", stareProfesor: "aprobat", organizatii: [] },
      "GET /organizatia-mea": { id: 1, nume: "Liceul de Test", adresa: "x", judetId: 40, localitateId: null, profesori: 0, elevi: 120, cereri: 0 },
      "GET /organizatia-mea/membri": (c) => (new URLSearchParams(c.query).get("pagina") === "3" ? { elemente: pe(101, 20), total: 120 } : { elemente: pe(1, 50), total: 120 }),
      "GET /materii": { elemente: [] },
    });
    const u = userEvent.setup();
    render(<SesiuneProvider><PaginaOrganizatie /></SesiuneProvider>);
    await u.click(await screen.findByRole("tab", { name: "Elevi" }));
    await u.click(await screen.findByRole("button", { name: "Pagina 3" }));
    expect(await screen.findByRole("heading", { name: "Elev 101" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Elev 1" })).not.toBeInTheDocument();
    const ultima = new URLSearchParams(cereri.filter((c) => c.cale === "/organizatia-mea/membri").at(-1).query);
    expect(ultima.toString()).toBe("rol=elev&stare=aprobat&pagina=3");
  });
});
