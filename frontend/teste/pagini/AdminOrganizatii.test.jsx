import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import AdminOrganizatii from "@/app/admin/organizatii/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { ORGANIZATII_APROBATE_PE_PAGINA_ADMIN, ORGANIZATII_IN_ASTEPTARE_PE_PAGINA_ADMIN } from "@/lib/pagini";
import { apiFals } from "../ajutor";

const org = (id, nume) => ({
  id, nume, adresa: "Str. 1", judetId: 1, judet: "Alba", localitateId: 1026, localitate: "Alba Iulia", stare: "in_asteptare", motivRespingere: null,
  creatLa: "2026-10-01T10:00:00Z", adminId: 9, adminNume: "Ovidiu", adminEmail: "o@example.invalid", adminTelefon: null, adminEmailConfirmat: true,
  profesori: 0, elevi: 0, cereri: 0,
});

function porneste(raspuns = () => ({ elemente: [org(1, "Centrul Alba")], total: 1 })) {
  const cereri = apiFals({
    "GET /utilizatori/me": { id: 9, nume: "Admin", rol: "admin", organizatii: [] },
    "GET /admin/organizatii": raspuns,
    "GET /judete": { elemente: [{ id: 1, nume: "Alba" }, { id: 40, nume: "București" }] },
    "GET /judete/1/localitati": { elemente: [{ id: 1026, nume: "Alba Iulia", uatNume: "Municipiul Alba Iulia" }] },
  });
  render(<SesiuneProvider><AdminOrganizatii /></SesiuneProvider>);
  return { u: userEvent.setup(), interogari: () => cereri.filter((c) => c.cale === "/admin/organizatii").map((c) => new URLSearchParams(c.query)) };
}

describe("admin → organizații: filtre și căutare", () => {
  it("fiecare tab cere mărimea lui de pagină", async () => {
    const { u, interogari } = porneste();
    await screen.findByRole("heading", { name: "Centrul Alba" });
    expect(interogari().at(-1).get("marime")).toBe(String(ORGANIZATII_IN_ASTEPTARE_PE_PAGINA_ADMIN));
    await u.click(screen.getByRole("tab", { name: "Aprobate" }));
    await waitFor(() => expect(interogari().at(-1).get("stare")).toBe("aprobata"));
    expect(interogari().at(-1).get("marime")).toBe(String(ORGANIZATII_APROBATE_PE_PAGINA_ADMIN));
  });

  it("căutarea pleacă după o pauză, o singură dată", async () => {
    const { u, interogari } = porneste();
    await screen.findByRole("heading", { name: "Centrul Alba" });
    await u.type(screen.getByRole("searchbox", { name: "Caută o organizație" }), " alba iulia ");
    await waitFor(() => expect(interogari().at(-1).get("q")).toBe("alba iulia"));
    expect(interogari().filter((q) => q.has("q"))).toHaveLength(1);
  });

  it("județul și apoi localitatea filtrează", async () => {
    const { u, interogari } = porneste();
    await screen.findByRole("option", { name: "Alba" });
    await u.selectOptions(screen.getByRole("combobox", { name: /Județul/ }), "1");
    await waitFor(() => expect(interogari().at(-1).get("judetId")).toBe("1"));
    expect(interogari().at(-1).has("localitateId")).toBe(false);
    await u.type(screen.getByRole("textbox", { name: /Localitatea/ }), "alba");
    await u.click(await screen.findByRole("button", { name: /Alba Iulia/ }));
    await waitFor(() => expect(interogari().at(-1).get("localitateId")).toBe("1026"));
    // Retyping the locality drops it from the filter.
    await u.type(screen.getByRole("textbox", { name: /Localitatea/ }), "x");
    await waitFor(() => expect(interogari().at(-1).has("localitateId")).toBe(false));
  });

  it("un filtru nou pornește de la prima pagină", async () => {
    const { u, interogari } = porneste(() => ({ elemente: [org(1, "Centrul Alba")], total: 100 }));
    await u.click(await screen.findByRole("button", { name: "Pagina 2" }));
    await waitFor(() => expect(interogari().at(-1).get("pagina")).toBe("2"));
    await u.type(screen.getByRole("searchbox"), "centrul");
    await waitFor(() => expect(interogari().at(-1).get("q")).toBe("centrul"));
    expect(interogari().at(-1).has("pagina")).toBe(false);
  });

  it("lista goală cu filtre spune că nimic nu se potrivește", async () => {
    const { u } = porneste(() => ({ elemente: [], total: 0 }));
    expect(await screen.findByText("Nicio organizație aici.")).toBeInTheDocument();
    await u.type(screen.getByRole("searchbox"), "zzz");
    expect(await screen.findByText("Nicio organizație nu se potrivește căutării și filtrelor.")).toBeInTheDocument();
  });
});
