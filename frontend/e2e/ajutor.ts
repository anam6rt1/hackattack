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
}

/** Signs in by API through the site (same cookie jar as the page): for tests that are not about signing in. */
export async function intraRapid(page: Page, identificator: string, parola = PAROLA) {
  const r = await page.request.post("/api/autentificare", { data: { identificator, parola }, headers: { "X-MarkAi": "1" } });
  expect(r.ok(), `login ${identificator}: ${r.status()}`).toBeTruthy();
}
