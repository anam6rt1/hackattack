import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import AlegeLoc from "@/components/AlegeLoc";
import AlegeOrganizatie from "@/components/AlegeOrganizatie";
import { apiFals } from "../ajutor";

const JUDETE = { elemente: [{ id: 1, nume: "Alba" }, { id: 40, nume: "București" }, { id: 12, nume: "Cluj" }] };
const LOCALITATI_ALBA = { elemente: [{ id: 1026, nume: "Alba Iulia", uatNume: "Municipiul Alba Iulia" }, { id: 1035, nume: "Bărăbanț", uatNume: "Municipiul Alba Iulia" }] };

function apiLoc() {
  return apiFals({
    "GET /judete": JUDETE,
    "GET /judete/1/localitati": LOCALITATI_ALBA,
    "GET /judete/40/localitati": { elemente: [{ id: 179141, nume: "Sectorul 1", uatNume: "Municipiul București" }] },
  });
}

const valori = (container) => Object.fromEntries([...container.querySelectorAll("input[type=hidden]")].map((i) => [i.name, i.value]));

describe("AlegeLoc", () => {
  it("încarcă județele în listă", async () => {
    apiLoc();
    render(<AlegeLoc />);
    expect(await screen.findByRole("option", { name: "București" })).toBeInTheDocument();
    expect(screen.getAllByRole("option")).toHaveLength(4);
  });

  it("localitatea e blocată până se alege județul", async () => {
    apiLoc();
    render(<AlegeLoc />);
    expect(screen.getByPlaceholderText("Alege întâi județul")).toBeDisabled();
  });

  it("județul e opțional, sau obligatoriu", async () => {
    apiLoc();
    const { rerender } = render(<AlegeLoc />);
    expect(screen.getByText("Județul (opțional)")).toBeInTheDocument();
    rerender(<AlegeLoc obligatoriu />);
    expect(screen.getByText("Județul")).toBeInTheDocument();
    expect(screen.getByRole("combobox")).toBeRequired();
  });

  it("caută localitatea după ce te oprești din scris, fără diacritice", async () => {
    const cereri = apiLoc();
    const u = userEvent.setup();
    render(<AlegeLoc />);
    await u.selectOptions(screen.getByRole("combobox"), await screen.findByRole("option", { name: "Alba" }));
    await u.type(screen.getByPlaceholderText("Scrie primele litere"), "barab");
    expect(await screen.findByRole("button", { name: /Bărăbanț/ })).toBeInTheDocument();
    const cautari = cereri.filter((c) => c.cale === "/judete/1/localitati");
    // Typing quickly gives one search for the final text, not one per letter.
    expect(cautari.at(-1).query).toBe("?q=barab");
    expect(cautari.length).toBeLessThan(5);
  });

  it("alegerea unei localități completează câmpurile ascunse și anunță", async () => {
    apiLoc();
    const u = userEvent.setup();
    const onChange = vi.fn();
    const { container } = render(<AlegeLoc onChange={onChange} />);
    await u.selectOptions(screen.getByRole("combobox"), await screen.findByRole("option", { name: "Alba" }));
    expect(onChange).toHaveBeenLastCalledWith({ judetId: 1, localitateId: null });
    await u.click(screen.getByPlaceholderText("Scrie primele litere"));
    await u.click(await screen.findByRole("button", { name: "Alba Iulia Municipiul Alba Iulia" }));
    expect(onChange).toHaveBeenLastCalledWith({ judetId: 1, localitateId: 1026 });
    expect(valori(container)).toEqual({ judetId: "1", localitateId: "1026" });
    expect(screen.getByDisplayValue("Alba Iulia")).toBeInTheDocument();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("schimbarea județului șterge localitatea aleasă", async () => {
    apiLoc();
    const u = userEvent.setup();
    const { container } = render(<AlegeLoc />);
    await u.selectOptions(screen.getByRole("combobox"), await screen.findByRole("option", { name: "Alba" }));
    await u.click(screen.getByPlaceholderText("Scrie primele litere"));
    await u.click(await screen.findByRole("button", { name: /Bărăbanț/ }));
    await u.selectOptions(screen.getByRole("combobox"), "40");
    expect(valori(container)).toEqual({ judetId: "40", localitateId: "" });
    expect(screen.getByPlaceholderText("Scrie primele litere")).toHaveValue("");
  });

  it("pornește cu locul salvat", async () => {
    apiLoc();
    const { container } = render(<AlegeLoc judetInitial={40} localitateInitiala={{ id: 179141, nume: "Sectorul 1", uatNume: "București" }} />);
    await screen.findByRole("option", { name: "București" });
    expect(screen.getByRole("combobox")).toHaveValue("40");
    expect(screen.getByDisplayValue("Sectorul 1")).toBeInTheDocument();
    expect(valori(container)).toEqual({ judetId: "40", localitateId: "179141" });
  });

  it("fără server rămâne utilizabil, doar fără județe", async () => {
    apiFals({});
    render(<AlegeLoc />);
    await waitFor(() => expect(screen.getAllByRole("option")).toHaveLength(1));
  });
});

const ORGANIZATII = [
  { id: 1, nume: "Liceul de Test", judet: "București", localitate: "Sectorul 1" },
  { id: 2, nume: "Centrul Alba", judet: "Alba", localitate: null },
];

describe("AlegeOrganizatie", () => {
  it("elevul alege cel mult una, dintr-o listă", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    render(<AlegeOrganizatie />);
    expect(await screen.findByRole("option", { name: "Liceul de Test · Sectorul 1" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Centrul Alba · Alba" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Fără organizație" })).toBeInTheDocument();
    expect(screen.getByText("Organizația ta (opțional)")).toBeInTheDocument();
  });

  it("lista are numele titlului ei (pentru cititoarele de ecran)", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    render(<AlegeOrganizatie obligatoriu />);
    expect(await screen.findByRole("combobox", { name: "Organizația ta" })).toBeInTheDocument();
    expect(screen.getByRole("group", { name: "Organizația ta" })).toBeInTheDocument();
  });

  it("grupul de bife al profesorului are nume", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    render(<AlegeOrganizatie multiplu />);
    await screen.findAllByRole("checkbox");
    expect(screen.getByRole("group", { name: "Organizațiile în care predai" })).toBeInTheDocument();
  });

  it("profesorul bifează una sau mai multe", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie multiplu obligatoriu />);
    const bife = await screen.findAllByRole("checkbox");
    expect(bife).toHaveLength(2);
    await u.click(bife[0]);
    await u.click(bife[1]);
    expect(bife.every((b) => b.checked && b.name === "organizatieIds")).toBe(true);
    expect(screen.getByText("Organizațiile în care predai")).toBeInTheDocument();
  });

  it("arată întâi organizațiile județului ales, apoi toate la cerere", async () => {
    const cereri = apiFals({ "GET /organizatii": { elemente: [] }, "GET /organizatii?judetId=1": { elemente: [ORGANIZATII[1]] } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie judetId={1} />);
    expect(await screen.findByRole("option", { name: "Centrul Alba · Alba" })).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Toate județele" }));
    expect(await screen.findByText("Nicio organizație aprobată încă.")).toBeInTheDocument();
    expect(cereri.map((c) => c.query)).toEqual(["?judetId=1", ""]);
    expect(screen.getByRole("button", { name: "Doar județul ales" })).toBeInTheDocument();
  });

  it("spune când județul nu are organizații", async () => {
    apiFals({ "GET /organizatii": { elemente: [] } });
    render(<AlegeOrganizatie judetId={12} />);
    expect(await screen.findByText("Nicio organizație aprobată în acest județ.")).toBeInTheDocument();
  });

  it("arată „Se încarcă...” până răspunde serverul", () => {
    apiFals({ "GET /organizatii": () => new Promise(() => {}) });
    render(<AlegeOrganizatie />);
    expect(screen.getByText("Se încarcă...")).toBeInTheDocument();
  });

  it("fără județ nu există butonul „Toate județele”", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    render(<AlegeOrganizatie />);
    await screen.findAllByRole("option");
    expect(screen.queryByRole("button", { name: "Toate județele" })).not.toBeInTheDocument();
  });

  it("obligatorie pentru elev, lista cere o alegere", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    render(<AlegeOrganizatie obligatoriu />);
    expect(await screen.findByRole("combobox")).toBeRequired();
    expect(screen.getByRole("option", { name: "Alege organizația" })).toBeInTheDocument();
  });

  it("o eroare a serverului arată lista goală, nu „Se încarcă” la nesfârșit", async () => {
    apiFals({});
    render(<AlegeOrganizatie />);
    expect(await screen.findByText("Nicio organizație aprobată încă.")).toBeInTheDocument();
  });
});
