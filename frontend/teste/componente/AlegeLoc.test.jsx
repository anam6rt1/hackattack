import { render, screen, waitFor, within } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import AlegeLoc from "@/components/AlegeLoc";
import AlegeOrganizatie, { AlegeOrganizatii } from "@/components/AlegeOrganizatie";
import AlegeMaterii from "@/components/AlegeMaterii";
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
const MULTE = Array.from({ length: 6 }, (_, i) => ({ id: i + 1, nume: `Școala ${i + 1}`, judet: "Alba", localitate: "Alba Iulia" }));

const organizatieAleasa = (container) => container.querySelector("input[name=organizatieId]").value;

describe("AlegeOrganizatie (elevul)", () => {
  it("lista se deschide ca un dropdown, cu „Fără organizație” și locul fiecăreia", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie />);
    await u.click(await screen.findByRole("combobox", { name: "Organizația ta (opțional)" }));
    expect(screen.getByRole("option", { name: "Liceul de Test · Sectorul 1" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Centrul Alba · Alba" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Fără organizație" })).toBeInTheDocument();
  });

  it("alegerea pleacă în câmpul ascuns, iar câmpul arată numele", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    const { container } = render(<AlegeOrganizatie />);
    await u.click(await screen.findByRole("combobox"));
    await u.click(screen.getByRole("option", { name: "Centrul Alba · Alba" }));
    expect(organizatieAleasa(container)).toBe("2");
    expect(screen.getByRole("combobox")).toHaveValue("Centrul Alba");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    // Retyping drops the choice.
    await u.type(screen.getByRole("combobox"), "x");
    expect(organizatieAleasa(container)).toBe("");
  });

  it("căutarea după nume sau localitate pleacă la server", async () => {
    const cereri = apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 }, "GET /organizatii?q=sectorul": { elemente: [ORGANIZATII[0]], total: 1 } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie />);
    await u.type(await screen.findByRole("combobox"), "sectorul");
    await waitFor(() => expect(screen.queryByRole("option", { name: /Centrul Alba/ })).not.toBeInTheDocument());
    expect(screen.getByRole("option", { name: "Liceul de Test · Sectorul 1" })).toBeInTheDocument();
    expect(cereri.map((c) => c.query)).toEqual(["", "?q=sectorul"]);
  });

  it("Enter alege prima organizație găsită", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    const { container } = render(<AlegeOrganizatie />);
    await u.click(await screen.findByRole("combobox"));
    await u.keyboard("{Enter}");
    expect(organizatieAleasa(container)).toBe("1");
  });

  it("spune când se văd doar primele rezultate", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 340 } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie />);
    await u.click(await screen.findByRole("combobox"));
    expect(screen.getByText("Se văd primele 2 din 340. Scrie ca să le restrângi.")).toBeInTheDocument();
  });

  it("obligatorie, cere o organizație aleasă din listă, nu doar text", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie obligatoriu />);
    const camp = await screen.findByRole("combobox", { name: "Organizația ta" });
    expect(camp).toBeInvalid();
    await u.type(camp, "liceu");
    expect(camp).toBeInvalid();
    await u.click(await screen.findByRole("option", { name: "Liceul de Test · Sectorul 1" }));
    expect(camp).toBeValid();
    expect(screen.queryByRole("option", { name: "Fără organizație" })).not.toBeInTheDocument();
  });

  it("opțională, câmpul gol e valid, textul neales nu", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie />);
    const camp = await screen.findByRole("combobox");
    expect(camp).toBeValid();
    await u.type(camp, "zz");
    expect(camp).toBeInvalid();
  });

  it("arată întâi organizațiile județului ales, apoi toate la cerere", async () => {
    const cereri = apiFals({ "GET /organizatii": { elemente: [] }, "GET /organizatii?judetId=1": { elemente: [ORGANIZATII[1]], total: 1 } });
    const u = userEvent.setup();
    render(<AlegeOrganizatie judetId={1} />);
    await u.click(await screen.findByRole("combobox"));
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
    expect(screen.getByRole("combobox")).toHaveAttribute("placeholder", "Se încarcă...");
  });

  it("fără județ nu există butonul „Toate județele”", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII } });
    render(<AlegeOrganizatie />);
    await screen.findByPlaceholderText("Caută după nume sau localitate");
    expect(screen.queryByRole("button", { name: "Toate județele" })).not.toBeInTheDocument();
  });

  it("o eroare a serverului arată lista goală, nu „Se încarcă” la nesfârșit", async () => {
    apiFals({});
    render(<AlegeOrganizatie />);
    expect(await screen.findByText("Nicio organizație aprobată încă.")).toBeInTheDocument();
  });
});

function ProfesorOrganizatii({ judetId = null, initiale = [] }) {
  const [alese, setAlese] = useState(initiale);
  return <form><AlegeOrganizatii judetId={judetId} alese={alese} onChange={setAlese} /></form>;
}

const aleseIds = (container) => [...container.querySelectorAll("input[name=organizatieIds]")].map((i) => i.value);

describe("AlegeOrganizatii (profesorul)", () => {
  it("butonul deschide fereastra; bifele devin pastile și câmpuri ascunse", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    const { container } = render(<ProfesorOrganizatii />);
    expect(screen.getByRole("group", { name: "Organizațiile în care predai (0 din 4)" })).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "+ Adaugă organizație" }));
    const fereastra = screen.getByRole("dialog", { name: "Organizațiile în care predai" });
    await u.click(await within(fereastra).findByRole("checkbox", { name: /Liceul de Test/ }));
    await u.click(within(fereastra).getByRole("checkbox", { name: /Centrul Alba/ }));
    expect(within(fereastra).getByText("2 din 4 alese.")).toBeInTheDocument();
    await u.click(within(fereastra).getByRole("button", { name: "Gata" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(aleseIds(container)).toEqual(["1", "2"]);
    await u.click(screen.getByRole("button", { name: "Scoate Liceul de Test" }));
    expect(aleseIds(container)).toEqual(["2"]);
  });

  it("cel mult patru: celelalte bife se dezactivează", async () => {
    apiFals({ "GET /organizatii": { elemente: MULTE, total: 6 } });
    const u = userEvent.setup();
    const { container } = render(<ProfesorOrganizatii />);
    await u.click(screen.getByRole("button", { name: "+ Adaugă organizație" }));
    const bife = await screen.findAllByRole("checkbox");
    for (const b of bife.slice(0, 4)) await u.click(b);
    expect(bife[4]).toBeDisabled();
    expect(bife[5]).toBeDisabled();
    expect(screen.getByText(/ai ajuns la limită/)).toBeInTheDocument();
    await u.click(bife[0]);
    expect(bife[4]).toBeEnabled();
    expect(aleseIds(container)).toEqual(["2", "3", "4"]);
  });

  it("plin, butonul devine „Schimbă alegerea”", () => {
    apiFals({ "GET /organizatii": { elemente: [] } });
    render(<ProfesorOrganizatii initiale={MULTE.slice(0, 4).map((o) => ({ id: o.id, nume: o.nume }))} />);
    expect(screen.getByRole("button", { name: "Schimbă alegerea" })).toBeInTheDocument();
  });

  it("căutarea pleacă la server, cu județul ales", async () => {
    const cereri = apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    render(<ProfesorOrganizatii judetId={1} />);
    await u.click(screen.getByRole("button", { name: "+ Adaugă organizație" }));
    await u.type(screen.getByRole("searchbox", { name: "Caută după nume, județ sau localitate" }), "alba iulia");
    await waitFor(() => expect(cereri.at(-1).query).toBe("?judetId=1&q=alba+iulia"));
    await u.click(screen.getByRole("button", { name: "Toate județele" }));
    await waitFor(() => expect(cereri.at(-1).query).toBe("?q=alba+iulia"));
  });

  it("Escape închide fereastra și păstrează alegerea", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const u = userEvent.setup();
    const { container } = render(<ProfesorOrganizatii />);
    await u.click(screen.getByRole("button", { name: "+ Adaugă organizație" }));
    await u.click(await screen.findByRole("checkbox", { name: /Centrul Alba/ }));
    await u.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(aleseIds(container)).toEqual(["2"]);
  });

  it("Enter în căutare nu trimite formularul paginii", async () => {
    apiFals({ "GET /organizatii": { elemente: ORGANIZATII, total: 2 } });
    const trimis = vi.fn((e) => e.preventDefault());
    const u = userEvent.setup();
    render(<form onSubmit={trimis}><AlegeOrganizatii alese={[]} onChange={() => {}} /></form>);
    await u.click(screen.getByRole("button", { name: "+ Adaugă organizație" }));
    await u.type(screen.getByRole("searchbox"), "liceu{Enter}");
    expect(trimis).not.toHaveBeenCalled();
  });
});

function Materii() {
  const [alese, setAlese] = useState([]);
  return <AlegeMaterii alese={alese} onChange={setAlese} name="materieIds" />;
}

describe("AlegeMaterii", () => {
  it("caută materia pe server și o adaugă", async () => {
    const cereri = apiFals({
      "GET /materii": { elemente: [{ id: 1, denumire: "Limba și literatura română" }, { id: 2, denumire: "Matematică" }] },
      "GET /materii?q=mate": { elemente: [{ id: 2, denumire: "Matematică" }] },
    });
    const u = userEvent.setup();
    const { container } = render(<Materii />);
    await u.click(screen.getByRole("button", { name: "+ Adaugă materie" }));
    await u.type(screen.getByRole("searchbox", { name: "Caută o materie" }), "mate");
    await waitFor(() => expect(screen.queryByRole("checkbox", { name: /română/ })).not.toBeInTheDocument());
    await u.click(screen.getByRole("checkbox", { name: "Matematică" }));
    expect(cereri.map((c) => c.query)).toContain("?q=mate");
    expect(container.querySelector("input[name=materieIds]").value).toBe("2");
    expect(screen.getByRole("group", { name: "Materii predate (1 din 4)" })).toBeInTheDocument();
  });

  it("spune când nimic nu se potrivește", async () => {
    apiFals({ "GET /materii": { elemente: [] } });
    const u = userEvent.setup();
    render(<Materii />);
    await u.click(screen.getByRole("button", { name: "+ Adaugă materie" }));
    expect(await screen.findByText("Nimic nu se potrivește căutării.")).toBeInTheDocument();
  });
});
