// Before any test: make sure the site on :3001 talks to the TEST API (never the real one), then add the content the tests use.
import { readFileSync } from "node:fs";
import { type APIRequestContext, expect } from "@playwright/test";
import { clientApi } from "./ajutor";

export default async function pregatire() {
  // The test database has only two counties (Alba, București); the real one has all 42.
  const judete = await (await fetch("http://127.0.0.1:3001/api/judete")).json();
  if (judete.elemente.length !== 2) throw new Error("The site on :3001 is not using the test API (expected 2 counties). Stop and check API_INTERN_URL.");
  const manifest = readFileSync(".next-e2e/routes-manifest.json", "utf8");
  if (!manifest.includes("127.0.0.1:5081")) throw new Error(".next-e2e does not proxy /api to the test API.");

  // A grade IX Lecție for the grade filter, an organisation-only Lecție of irina's, and a public Lecție with two chapters,
  // six multiple-choice questions (the right option is always "Răspunsul bun") and two estimates, for the quiz and Triviador.
  const admin = await clientApi("admin");
  const irina = await clientApi("irina");
  // Outside CI the servers may still be up from an earlier run (reuseExistingServer): use what that run created.
  const existenta = async (c: APIRequestContext, titlu: string) =>
    (await (await c.get("/continut/parcursuri?materieId=1")).json()).elemente.find((p: { titlu: string }) => p.titlu === titlu)?.id as number | undefined;
  const ix = (await existenta(admin, "Ion, clasa a IX-a (E2E)"))
    ?? (await (await admin.post("/parcursuri", { data: { materieId: 1, titlu: "Ion, clasa a IX-a (E2E)", clasa: "IX", vizibilitate: "public", organizatieId: null } })).json()).id;
  const privata = (await existenta(irina, "Doar pentru liceu (E2E)"))
    ?? (await (await irina.post("/parcursuri", { data: { materieId: 1, titlu: "Doar pentru liceu (E2E)", clasa: "XII", vizibilitate: "organizatie", organizatieId: 1 } })).json()).id;
  // Global setup's environment reaches the test workers.
  process.env.E2E_LECTIE_IX = String(ix);
  process.env.E2E_LECTIE_PRIVATA = String(privata);
  await irina.dispose();
  const gasita = await existenta(admin, "Luceafărul (E2E)");
  if (gasita) {
    process.env.E2E_LECTIE = String(gasita);
    await admin.dispose();
    return;
  }
  const l = await (await admin.post("/parcursuri", { data: { materieId: 1, titlu: "Luceafărul (E2E)", clasa: "XII", vizibilitate: "public", organizatieId: null } })).json();
  for (const [titlu, text] of [["Geneza poemului", "„Luceafărul” a apărut în 1883."], ["Cătălina și Hyperion", "Fata de împărat se numește Cătălina."]])
    expect((await admin.post("/cursuri", { data: { parcursId: l.id, titlu, capitol: "Romantismul", materiale: [{ titlu: "Notițe", text }] } })).ok()).toBeTruthy();
  for (let i = 1; i <= 6; i++)
    expect((await admin.post(`/parcursuri/${l.id}/intrebari`, { data: {
      text: `Întrebarea ${i} despre Luceafărul`, explicatie: `Explicația ${i}.`, activa: true,
      variante: [{ text: "Altă variantă A", corecta: false }, { text: "Răspunsul bun", corecta: true }, { text: "Altă variantă B", corecta: false }, { text: "Altă variantă C", corecta: false }],
    } })).ok()).toBeTruthy();
  for (const [text, an] of [["În ce an a apărut „Luceafărul”?", 1883], ["În ce an s-a născut Eminescu?", 1850]])
    expect((await admin.post(`/parcursuri/${l.id}/intrebari`, { data: { text, tip: "estimare", raspunsNumeric: an, activa: true } })).ok()).toBeTruthy();
  // A PDF on the Lecție (opens in the browser).
  const pdf = Buffer.from("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");
  expect((await admin.post(`/parcursuri/${l.id}/fisiere`, { multipart: { fisier: { name: "Luceafărul - rezumat.pdf", mimeType: "application/pdf", buffer: pdf } } })).status()).toBe(201);
  await admin.dispose();
  process.env.E2E_LECTIE = String(l.id);
}
