import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import AcasaAdmin from "@/components/acasa/AcasaAdmin";
import AcasaElev from "@/components/acasa/AcasaElev";
import AcasaProfesor from "@/components/acasa/AcasaProfesor";
import AnuleazaRezervare from "@/components/profesori/AnuleazaRezervare";
import SloturileMele from "@/components/profesori/SloturileMele";
import { apiFals, raspuns } from "../ajutor";

const maine = new Date(Date.now() + 2 * 86400_000).toISOString();
const ieri = new Date(Date.now() - 86400_000).toISOString();

const PROGRES = {
  nivel: { nivel: 2, titlu: "Ucenic", xpTotal: 150, xpNivel: 100, xpUrmatorNivel: 300, titluUrmator: "Cititor" },
  materii: [
    { materieId: 1, denumire: "Limba și literatura română", puncte: 20, lectiiCompletate: 2, totalLectii: 5 },
    { materieId: 2, denumire: "Matematică", puncte: 0, lectiiCompletate: 0, totalLectii: 0 },
  ],
  ultimaLectie: { parcursId: 7, titlu: "Ion", materie: "Limba și literatura română", completata: false, capitol: "Glasul pământului" },
  lectiiCompletate: [{ parcursId: 3, titlu: "Luceafărul" }],
};

const rezervare = (id, extra = {}, slot = {}) => ({
  id, status: "confirmata", profesor: "Irina Marin", participare: "online",
  slot: { titlu: `Ședința ${id}`, inceputLa: maine, linkIntalnire: null, loc: null, notaLoc: null, ...slot }, ...extra,
});

describe("panoul elevului", () => {
  const porneste = (progres = PROGRES, rezervari = []) => {
    const cereri = apiFals({ "GET /progres": progres, "GET /rezervari": { elemente: rezervari }, "DELETE /rezervari/1": raspuns(204) });
    render(<AcasaElev utilizator={{ nume: "Ana Țăranu" }} />);
    return cereri;
  };

  it("salută după prenume și arată unde a rămas", async () => {
    porneste();
    expect(await screen.findByRole("heading", { name: "Salut, Ana!" })).toBeInTheDocument();
    expect(screen.getByText("Data trecută ai lucrat la „Glasul pământului”.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Continuă de unde ai rămas" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Ion/ })).toHaveAttribute("href", "/parcurs/7");
  });

  it("nivelul, XP-ul și cât mai e", async () => {
    porneste();
    expect(await screen.findByLabelText("Nivelul 2")).toBeInTheDocument();
    expect(screen.getByText("150 XP")).toBeInTheDocument();
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "25");
    expect(screen.getByText(/Mai ai 150 XP până la „Cititor”/)).toBeInTheDocument();
  });

  it("la nivelul maxim bara e plină", async () => {
    porneste({ ...PROGRES, nivel: { nivel: 9, titlu: "Maestru", xpTotal: 5000, xpNivel: 5000, xpUrmatorNivel: null, titluUrmator: null } });
    expect(await screen.findByText("Ai atins cel mai înalt nivel. Felicitări!")).toBeInTheDocument();
    expect(screen.getByRole("progressbar")).toHaveAttribute("aria-valuenow", "100");
  });

  it("progresul pe materii, și la o materie fără lecții", async () => {
    porneste();
    expect(await screen.findByText("2 din 5 lecții · 20 puncte")).toBeInTheDocument();
    expect(screen.getByText("0 din 0 lecții · 0 puncte")).toBeInTheDocument();
  });

  it("elevul nou primește un îndemn, fără „Recapitulează”", async () => {
    porneste({ ...PROGRES, ultimaLectie: null, lectiiCompletate: [] });
    expect(await screen.findByRole("heading", { name: "Următorul pas" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Recapitulează" })).not.toBeInTheDocument();
  });

  it("lecția ultimă terminată apare „Terminată”", async () => {
    porneste({ ...PROGRES, ultimaLectie: { ...PROGRES.ultimaLectie, completata: true } });
    expect(await screen.findByText("Terminată")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Următorul pas" })).toBeInTheDocument();
  });

  it("ședințele viitoare: link, sală sau „profesorul va adăuga linkul”", async () => {
    porneste(PROGRES, [
      rezervare(1, {}, { linkIntalnire: "https://meet.example.ro/a" }),
      rezervare(2, { participare: "la_sala" }, { loc: { organizatie: "Liceul de Test", localitate: "Sectorul 1", judet: "București" }, notaLoc: "Sala 12" }),
      rezervare(3),
    ]);
    expect(await screen.findByRole("link", { name: "Intră în ședință →" })).toHaveAttribute("href", "https://meet.example.ro/a");
    expect(screen.getByText("La sală: Liceul de Test · Sectorul 1, București · Sala 12")).toBeInTheDocument();
    expect(screen.getByText("Profesorul va adăuga linkul ședinței.")).toBeInTheDocument();
  });

  it("ședințele trecute sau anulate nu apar", async () => {
    porneste(PROGRES, [rezervare(1, {}, { inceputLa: ieri }), rezervare(2, { status: "anulata" })]);
    expect(await screen.findByText("Nu ai ședințe rezervate.")).toBeInTheDocument();
  });

  it("cel mult 4 ședințe", async () => {
    porneste(PROGRES, [1, 2, 3, 4, 5, 6].map((i) => rezervare(i)));
    await screen.findByText("Ședința 1");
    expect(screen.queryByText("Ședința 5")).not.toBeInTheDocument();
  });

  it("o eroare la progres e afișată", async () => {
    apiFals({ "GET /progres": raspuns(500, { mesaj: "Serverul nu răspunde." }), "GET /rezervari": { elemente: [] } });
    render(<AcasaElev utilizator={{ nume: "Ana" }} />);
    expect(await screen.findByText("Serverul nu răspunde.")).toBeInTheDocument();
  });
});

describe("anularea unei rezervări", () => {
  it("cere confirmare pe loc", async () => {
    apiFals({ "DELETE /rezervari/5": raspuns(204) });
    const u = userEvent.setup();
    render(<AnuleazaRezervare rezervareId={5} onAnulat={() => {}} />);
    await u.click(screen.getByRole("button", { name: "Anulează" }));
    expect(screen.getByRole("button", { name: "Da, anulează" })).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Nu" }));
    expect(screen.getByRole("button", { name: "Anulează" })).toBeInTheDocument();
  });

  it("anulează și anunță pagina", async () => {
    const cereri = apiFals({ "DELETE /rezervari/5": raspuns(204) });
    const onAnulat = vi.fn();
    const u = userEvent.setup();
    render(<AnuleazaRezervare rezervareId={5} onAnulat={onAnulat} />);
    await u.click(screen.getByRole("button", { name: "Anulează" }));
    await u.click(screen.getByRole("button", { name: "Da, anulează" }));
    await waitFor(() => expect(onAnulat).toHaveBeenCalled());
    expect(cereri[0]).toMatchObject({ metoda: "DELETE", cale: "/rezervari/5" });
  });

  it("arată de ce nu se poate", async () => {
    apiFals({ "DELETE /rezervari/5": raspuns(409, { mesaj: "Ședința a început deja și nu mai poate fi anulată." }) });
    const u = userEvent.setup();
    render(<AnuleazaRezervare rezervareId={5} onAnulat={() => {}} />);
    await u.click(screen.getByRole("button", { name: "Anulează" }));
    await u.click(screen.getByRole("button", { name: "Da, anulează" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("a început deja");
  });
});

const slot = (id, extra = {}, elevi = []) => ({
  slot: { id, titlu: `Ședința ${id}`, materie: "Română", inceputLa: maine, status: "deschisa", tip: "online", capacitate: 5, locuriLibere: 5 - elevi.length,
    proBono: false, linkIntalnire: null, loc: null, notaLoc: null, ...extra },
  elevi,
});

const PANOU = {
  castiguri: { total: 120, inAsteptare: 40, moneda: "RON" }, onorariuNegociat: 40, moneda: "RON",
  pret: { suma: 50, moneda: "RON", durataMinute: 50 },
  profil: { nume: "Irina Marin", pozaUrl: "/profesori/2/poza", materii: [{ id: 1, denumire: "Limba și literatura română" }, { id: 2, denumire: "Matematică" }] },
  sloturi: [],
};

describe("panoul profesorului și ședințele", () => {
  const porneste = (panou = PANOU, rute = {}) => {
    const cereri = apiFals({
      "GET /profesori/me": panou,
      "GET /profesori/me/loc": { organizatieId: null, optiuni: [{ organizatieId: null, eticheta: "Locul meu", loc: { localitate: "Sectorul 2", judet: "București" } }, { organizatieId: 1, eticheta: "Liceul de Test", loc: { localitate: "Sectorul 1", judet: "București" } }] },
      ...rute,
    });
    const u = userEvent.setup();
    render(<AcasaProfesor utilizator={{ nume: "Irina Marin" }} />);
    return { u, cereri };
  };

  it("câștigurile, onorariul și profilul", async () => {
    porneste();
    expect(await screen.findByRole("heading", { name: "Bine ai revenit, Irina!" })).toBeInTheDocument();
    expect(screen.getByText("40 RON urmează să fie decontați.")).toBeInTheDocument();
    expect(screen.getByText("Limba și literatura română, Matematică")).toBeInTheDocument();
    expect(document.querySelector("img.tv-poza")).toHaveAttribute("src", "/api/profesori/2/poza");
  });

  it("numără doar înscrierile confirmate", async () => {
    porneste({ ...PANOU, sloturi: [slot(1, {}, [{ nume: "Ana", status: "confirmata" }, { nume: "Radu", status: "anulata" }]), slot(2, {}, [{ nume: "Elena", status: "confirmata" }])] });
    const cutie = (await screen.findByText("Înscrieri")).parentElement;
    expect(within(cutie).getByText("2")).toBeInTheDocument();
  });

  it("ședințele viitoare și istoricul sunt separate", async () => {
    porneste({ ...PANOU, sloturi: [slot(1), slot(2, { inceputLa: ieri }), slot(3, { status: "anulata" })] });
    const viitoare = (await screen.findByRole("heading", { name: "Ședințe viitoare" })).parentElement;
    expect(within(viitoare).getByText("Ședința 1")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Istoric" })).toBeInTheDocument();
    expect(screen.getByText("Anulată")).toBeInTheDocument();
  });

  it("fără ședințe spune asta", async () => {
    porneste();
    expect(await screen.findByText("Nicio ședință programată.")).toBeInTheDocument();
  });

  it("la o ședință hibridă se vede cum participă fiecare elev", async () => {
    porneste({ ...PANOU, sloturi: [slot(1, { tip: "hibrid" }, [{ nume: "Ana", status: "confirmata", participare: "online" }, { nume: "Radu", status: "confirmata", participare: "la_sala" }])] });
    expect(await screen.findByText(/Ana \(online\), Radu \(la sală\)/)).toBeInTheDocument();
    expect(screen.getByText(/2\/5 înscriși/)).toBeInTheDocument();
  });

  it("ședința online are link, fără detalii de loc", async () => {
    porneste({ ...PANOU, sloturi: [slot(1)] });
    expect(await screen.findByRole("button", { name: "Adaugă link" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /detalii loc/ })).not.toBeInTheDocument();
  });

  it("ședința față în față are detalii de loc, fără link", async () => {
    porneste({ ...PANOU, sloturi: [slot(1, { tip: "fata_in_fata", loc: { organizatie: "Liceul de Test", judet: "București" } })] });
    expect(await screen.findByRole("button", { name: "Adaugă detalii loc" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /link/ })).not.toBeInTheDocument();
  });

  it("linkul se adaugă direct din listă", async () => {
    const actualizat = { ...PANOU, sloturi: [slot(1, { linkIntalnire: "https://meet.example.ro/x" })] };
    const { u, cereri } = porneste({ ...PANOU, sloturi: [slot(1)] }, { "PUT /profesori/me/sloturi/1/link": actualizat });
    await u.click(await screen.findByRole("button", { name: "Adaugă link" }));
    await u.type(screen.getByLabelText("Linkul ședinței"), "https://meet.example.ro/x");
    await u.click(screen.getByRole("button", { name: "Salvează" }));
    expect(await screen.findByRole("link", { name: "Deschide linkul" })).toHaveAttribute("href", "https://meet.example.ro/x");
    expect(cereri.find((c) => c.metoda === "PUT").corp).toEqual({ linkIntalnire: "https://meet.example.ro/x" });
  });

  it("nota de sală greșită arată mesajul serverului", async () => {
    const { u } = porneste({ ...PANOU, sloturi: [slot(1, { tip: "hibrid" })] }, { "PUT /profesori/me/sloturi/1/nota": raspuns(400, { mesaj: "Textul are cel mult 200 de caractere." }) });
    await u.click(await screen.findByRole("button", { name: "Adaugă detalii loc" }));
    await u.type(screen.getByLabelText("Detalii despre loc"), "Sala 12");
    await u.click(screen.getByRole("button", { name: "Salvează" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("cel mult 200");
  });

  it("anularea cere confirmare și reîncarcă panoul", async () => {
    let anulata = false;
    const { u, cereri } = porneste(PANOU, {
      "GET /profesori/me": () => ({ ...PANOU, sloturi: [slot(1, anulata ? { status: "anulata" } : {})] }),
      "DELETE /profesori/me/sloturi/1": () => { anulata = true; return raspuns(204); },
    });
    await u.click(await screen.findByRole("button", { name: "Anulează" }));
    await u.click(screen.getByRole("button", { name: "Confirm anularea" }));
    expect(await screen.findByText("Anulată")).toBeInTheDocument();
    expect(cereri.filter((c) => c.metoda === "DELETE")).toHaveLength(1);
  });

  it("o anulare refuzată de server spune de ce și lasă ședința", async () => {
    const { u } = porneste({ ...PANOU, sloturi: [slot(1)] }, { "DELETE /profesori/me/sloturi/1": raspuns(500, { mesaj: "Nu s-a putut anula. Încearcă din nou." }) });
    await u.click(await screen.findByRole("button", { name: "Anulează" }));
    await u.click(screen.getByRole("button", { name: "Confirm anularea" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Nu s-a putut anula.");
    expect(screen.queryByText("Anulată")).not.toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Renunț" }));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("programarea trimite momentul în UTC și tipul ales", async () => {
    const { u, cereri } = porneste(PANOU, { "POST /profesori/me/sloturi": raspuns(201, PANOU) });
    await u.type(await screen.findByLabelText("Titlu"), "Recapitulare: Moara cu noroc");
    await u.selectOptions(screen.getByLabelText("Materie"), "2");
    const zi = new Date(Date.now() + 3 * 86400_000);
    const data = `${zi.getFullYear()}-${String(zi.getMonth() + 1).padStart(2, "0")}-${String(zi.getDate()).padStart(2, "0")}`;
    await u.clear(screen.getByLabelText("Data"));
    await u.type(screen.getByLabelText("Data"), data);
    await u.selectOptions(screen.getByLabelText("Ora"), "18:30");
    await u.click(screen.getByLabelText("Hibrid"));
    await u.selectOptions(screen.getByLabelText("Locul"), "1");
    await u.type(screen.getByLabelText(/Detalii despre loc/), "Sala 3");
    await u.click(screen.getByLabelText("Pro bono"));
    await u.click(screen.getByRole("button", { name: "Programează ședința" }));
    await waitFor(() => expect(cereri.find((c) => c.metoda === "POST")).toBeDefined());
    const corp = cereri.find((c) => c.metoda === "POST").corp;
    expect(corp).toMatchObject({ materieId: 2, titlu: "Recapitulare: Moara cu noroc", tip: "hibrid", locOrganizatieId: 1, notaLoc: "Sala 3", proBono: true, capacitate: 1 });
    expect(corp.inceputLa).toBe(new Date(`${data}T18:30:00`).toISOString());
  });

  it("o ședință peste mai puțin de 30 de minute e oprită înainte de server", async () => {
    const { u, cereri } = porneste();
    await u.type(await screen.findByLabelText("Titlu"), "Prea curând");
    const azi = new Date();
    await u.clear(screen.getByLabelText("Data"));
    await u.type(screen.getByLabelText("Data"), `${azi.getFullYear() - 1}-01-01`);
    screen.getByRole("button", { name: "Programează ședința" }).closest("form").noValidate = true;
    await u.click(screen.getByRole("button", { name: "Programează ședința" }));
    expect(screen.getByRole("alert")).toHaveTextContent("cel puțin 30 de minute");
    expect(cereri.some((c) => c.metoda === "POST")).toBe(false);
  });

  it("online nu are câmp de loc; față în față nu are link", async () => {
    const { u } = porneste();
    await screen.findByLabelText("Titlu");
    expect(screen.queryByLabelText("Locul")).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Link ședință/)).toBeInTheDocument();
    await u.click(screen.getByLabelText("Față în față"));
    expect(screen.getByLabelText("Locul")).toBeInTheDocument();
    expect(screen.queryByLabelText(/Link ședință/)).not.toBeInTheDocument();
  });

  it("tariful și onorariul sunt explicate", async () => {
    porneste();
    expect(await screen.findByText(/tariful fix al platformei \(50 RON \/ 50 min\)/)).toBeInTheDocument();
    expect(screen.getByLabelText("50 RON")).toBeChecked();
  });

  it("SloturileMele singur: o ședință pro bono e marcată", () => {
    apiFals({ "GET /profesori/me/loc": { optiuni: [] } });
    render(<SloturileMele panou={{ ...PANOU, sloturi: [slot(4, { proBono: true })] }} onActualizat={() => {}} />);
    expect(screen.getByText("Pro bono", { selector: ".tv-pastila" })).toBeInTheDocument();
  });
});

describe("panoul administratorului", () => {
  it("numără cererile și listează materiile", async () => {
    apiFals({
      "GET /admin/profesori": { elemente: [{ id: 1 }, { id: 2 }] },
      "GET /materii": { elemente: [{ id: 1, denumire: "Română", numarLectii: 1 }, { id: 2, denumire: "Fizică", numarLectii: 4 }] },
    });
    render(<AcasaAdmin utilizator={{ nume: "Admin Platformă" }} />);
    expect(await screen.findByText("așteaptă verificarea ta.")).toBeInTheDocument();
    expect(screen.getByText("1 lecție publicată")).toBeInTheDocument();
    expect(screen.getByText("4 lecții publicate")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Fizică/ })).toHaveAttribute("href", "/admin/continut?materie=2");
  });

  it("fără cereri spune asta, chiar dacă serverul a dat eroare", async () => {
    apiFals({ "GET /admin/profesori": raspuns(500), "GET /materii": { elemente: [] } });
    render(<AcasaAdmin utilizator={{ nume: "Admin" }} />);
    expect(await screen.findByText("Nicio cerere nouă.")).toBeInTheDocument();
  });
});
