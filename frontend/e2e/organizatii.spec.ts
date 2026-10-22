import { expect, test, type Browser, type Page } from "@playwright/test";
import { elevNou, intra, intraRapid, PAROLA, PDF, PNG, telefonUnic, ultimulCod, unic } from "./ajutor";


async function confirmaEmail(page: Page, email: string) {
  await expect(page.getByRole("heading", { name: "Confirmă adresa de email" })).toBeVisible();
  await page.getByLabel(/Codul de 6 cifre/).fill(await ultimulCod(email));
  await page.getByRole("button", { name: "Confirmă și intră în cont" }).click();
}

/** An organisation applies through the site; the platform admin approves it in /admin/organizatii. */
async function organizatieAprobata(browser: Browser) {
  const page = await browser.newPage();
  const username = unic("org");
  const nume = `Școala E2E ${username.slice(-5)}`;
  await page.goto("/");
  await page.getByRole("tab", { name: "Organizație" }).click();
  await page.getByLabel("Numele organizației (școală, centru)").fill(nume);
  await page.getByLabel("Adresa").fill("Str. Școlii 7");
  await page.getByRole("combobox", { name: "Județul" }).selectOption({ label: "București" });
  await page.getByLabel("Numele tău (administratorul organizației)").fill("Olga Ionescu");
  await page.getByLabel("Utilizator").fill(username);
  await page.getByLabel("Telefon").fill(telefonUnic());
  await page.getByLabel("Email").fill(`${username}@example.invalid`);
  await page.getByLabel("Parolă (minim 8 caractere)").fill(PAROLA);
  await page.getByRole("button", { name: "Trimite cererea" }).click();
  await confirmaEmail(page, `${username}@example.invalid`);
  await expect(page).toHaveURL(/\/in-asteptare$/);
  await expect(page.getByText(`Un administrator al platformei verifică organizația „${nume}”`)).toBeVisible();

  const admin = await browser.newPage();
  await intraRapid(admin, "admin");
  await admin.goto("/admin/organizatii");
  const card = admin.locator("article").filter({ hasText: nume });
  await card.getByRole("button", { name: "Aprobă organizația" }).click();
  await expect(card).toBeHidden();
  await admin.close();

  await page.getByRole("button", { name: "Verifică din nou" }).click();
  await expect(page).toHaveURL(/\/$/);
  return { page, nume, username };
}

test("o organizație nouă e aprobată de administratorul platformei", async ({ browser }) => {
  const { page, nume } = await organizatieAprobata(browser);
  await page.goto("/organizatie");
  await expect(page.getByRole("heading", { name: nume })).toBeVisible();
  await expect(page.getByText("Nicio cerere în așteptare.", { exact: false })).toBeVisible();
  await page.close();
});

test("un profesor aplică la organizație, iar administratorul ei îl aprobă", async ({ browser, page }) => {
  const org = await organizatieAprobata(browser);
  const username = unic("prof");
  const email = `${username}@example.invalid`;
  await page.goto("/");
  await page.getByRole("tab", { name: "Sunt profesor" }).click();
  await page.getByLabel("Nume și prenume").fill("Mihai Profesorescu");
  await page.getByLabel("Utilizator").fill(username);
  await page.getByLabel("Telefon").fill(telefonUnic());
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Parolă (minim 8 caractere)").fill(PAROLA);
  await page.getByLabel("Limba și literatura română").check();
  await page.getByLabel("Despre tine (apare pe profilul public)").fill("Predau română de 10 ani.");
  await page.getByLabel("Fotografie").setInputFiles({ name: "poza.png", mimeType: "image/png", buffer: PNG });
  await page.getByLabel("Dovada calificării").setInputFiles({ name: "diploma.pdf", mimeType: "application/pdf", buffer: PDF });
  await page.getByRole("combobox", { name: "Județul" }).selectOption({ label: "București" });
  await page.getByRole("group", { name: "Organizațiile în care predai" }).getByLabel(new RegExp(org.nume)).check();
  await page.getByRole("button", { name: "Trimite cererea" }).click();
  await confirmaEmail(page, email);
  await expect(page).toHaveURL(/\/in-asteptare$/);
  await expect(page.locator("li").filter({ hasText: org.nume })).toContainText("în așteptare");

  await org.page.goto("/organizatie");
  const cerere = org.page.locator("article").filter({ hasText: "Mihai Profesorescu" });
  await expect(cerere.getByRole("link", { name: "Descarcă dovada calificării" })).toBeVisible();
  await cerere.getByRole("button", { name: "Aprobă" }).click();
  await expect(cerere).toBeHidden();

  await page.getByRole("button", { name: "Verifică din nou" }).click();
  await expect(page.getByRole("heading", { name: "Bine ai revenit, Mihai!" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Meniul principal" }).getByRole("link", { name: "Ședințele mele" })).toBeVisible();
  await org.page.close();
});

test("un profesor al organizației confirmă un elev care cere să intre", async ({ page, browser }) => {
  // irina teaches at "Liceul de Test" in the fixtures.
  const elev = await elevNou("cerere", { organizatieId: 1 });
  await intraRapid(page, "irina");
  await page.goto("/organizatie");
  await expect(page.getByRole("heading", { name: "Confirmă elevii" })).toBeVisible();
  const cerere = page.locator("article").filter({ hasText: elev });
  await cerere.getByRole("button", { name: "Aprobă" }).click();
  await expect(cerere).toBeHidden();

  const e = await browser.newPage();
  await intra(e, elev);
  await e.goto("/setari");
  await e.getByRole("button", { name: "Organizație și loc" }).click();
  await expect(e.locator("li").filter({ hasText: "Liceul de Test" })).toContainText("membru");
  await e.close();
});

test("respingerea unui elev cere un motiv", async ({ page }) => {
  const elev = await elevNou("respins", { organizatieId: 1 });
  await intraRapid(page, "director");
  await page.goto("/organizatie");
  const cerere = page.locator("article").filter({ hasText: elev });
  await cerere.getByRole("button", { name: "Respinge" }).click();
  await expect(cerere.getByRole("button", { name: "Respinge" })).toBeDisabled();
  await cerere.getByLabel("Motivul respingerii").fill("Nu ești elev la noi.");
  await cerere.getByRole("button", { name: "Respinge" }).click();
  await expect(cerere).toBeHidden();
});

test("administratorul organizației respinge un profesor, iar acesta vede motivul", async ({ browser, page }) => {
  const org = await organizatieAprobata(browser);
  const username = unic("respins");
  const c = await (await import("./ajutor")).clientApi();
  const formular = {
    Nume: "Radu Respins", Username: username, Email: `${username}@example.invalid`, Parola: PAROLA, Telefon: telefonUnic(),
    Descriere: "Predau.", MaterieIds: "1", JudetId: "40",
    OrganizatieIds: String((await (await c.get("/organizatii?q=" + encodeURIComponent(org.nume))).json()).elemente[0].id),
    Poza: { name: "p.png", mimeType: "image/png", buffer: PNG }, Dovada: { name: "d.pdf", mimeType: "application/pdf", buffer: PDF },
  };
  expect((await c.post("/autentificare/inregistrare-profesor", { multipart: formular })).status()).toBe(202);
  expect((await c.post("/autentificare/confirma-email", { data: { email: `${username}@example.invalid`, cod: await ultimulCod(`${username}@example.invalid`) } })).ok()).toBeTruthy();
  await c.dispose();

  await org.page.goto("/organizatie");
  const cerere = org.page.locator("article").filter({ hasText: "Radu Respins" });
  await cerere.getByRole("button", { name: "Respinge" }).click();
  await cerere.getByLabel("Motivul respingerii").fill("Nu predăm această materie.");
  await cerere.getByRole("button", { name: "Respinge" }).click();
  await expect(cerere).toBeHidden();

  await intra(page, username);
  await expect(page).toHaveURL(/\/in-asteptare$/);
  await expect(page.getByRole("heading", { name: "Cererea ta nu a fost aprobată." })).toBeVisible();
  await expect(page.getByText("Nu predăm această materie.").first()).toBeVisible();
  await org.page.close();
});

test("administratorul scoate un elev din organizație, cu confirmarea site-ului", async ({ page }) => {
  const elev = await elevNou("scos", { organizatieId: 1 });
  await intraRapid(page, "director");
  await page.goto("/organizatie");
  await page.locator("article").filter({ hasText: elev }).getByRole("button", { name: "Aprobă" }).click();
  await page.getByRole("tab", { name: "Elevi" }).click();
  const membru = page.locator("article").filter({ hasText: elev });
  await membru.getByRole("button", { name: "Scoate din organizație" }).click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toHaveAccessibleName("Scoți persoana din organizație?");
  await dialog.getByRole("button", { name: "Anulează" }).click();
  await expect(membru).toBeVisible();
  await membru.getByRole("button", { name: "Scoate din organizație" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Scoate" }).click();
  await expect(membru).toBeHidden();
});
