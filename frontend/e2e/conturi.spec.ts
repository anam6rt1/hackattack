import { expect, test } from "@playwright/test";
import { elevNou, intra, PAROLA, PAROLA_NOUA, ultimulCod, ultimulMesaj, unic } from "./ajutor";

test("înregistrarea unui elev, cu codul de pe email, duce în panoul lui", async ({ page }) => {
  const username = unic("ana");
  const email = `${username}@example.invalid`;
  await page.goto("/");
  await page.getByRole("tab", { name: "Cont de elev" }).click();
  await page.getByLabel("Nume și prenume").fill("Ana Țăranu");
  await page.getByLabel("Utilizator").fill(username);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Parolă (minim 8 caractere)").fill(PAROLA);
  await page.getByLabel("Clasa (opțional)").selectOption("XI");
  await page.getByRole("button", { name: "Creează contul de elev" }).click();
  await expect(page.getByRole("heading", { name: "Confirmă adresa de email" })).toBeVisible();
  await page.getByLabel(`Codul de 6 cifre trimis pe ${email}`).fill(await ultimulCod(email));
  await page.getByRole("button", { name: "Confirmă și intră în cont" }).click();
  await expect(page.getByRole("heading", { name: "Salut, Ana!" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Meniul principal" }).getByRole("link", { name: "Materiale" })).toBeVisible();
});

test("un cod greșit e refuzat, iar contul neconfirmat e trimis la cod la intrare", async ({ page }) => {
  const username = unic("bogdan");
  const email = `${username}@example.invalid`;
  await page.goto("/");
  await page.getByRole("tab", { name: "Cont de elev" }).click();
  await page.getByLabel("Nume și prenume").fill("Bogdan Ștefănescu");
  await page.getByLabel("Utilizator").fill(username);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Parolă (minim 8 caractere)").fill(PAROLA);
  await page.getByRole("button", { name: "Creează contul de elev" }).click();
  await page.getByLabel(/Codul de 6 cifre/).fill("000000");
  await page.getByRole("button", { name: "Confirmă și intră în cont" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Codul nu este corect" })).toBeVisible();

  await page.reload();
  await intra(page, username).catch(() => {});
  await expect(page.getByRole("heading", { name: "Confirmă adresa de email" })).toBeVisible();
  await page.getByLabel(/Codul de 6 cifre/).fill(await ultimulCod(email));
  await page.getByRole("button", { name: "Confirmă și intră în cont" }).click();
  await expect(page.getByRole("heading", { name: "Salut, Bogdan!" })).toBeVisible();
});

test("parola uitată: cod pe email, parola nouă, intrare cu ea", async ({ page }) => {
  const username = await elevNou("carmen");
  const email = `${username}@example.invalid`;
  await page.goto("/");
  await page.getByRole("button", { name: "Am uitat parola" }).click();
  await page.getByLabel("Email sau utilizator").fill(username);
  const inainte = await ultimulMesaj();
  await page.getByRole("button", { name: "Trimite codul" }).click();
  await expect(page.getByLabel("Codul de 6 cifre")).toHaveValue("");
  await page.getByLabel("Codul de 6 cifre").fill(await ultimulCod(email, inainte));
  await page.getByLabel("Parola nouă (minim 8 caractere)").fill(PAROLA_NOUA);
  await page.getByLabel("Confirmă parola nouă").fill(PAROLA_NOUA);
  await page.getByRole("button", { name: "Schimbă parola" }).click();
  await expect(page.getByRole("status")).toContainText("Parola a fost schimbată");

  await page.getByLabel("Email sau utilizator").fill(username);
  await page.getByLabel("Parolă", { exact: true }).fill(PAROLA);
  await page.getByRole("button", { name: "Intră în cont" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "parolă greșită" })).toBeVisible();
  await page.getByLabel("Parolă", { exact: true }).fill(PAROLA_NOUA);
  await page.getByRole("button", { name: "Intră în cont" }).click();
  await expect(page.getByRole("heading", { name: /^Salut, / })).toBeVisible();
});

test("ieșirea din cont revine la prezentare", async ({ page }) => {
  await intra(page, await elevNou("dan"));
  await page.getByRole("button", { name: /^Elev / }).click();
  await page.getByRole("button", { name: "Ieși din cont" }).click();
  await expect(page.getByRole("button", { name: "Intră în cont" })).toBeVisible();
  await page.goto("/materiale");
  await expect(page).toHaveURL(/\/\?inapoi=%2Fmateriale/);
});

test("o pagină protejată, deschisă fără cont, se redeschide după intrare", async ({ page }) => {
  const username = await elevNou("emil");
  await page.goto("/antrenament");
  await expect(page).toHaveURL(/inapoi=%2Fantrenament/);
  await intra(page, username);
  await expect(page).toHaveURL(/\/antrenament$/);
});
