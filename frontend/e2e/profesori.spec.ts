import { expect, test } from "@playwright/test";
import { intraRapid } from "./ajutor";

// The preview API's sessions (database/tools/MarkAi.Database/SedinteTest.cs): irina has three and george booked some of them;
// director teaches no subject, so she has none and is not listed.
test.beforeEach(async ({ page }) => {
  await intraRapid(page, "george");
  await page.goto("/profesori");
});

test("lista are doar profesorii cu ședințe programate", async ({ page }) => {
  await expect(page.getByRole("heading", { name: "Irina Marin" })).toBeVisible();
  await expect(page.getByText("Recapitulare pentru bacalaureat")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Dana Director" })).toBeHidden();
});

test("căutarea după nume sau materie, fără diacritice", async ({ page }) => {
  const cauta = page.getByRole("searchbox", { name: "Caută un profesor" });
  await cauta.fill("marin");
  await expect(page.getByRole("heading", { name: "Irina Marin" })).toBeVisible();
  await cauta.fill("romana");
  await expect(page.getByRole("heading", { name: "Irina Marin" })).toBeVisible();
  await cauta.fill("zzz");
  await expect(page.getByText("Niciun profesor cu ședințe programate nu se potrivește căutării „zzz”.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Irina Marin" })).toBeHidden();
});

test("o ședință rezervată arată „Rezervat”, fără linkul de intrare", async ({ page }) => {
  const rezervata = page.getByRole("listitem").filter({ hasText: "Rezervat" }).first();
  await expect(rezervata).toBeVisible();
  await expect(rezervata.getByRole("button", { name: /Anulează/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Intră în ședință/ })).toHaveCount(0);
});

test("fără localitate și fără organizație, filtrele acelea sunt oprite", async ({ page }) => {
  await expect(page.getByRole("button", { name: "Aproape de mine" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Din organizația mea" })).toBeDisabled();
  await expect(page.getByRole("link", { name: /Alege-ți localitatea în Setări/ })).toHaveAttribute("href", "/setari");
});
