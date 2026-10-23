// Helpers for the browser tests: talking to the test API directly (setup, reading the codes the "jurnal" provider keeps),
// unique names, and signing in through the site.
import { expect, request, type APIRequestContext, type Page } from "@playwright/test";

export const API = "http://127.0.0.1:5081";
export const PAROLA = "Test-demo-2026!";
export const PAROLA_NOUA = "Parola-noua-E2E-2026";

/** A name no other test (or browser project) uses: tests share one test database. */
export function unic(prefix: string) {
  return `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

/** A phone number no other test uses. */
export function telefonUnic() {
  return "07" + String(Math.floor(Math.random() * 1e8)).padStart(8, "0");
}

/** A client of the test API signed in as `username` (cookies kept), with the site's CSRF header. */
export async function clientApi(username?: string, parola = PAROLA): Promise<APIRequestContext> {
  const c = await request.newContext({ baseURL: API, extraHTTPHeaders: { "X-MarkAi": "1" } });
  if (username) {
    const r = await c.post("/autentificare", { data: { identificator: username, parola } });
    expect(r.ok(), `login ${username}: ${r.status()} ${await r.text()}`).toBeTruthy();
  }
  return c;
}

let admin: APIRequestContext | null = null;
const adminApi = async () => (admin ??= await clientApi("admin"));

/**
 * The last code sent to `catre` (an email or a +40 phone number), read from the admin's "Mesaje trimise" list (the test API
 * sends nothing for real; it keeps the text). Waits until a message newer than `dupaId` arrives.
 */
export async function ultimulCod(catre: string, dupaId = 0): Promise<string> {
  const a = await adminApi();
  let gasit: string | undefined;
  await expect
    .poll(async () => {
      const r = await (await a.get("/admin/mesaje")).json();
      const m = r.elemente.find((x: { catre: string; id: number; continut: string | null }) =>
        x.catre.toLowerCase() === catre.toLowerCase() && x.id > dupaId && /\b\d{6}\b/.test(x.continut ?? ""));
      gasit = m?.continut.match(/\b\d{6}\b/)[0];
      return gasit;
    }, { message: `a code for ${catre}`, timeout: 15_000 })
    .toBeTruthy();
  return gasit!;
}

/** The id of the newest message so far (to wait for one sent after it). */
export async function ultimulMesaj(): Promise<number> {
  const r = await (await (await adminApi()).get("/admin/mesaje")).json();
  return r.elemente[0]?.id ?? 0;
}

/** A confirmed student account, made through the API (fast); returns its username. */
export async function elevNou(prefix = "elev", extra: Record<string, unknown> = {}): Promise<string> {
  const username = unic(prefix);
  const email = `${username}@example.invalid`;
  const c = await clientApi();
  const r = await c.post("/autentificare/inregistrare", { data: { nume: `Elev ${username}`, username, email, parola: PAROLA, ...extra } });
  expect(r.status()).toBe(202);
  const ok = await c.post("/autentificare/confirma-email", { data: { email, cod: await ultimulCod(email) } });
  expect(ok.ok()).toBeTruthy();
  await c.dispose();
  return username;
}

/** Signs in through the site's form and waits for the account menu. */
export async function intra(page: Page, identificator: string, parola = PAROLA) {
  await page.goto("/");
  await page.getByLabel("Email sau utilizator").fill(identificator);
  await page.getByLabel("Parolă", { exact: true }).fill(parola);
  await page.getByRole("button", { name: "Intră în cont" }).click();
  await expect(page.getByRole("button", { name: "Intră în cont" })).toBeHidden();
  // WebKit stores the cookie from the login response a moment after the page shows the account;
  // a navigation in that gap would arrive logged out.
  await expect.poll(async () => (await page.context().cookies()).some((c) => c.name === "markai_sesiune")).toBe(true);
}

/** Signs in by API through the site (same cookie jar as the page): for tests that are not about signing in. */
export async function intraRapid(page: Page, identificator: string, parola = PAROLA) {
  const r = await page.request.post("/api/autentificare", { data: { identificator, parola }, headers: { "X-MarkAi": "1" } });
  expect(r.ok(), `login ${identificator}: ${r.status()}`).toBeTruthy();
}

/** Small files for upload forms (a professor's photo and qualification proof). */
export const PNG = Buffer.from("89504e470d0a1a0a0000000d49484452", "hex");
export const PDF = Buffer.from("%PDF-1.4\n%test\n");

/**
 * A professor approved in Liceul de Test (by director), teaching Romanian, with one upcoming online session so the Profesori list
 * shows them. The name gets the unique username after it (the browsers share one test database). Returns username, name and id.
 */
export async function profesorNou(prefix = "prof", prenume = "Profesor Nou"): Promise<{ username: string; nume: string; id: number }> {
  const username = unic(prefix);
  const nume = `${prenume} ${username}`;
  const email = `${username}@example.invalid`;
  const c = await clientApi();
  const liceu = (await (await c.get("/organizatii?q=" + encodeURIComponent("Liceul de Test"))).json()).elemente[0].id;
  const formular = {
    Nume: nume, Username: username, Email: email, Parola: PAROLA, Telefon: telefonUnic(), Descriere: "Predau română.", MaterieIds: "1",
    JudetId: "40", OrganizatieIds: String(liceu), Poza: { name: "p.png", mimeType: "image/png", buffer: PNG },
    Dovada: { name: "d.pdf", mimeType: "application/pdf", buffer: PDF },
  };
  expect((await c.post("/autentificare/inregistrare-profesor", { multipart: formular })).status()).toBe(202);
  expect((await c.post("/autentificare/confirma-email", { data: { email, cod: await ultimulCod(email) } })).ok()).toBeTruthy();
  await c.dispose();
  const director = await clientApi("director");
  const p = await clientApi(username);
  const id = (await (await p.get("/utilizatori/me")).json()).id;
  expect((await director.post(`/organizatia-mea/membri/${id}/aprobare`)).ok()).toBeTruthy();
  const sedinta = await p.post("/profesori/me/sloturi", { data: { materieId: 1, titlu: `Ședința lui ${nume}`, capacitate: 3, proBono: true,
    inceputLa: new Date(Date.now() + 3 * 86400000).toISOString(), linkIntalnire: "https://meet.example.invalid/x" } });
  expect(sedinta.status(), await sedinta.text()).toBe(201);
  await Promise.all([director.dispose(), p.dispose()]);
  return { username, nume, id };
}

