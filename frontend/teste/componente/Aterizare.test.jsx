import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import Aterizare from "@/components/acasa/Aterizare";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals, raspuns, router } from "../ajutor";

const BAZA = {
  "GET /utilizatori/me": raspuns(401),
  "GET /judete": { elemente: [{ id: 40, nume: "București" }] },
  "GET /organizatii": { elemente: [{ id: 1, nume: "Liceul de Test", judet: "București", localitate: "Sectorul 1" }] },
  "GET /materii": { elemente: [{ id: 1, denumire: "Limba și literatura română" }, { id: 2, denumire: "Matematică" }] },
};

function porneste(rute = {}) {
  const cereri = apiFals({ ...BAZA, ...rute });
  const u = userEvent.setup();
  render(<SesiuneProvider><Aterizare /></SesiuneProvider>);
  return { u, cereri };
}

const camp = (eticheta) => screen.getByLabelText(eticheta);

/** Opens a picker's window, ticks the given options and closes it. */
async function alege(u, buton, ...optiuni) {
  await u.click(screen.getByRole("button", { name: buton }));
  const fereastra = screen.getByRole("dialog");
  for (const o of optiuni) await u.click(await within(fereastra).findByRole("checkbox", { name: o }));
  await u.click(within(fereastra).getByRole("button", { name: "Gata" }));
}

async function intra(u, identificator = "ana", parola = "Parola-buna-1") {
  await u.type(camp("Email sau utilizator"), identificator);
  await u.type(camp("Parolă"), parola);
  await u.click(screen.getByRole("button", { name: "Intră în cont" }));
}

describe("intrarea în cont", () => {
  it("trimite identificatorul și parola", async () => {
    const { u, cereri } = porneste({ "POST /autentificare": { id: 7, rol: "elev" } });
    await intra(u, "ana@example.ro");
    await waitFor(() => expect(cereri.find((c) => c.cale === "/autentificare")?.corp).toEqual({ identificator: "ana@example.ro", parola: "Parola-buna-1" }));
  });

  it("parola greșită arată mesajul serverului", async () => {
    const { u } = porneste({ "POST /autentificare": raspuns(401, { mesaj: "Email/utilizator sau parolă greșită." }) });
    await intra(u);
    expect(await screen.findByRole("alert")).toHaveTextContent("Email/utilizator sau parolă greșită.");
    expect(screen.getByRole("button", { name: "Intră în cont" })).toBeEnabled();
  });

  it("butonul e blocat cât timp se intră", async () => {
    const { u } = porneste({ "POST /autentificare": () => new Promise(() => {}) });
    await intra(u);
    expect(screen.getByRole("button", { name: "Se intră..." })).toBeDisabled();
  });

  it("profesorul neaprobat e dus la /in-asteptare", async () => {
    const { u } = porneste({ "POST /autentificare": { id: 3, rol: "profesor", stareProfesor: "in_asteptare" } });
    await intra(u, "paul");
    await waitFor(() => expect(router.push).toHaveBeenCalledWith("/in-asteptare"));
  });

  it("contul neconfirmat cere codul de pe email", async () => {
    const { u } = porneste({
      "POST /autentificare": raspuns(403, { cod: "email_neconfirmat", email: "ana@example.ro", mesaj: "Contul nu este confirmat încă. Scrie codul primit pe email." }),
    });
    await intra(u);
    expect(await screen.findByRole("heading", { name: "Confirmă adresa de email" })).toBeInTheDocument();
    expect(screen.getByText("Contul nu este confirmat încă. Scrie codul primit pe email.")).toBeInTheDocument();
    expect(camp("Codul de 6 cifre trimis pe ana@example.ro")).toBeInTheDocument();
  });

  it("taburile arată formularele", async () => {
    const { u } = porneste();
    await u.click(screen.getByRole("tab", { name: "Cont de elev" }));
    expect(screen.getByRole("button", { name: "Creează contul de elev" })).toBeInTheDocument();
    await u.click(screen.getByRole("tab", { name: "Sunt profesor" }));
    expect(screen.getByRole("button", { name: "+ Adaugă materie" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "+ Adaugă organizație" })).toBeInTheDocument();
    await u.click(screen.getByRole("tab", { name: "Organizație" }));
    expect(camp("Numele organizației (școală, centru)")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Organizație" })).toHaveAttribute("aria-selected", "true");
  });
});

async function laConfirmare(rute = {}) {
  const r = porneste({
    "POST /autentificare/inregistrare": raspuns(202, { email: "ana@example.ro", mesaj: "Ți-am trimis un cod de confirmare pe ana@example.ro." }),
    ...rute,
  });
  await r.u.click(screen.getByRole("tab", { name: "Cont de elev" }));
  await r.u.type(camp("Nume și prenume"), "Ana Țăranu");
  await r.u.type(camp("Utilizator"), "ana.t");
  await r.u.type(camp("Email"), "ana@example.ro");
  await r.u.type(camp("Parolă (minim 8 caractere)"), "Parola-buna-1");
  await r.u.click(screen.getByRole("button", { name: "Creează contul de elev" }));
  await screen.findByRole("heading", { name: "Confirmă adresa de email" });
  return r;
}

describe("înregistrarea elevului și codul de pe email", () => {
  it("câmpurile goale pleacă drept null", async () => {
    const { cereri } = await laConfirmare();
    expect(cereri.find((c) => c.cale === "/autentificare/inregistrare").corp).toEqual({
      nume: "Ana Țăranu", username: "ana.t", email: "ana@example.ro", parola: "Parola-buna-1",
      clasa: null, judetId: null, localitateId: null, organizatieId: null,
    });
  });

  it("clasa și organizația aleasă pleacă ca valori", async () => {
    const cereri = apiFals({ ...BAZA, "POST /autentificare/inregistrare": raspuns(202, { email: "b@example.ro", mesaj: "ok" }) });
    const u = userEvent.setup();
    render(<SesiuneProvider><Aterizare /></SesiuneProvider>);
    await u.click(screen.getByRole("tab", { name: "Cont de elev" }));
    await u.type(camp("Nume și prenume"), "Bogdan");
    await u.type(camp("Utilizator"), "bogdan");
    await u.type(camp("Email"), "b@example.ro");
    await u.type(camp("Parolă (minim 8 caractere)"), "Parola-buna-1");
    await u.selectOptions(camp("Clasa (opțional)"), "XI");
    await u.type(screen.getByRole("combobox", { name: "Organizația ta (opțional)" }), "liceul");
    await u.click(await screen.findByRole("option", { name: "Liceul de Test · Sectorul 1" }));
    await u.click(screen.getByRole("button", { name: "Creează contul de elev" }));
    await waitFor(() => expect(cereri.find((c) => c.cale === "/autentificare/inregistrare")?.corp).toMatchObject({ clasa: "XI", organizatieId: 1 }));
  });

  it("codul bun intră în cont", async () => {
    const { u, cereri } = await laConfirmare({ "POST /autentificare/confirma-email": { id: 9, rol: "elev" } });
    await u.type(camp("Codul de 6 cifre trimis pe ana@example.ro"), "123456");
    await u.click(screen.getByRole("button", { name: "Confirmă și intră în cont" }));
    await waitFor(() => expect(cereri.find((c) => c.cale === "/autentificare/confirma-email")?.corp).toEqual({ email: "ana@example.ro", cod: "123456" }));
  });

  it("codul greșit arată mesajul și lasă reîncercarea", async () => {
    const { u } = await laConfirmare({ "POST /autentificare/confirma-email": raspuns(400, { mesaj: "Codul nu este corect sau a expirat." }) });
    await u.type(camp("Codul de 6 cifre trimis pe ana@example.ro"), "000000");
    await u.click(screen.getByRole("button", { name: "Confirmă și intră în cont" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Codul nu este corect sau a expirat.");
    expect(screen.getByRole("button", { name: "Confirmă și intră în cont" })).toBeEnabled();
  });

  afterEach(() => vi.useRealTimers());

  it("retrimiterea are o pauză de 60 de secunde", async () => {
    const { u } = await laConfirmare({ "POST /autentificare/retrimite-confirmare": { mesaj: "Ți-am trimis un cod nou." } });
    vi.useFakeTimers({ shouldAdvanceTime: true });
    await u.click(screen.getByRole("button", { name: "Retrimite codul" }));
    expect(await screen.findByText("Ți-am trimis un cod nou. Codurile vechi nu mai sunt valabile.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Retrimite peste 60 s/ })).toBeDisabled();
    // One timer per second, re-armed after each render.
    const secunde = async (n) => { for (let i = 0; i < n; i++) await act(() => vi.advanceTimersByTimeAsync(1000)); };
    await secunde(3);
    expect(screen.getByRole("button", { name: "Retrimite peste 57 s" })).toBeDisabled();
    await secunde(57);
    expect(screen.getByRole("button", { name: "Retrimite codul" })).toBeEnabled();
  });

  it("„Înapoi” revine la formulare", async () => {
    const { u } = await laConfirmare();
    await u.click(screen.getByRole("button", { name: "Înapoi" }));
    expect(screen.getByRole("tab", { name: "Intră" })).toBeInTheDocument();
  });

  it("emailul care nu pleacă spune ce să faci", async () => {
    porneste({ "POST /autentificare/inregistrare": raspuns(202, { email: "c@example.ro", mesaj: "Contul a fost creat, dar emailul cu codul nu a putut fi trimis acum. Apasă „Retrimite codul” peste câteva minute." }) });
    const u = userEvent.setup();
    await u.click(screen.getAllByRole("tab", { name: "Cont de elev" })[0]);
    await u.type(camp("Nume și prenume"), "C");
    await u.type(camp("Utilizator"), "c");
    await u.type(camp("Email"), "c@example.ro");
    await u.type(camp("Parolă (minim 8 caractere)"), "Parola-buna-1");
    await u.click(screen.getByRole("button", { name: "Creează contul de elev" }));
    expect(await screen.findByText(/nu a putut fi trimis acum/)).toBeInTheDocument();
  });
});

async function laParolaUitata(rute = {}) {
  const r = porneste({ "POST /autentificare/parola-uitata": { mesaj: "Dacă există un cont cu aceste date, i-am trimis un cod." }, ...rute });
  await r.u.click(screen.getByRole("button", { name: "Am uitat parola" }));
  return r;
}

describe("parola uitată", () => {
  it("cere codul pe email, implicit", async () => {
    const { u, cereri } = await laParolaUitata();
    await u.type(camp("Email sau utilizator"), "ana");
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    await screen.findByText("Dacă există un cont cu aceste date, i-am trimis un cod.");
    expect(cereri.find((c) => c.cale === "/autentificare/parola-uitata").corp).toEqual({ identificator: "ana", canal: "email" });
  });

  it("poate cere codul prin SMS", async () => {
    const { u, cereri } = await laParolaUitata();
    await u.type(camp("Email sau utilizator"), "ana");
    await u.click(screen.getByLabelText(/Prin SMS/));
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    await screen.findByLabelText("Codul de 6 cifre");
    expect(cereri.find((c) => c.cale === "/autentificare/parola-uitata").corp.canal).toBe("sms");
  });

  // The bug found in Phase 2: without distinct keys React reused the "Email sau utilizator" input as "Cod", text included.
  it("câmpul pentru cod pornește gol, nu cu textul scris înainte", async () => {
    const { u } = await laParolaUitata();
    await u.type(camp("Email sau utilizator"), "ana@example.ro");
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    expect(await screen.findByLabelText("Codul de 6 cifre")).toHaveValue("");
    expect(camp("Parola nouă (minim 8 caractere)")).toHaveValue("");
  });

  it("parolele diferite sunt oprite înainte de server", async () => {
    const { u, cereri } = await laParolaUitata();
    await u.type(camp("Email sau utilizator"), "ana");
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    await u.type(await screen.findByLabelText("Codul de 6 cifre"), "123456");
    await u.type(camp("Parola nouă (minim 8 caractere)"), "Parola-noua-1");
    await u.type(camp("Confirmă parola nouă"), "Parola-noua-2");
    await u.click(screen.getByRole("button", { name: "Schimbă parola" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Parolele nu sunt la fel.");
    expect(cereri.some((c) => c.cale === "/autentificare/parola-noua")).toBe(false);
  });

  it("schimbarea reușită revine la intrare cu mesajul serverului", async () => {
    const { u, cereri } = await laParolaUitata({ "POST /autentificare/parola-noua": { mesaj: "Parola a fost schimbată. Intră în cont cu parola nouă." } });
    await u.type(camp("Email sau utilizator"), "ana");
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    await u.type(await screen.findByLabelText("Codul de 6 cifre"), "123456");
    await u.type(camp("Parola nouă (minim 8 caractere)"), "Parola-noua-1");
    await u.type(camp("Confirmă parola nouă"), "Parola-noua-1");
    await u.click(screen.getByRole("button", { name: "Schimbă parola" }));
    expect(await screen.findByRole("status")).toHaveTextContent("Parola a fost schimbată.");
    expect(cereri.find((c) => c.cale === "/autentificare/parola-noua").corp).toEqual({ identificator: "ana", cod: "123456", parola: "Parola-noua-1" });
  });

  it("codul greșit arată mesajul", async () => {
    const { u } = await laParolaUitata({ "POST /autentificare/parola-noua": raspuns(400, { mesaj: "Codul nu este corect sau a expirat. Cere un cod nou." }) });
    await u.type(camp("Email sau utilizator"), "ana");
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    await u.type(await screen.findByLabelText("Codul de 6 cifre"), "999999");
    await u.type(camp("Parola nouă (minim 8 caractere)"), "Parola-noua-1");
    await u.type(camp("Confirmă parola nouă"), "Parola-noua-1");
    await u.click(screen.getByRole("button", { name: "Schimbă parola" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Cere un cod nou.");
  });

  it("„Cere alt cod” revine la primul pas, cu identificatorul păstrat", async () => {
    const { u } = await laParolaUitata();
    await u.type(camp("Email sau utilizator"), "ana");
    await u.click(screen.getByRole("button", { name: "Trimite codul" }));
    await u.click(await screen.findByRole("button", { name: "Cere alt cod" }));
    expect(camp("Email sau utilizator")).toHaveValue("ana");
  });

  it("„Înapoi la intrare” nu arată niciun mesaj", async () => {
    const { u } = await laParolaUitata();
    await u.click(screen.getByRole("button", { name: "Înapoi la intrare" }));
    expect(screen.getByRole("button", { name: "Intră în cont" })).toBeInTheDocument();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});

describe("înregistrarea profesorului și a organizației", () => {
  it("fără materie, profesorul e oprit cu un mesaj", async () => {
    const { u, cereri } = porneste();
    await u.click(screen.getByRole("tab", { name: "Sunt profesor" }));
    const form = screen.getByRole("button", { name: "Trimite cererea" }).closest("form");
    form.noValidate = true;
    await u.click(screen.getByRole("button", { name: "Trimite cererea" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Alege cel puțin o materie pe care o predai.");
    expect(cereri.some((c) => c.cale === "/autentificare/inregistrare-profesor")).toBe(false);
  });

  it("fără organizație, profesorul e oprit cu un mesaj", async () => {
    const { u } = porneste();
    await u.click(screen.getByRole("tab", { name: "Sunt profesor" }));
    await alege(u, "+ Adaugă materie", "Matematică");
    screen.getByRole("button", { name: "Trimite cererea" }).closest("form").noValidate = true;
    await u.click(screen.getByRole("button", { name: "Trimite cererea" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Alege cel puțin o organizație în care predai.");
  });

  it("materiile și organizațiile alese în ferestre pleacă în formular", async () => {
    const { u, cereri } = porneste({ "POST /autentificare/inregistrare-profesor": raspuns(202, { email: "p@example.ro", mesaj: "ok" }) });
    await u.click(screen.getByRole("tab", { name: "Sunt profesor" }));
    await alege(u, "+ Adaugă materie", "Matematică", "Limba și literatura română");
    await alege(u, "+ Adaugă organizație", /Liceul de Test/);
    expect(screen.getByRole("button", { name: "Scoate Matematică" })).toBeInTheDocument();
    screen.getByRole("button", { name: "Trimite cererea" }).closest("form").noValidate = true;
    await u.click(screen.getByRole("button", { name: "Trimite cererea" }));
    await waitFor(() => expect(cereri.some((c) => c.cale === "/autentificare/inregistrare-profesor")).toBe(true));
    const corp = cereri.find((c) => c.cale === "/autentificare/inregistrare-profesor").corp;
    expect([corp.getAll("materieIds"), corp.getAll("organizatieIds")]).toEqual([["2", "1"], ["1"]]);
  });

  it("organizația trimite numerele locului ca numere", async () => {
    const { u, cereri } = porneste({ "POST /autentificare/inregistrare-organizatie": raspuns(202, { email: "o@example.ro", mesaj: "ok" }) });
    await u.click(screen.getByRole("tab", { name: "Organizație" }));
    await u.type(camp("Numele organizației (școală, centru)"), "Școala Gimnazială nr. 1");
    await u.type(camp("Adresa"), "Str. Școlii 1");
    await u.selectOptions(await screen.findByRole("combobox", { name: "Județul" }), "40");
    await u.type(camp("Numele tău (administratorul organizației)"), "Olga");
    await u.type(camp("Utilizator"), "olga");
    await u.type(camp("Telefon"), "0744 000 999");
    await u.type(camp("Email"), "o@example.ro");
    await u.type(camp("Parolă (minim 8 caractere)"), "Parola-buna-1");
    await u.click(screen.getByRole("button", { name: "Trimite cererea" }));
    await waitFor(() => expect(cereri.find((c) => c.cale === "/autentificare/inregistrare-organizatie")?.corp).toMatchObject({
      numeOrganizatie: "Școala Gimnazială nr. 1", judetId: 40, localitateId: null, telefon: "0744 000 999",
    }));
    expect(await screen.findByRole("heading", { name: "Confirmă adresa de email" })).toBeInTheDocument();
  });

  it("textul de prezentare spune cinstit că apelarea vine în curând", () => {
    porneste();
    expect(within(screen.getByText("Linia AI, fără internet").parentElement).getByText(/va fi disponibilă în curând/)).toBeInTheDocument();
  });
});
