import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import AdminProfesori from "@/app/admin/profesori/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals, raspuns } from "../ajutor";

const profesor = (id, nume, extra = {}) => ({
  id, nume, username: nume.toLowerCase(), email: `${id}@example.invalid`, telefon: null, descriere: null, pozaUrl: null, areDovada: false,
  stare: "aprobat", motivRespingere: null, onorariuNegociat: 56, materii: [{ id: 1, denumire: "Română" }], creatLa: "2026-10-01T10:00:00Z",
  retrasLa: null, ...extra,
});

function porneste(lista, raspunsRetragere = { sedinteAnulate: 2 }) {
  const cereri = apiFals({
    "GET /utilizatori/me": { id: 9, nume: "Admin", rol: "admin", organizatii: [] },
    "GET /admin/profesori": { elemente: lista },
    "GET /tarif": { suma: 80, moneda: "RON", durataMinute: 60 },
    "POST /admin/profesori/2/retragere": raspunsRetragere,
    "POST /admin/profesori/3/reaprobare": raspuns(204),
  });
  render(<SesiuneProvider><AdminProfesori /></SesiuneProvider>);
  return { u: userEvent.setup(), cereri, trimise: () => cereri.filter((c) => c.metoda === "POST") };
}

describe("admin → profesori: retragerea aprobării", () => {
  it("cere un motiv, apoi o confirmare care spune ce urmează", async () => {
    const { u, trimise } = porneste([profesor(2, "Irina")]);
    const card = (await screen.findByRole("heading", { name: "Irina" })).closest("article");
    await u.click(within(card).getByRole("button", { name: "Retrage aprobarea" }));
    const retrage = within(card).getByRole("button", { name: "Retrage" });
    expect(retrage).toBeDisabled();
    await u.type(within(card).getByRole("textbox", { name: "Motivul retragerii" }), "   ");
    expect(retrage).toBeDisabled();
    await u.type(within(card).getByRole("textbox", { name: "Motivul retragerii" }), "Plângeri de la elevi.");
    await u.click(retrage);
    const dialog = screen.getByRole("alertdialog");
    expect(dialog).toHaveTextContent("Ședințele viitoare se anulează");
    expect(dialog).toHaveTextContent("O organizație din care face parte îl poate aproba din nou");
    await u.click(within(dialog).getByRole("button", { name: "Retrage aprobarea" }));
    await waitFor(() => expect(trimise()).toHaveLength(1));
    expect(trimise()[0]).toMatchObject({ cale: "/admin/profesori/2/retragere", corp: { motiv: "Plângeri de la elevi." } });
  });

  it("renunțarea la confirmare nu trimite nimic", async () => {
    const { u, trimise } = porneste([profesor(2, "Irina")]);
    const card = (await screen.findByRole("heading", { name: "Irina" })).closest("article");
    await u.click(within(card).getByRole("button", { name: "Retrage aprobarea" }));
    await u.type(within(card).getByRole("textbox", { name: "Motivul retragerii" }), "x");
    await u.click(within(card).getByRole("button", { name: "Retrage" }));
    await u.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Anulează" }));
    expect(trimise()).toHaveLength(0);
  });

  it("o eroare a serverului se vede pe card", async () => {
    const { u } = porneste([profesor(2, "Irina")], raspuns(409, { mesaj: "Profesorul nu este aprobat acum." }));
    const card = (await screen.findByRole("heading", { name: "Irina" })).closest("article");
    await u.click(within(card).getByRole("button", { name: "Retrage aprobarea" }));
    await u.type(within(card).getByRole("textbox", { name: "Motivul retragerii" }), "x");
    await u.click(within(card).getByRole("button", { name: "Retrage" }));
    await u.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Retrage aprobarea" }));
    expect(await within(card).findByRole("alert")).toHaveTextContent("Profesorul nu este aprobat acum.");
  });

  it("un profesor retras arată data și motivul și poate fi aprobat din nou", async () => {
    const { u, trimise } = porneste([profesor(3, "Radu", { stare: "respins", motivRespingere: "Plângeri.", retrasLa: "2026-10-20T09:30:00Z" })]);
    const card = (await screen.findByRole("heading", { name: "Radu" })).closest("article");
    expect(within(card).getByText(/Aprobare retrasă pe .*: Plângeri\./)).toBeInTheDocument();
    expect(within(card).queryByRole("button", { name: "Retrage aprobarea" })).not.toBeInTheDocument();
    await u.click(within(card).getByRole("button", { name: "Aprobă din nou" }));
    await u.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Aprobă din nou" }));
    await waitFor(() => expect(trimise()).toMatchObject([{ cale: "/admin/profesori/3/reaprobare" }]));
  });

  it("un profesor respins de organizații (nu retras) nu are „Aprobă din nou”", async () => {
    porneste([profesor(4, "Paul", { stare: "respins", motivRespingere: "Fără diplomă." })]);
    const card = (await screen.findByRole("heading", { name: "Paul" })).closest("article");
    expect(within(card).getByText("Respins: Fără diplomă.")).toBeInTheDocument();
    expect(within(card).queryByRole("button", { name: "Aprobă din nou" })).not.toBeInTheDocument();
  });

  it("taburile cer lista pe stare", async () => {
    const { u, cereri } = porneste([]);
    await screen.findByText("Nicio cerere aici.");
    await u.click(screen.getByRole("tab", { name: "Respinși" }));
    await u.click(screen.getByRole("tab", { name: "Aprobați" }));
    await waitFor(() => expect(cereri.filter((c) => c.cale === "/admin/profesori").map((c) => c.query))
      .toEqual(["?stare=in_asteptare&marime=6", "?stare=respins&marime=6", "?stare=aprobat&marime=6"]));
  });
});
