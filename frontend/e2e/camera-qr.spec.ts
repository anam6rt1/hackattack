import { expect, test } from "@playwright/test";
import { elevNou, intra, PAROLA, ultimulCod, unic } from "./ajutor";

// The QR code of a Triviador room is a link to /joc/COD; someone without an account opens it on their phone.
async function cameraNoua(browser: import("@playwright/test").Browser) {
  const gazda = await browser.newPage();
  await intra(gazda, await elevNou("gazda"));
  await gazda.goto("/joc");
  await gazda.getByRole("button", { name: "Creează o cameră" }).click();
  await expect(gazda).toHaveURL(/\/joc\/[A-Z0-9]{6}$/);
  return { gazda, cod: gazda.url().split("/").pop()! };
}

test("linkul camerei, deschis fără cont, duce în cameră după intrare", async ({ browser, page }) => {
  const { gazda, cod } = await cameraNoua(browser);
  const musafir = await elevNou("musafir");
  await page.goto(`/joc/${cod}`);
  await expect(page).toHaveURL(new RegExp(`inapoi=%2Fjoc%2F${cod}`));
  await page.getByLabel("Email sau utilizator").fill(musafir);
  await page.getByLabel("Parolă", { exact: true }).fill(PAROLA);
  await page.getByRole("button", { name: "Intră în cont" }).click();
  await expect(page).toHaveURL(new RegExp(`/joc/${cod}$`));
  await expect(page.getByText(`Elev ${musafir}`).first()).toBeVisible();
  await expect(gazda.getByText(`Elev ${musafir}`).first()).toBeVisible();
  await gazda.close();
});

test("linkul camerei, cu un cont nou făcut pe loc, duce tot în cameră", async ({ browser, page }) => {
  const { gazda, cod } = await cameraNoua(browser);
  const username = unic("nou");
  const email = `${username}@example.invalid`;
  await page.goto(`/joc/${cod}`);
  await page.getByRole("tab", { name: "Cont de elev" }).click();
  await page.getByLabel("Nume și prenume").fill("Ioana Nouă");
  await page.getByLabel("Utilizator").fill(username);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Parolă (minim 8 caractere)").fill(PAROLA);
  await page.getByRole("button", { name: "Creează contul de elev" }).click();
  await page.getByLabel(/Codul de 6 cifre/).fill(await ultimulCod(email));
  await page.getByRole("button", { name: "Confirmă și intră în cont" }).click();
  await expect(page).toHaveURL(new RegExp(`/joc/${cod}$`));
  await expect(gazda.getByText("Ioana Nouă").first()).toBeVisible();
  await gazda.close();
});

test("un cod de cameră greșit spune că nu există", async ({ page }) => {
  await intra(page, await elevNou("rataci"));
  await page.goto("/joc");
  await page.getByLabel("Codul camerei").fill("zzzzzz");
  await page.getByRole("button", { name: "Intră în cameră" }).click();
  await expect(page.getByText("Camera nu există sau s-a închis.")).toBeVisible();
});
