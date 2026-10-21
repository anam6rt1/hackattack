import { expect, test } from "@playwright/test";
import { elevNou, intraRapid, telefonUnic, ultimulCod } from "./ajutor";

test("numărul de telefon se verifică cu codul din SMS", async ({ page }) => {
  await intraRapid(page, await elevNou("tel"));
  await page.goto("/linia-ai");
  const telefon = telefonUnic();
  await page.getByLabel("Numărul tău de telefon").fill(telefon);
  await page.getByRole("button", { name: "Trimite codul prin SMS" }).click();
  await page.getByLabel("Codul primit prin SMS").fill(await ultimulCod("+40" + telefon.slice(1)));
  await page.getByRole("button", { name: "Confirmă" }).click();
  await expect(page.getByText(`Tutorele te sună pe +40${telefon.slice(1)} (verificat)`)).toBeVisible();
  // "Sună-mă" stays honest: the preview has no Twilio, so the button says calls are not on yet.
  await expect(page.getByText("Apelurile nu sunt încă pornite pe server.")).toBeVisible();
});

test("un cod SMS greșit e refuzat", async ({ page }) => {
  await intraRapid(page, await elevNou("tel"));
  await page.goto("/linia-ai");
  await page.getByLabel("Numărul tău de telefon").fill(telefonUnic());
  await page.getByRole("button", { name: "Trimite codul prin SMS" }).click();
  await page.getByLabel("Codul primit prin SMS").fill("000000");
  await page.getByRole("button", { name: "Confirmă" }).click();
  await expect(page.getByText(/Codul nu este corect/)).toBeVisible();
  await expect(page.getByText("Verifică mai întâi numărul tău de telefon")).toBeVisible();
});

test("numărul altui cont e refuzat înainte de SMS", async ({ page }) => {
  await intraRapid(page, await elevNou("tel"));
  await page.goto("/linia-ai");
  // irina's number in the fixtures.
  await page.getByLabel("Numărul tău de telefon").fill("0722123456");
  await page.getByRole("button", { name: "Trimite codul prin SMS" }).click();
  await expect(page.getByText("Acest telefon este deja asociat altui cont.")).toBeVisible();
});
