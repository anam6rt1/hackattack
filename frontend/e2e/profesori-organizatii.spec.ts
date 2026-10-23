import { expect, test } from "@playwright/test";
import { clientApi, intraRapid, profesorNou } from "./ajutor";

test("un profesor care iese din singura organizație rămâne la Profesori", async ({ page }) => {
  const { username, nume, id } = await profesorNou("plecat", "Ana Plecată");
  const p = await clientApi(username);
  const liceu = (await (await p.get("/utilizatori/me/organizatii")).json()).elemente[0].organizatieId;
  expect((await p.delete(`/utilizatori/me/organizatii/${liceu}`)).ok()).toBeTruthy();
  await p.dispose();

  await intraRapid(page, "george");
  await page.goto("/profesori");
  await page.getByRole("searchbox", { name: "Caută un profesor" }).fill(`plecata ${username}`);
  await expect(page.getByRole("heading", { name: nume })).toBeVisible();
  // Still approved: the public profile (API) answers.
  const r = await page.request.get(`/api/profesori/${id}`);
  expect(r.ok()).toBeTruthy();
  expect((await r.json()).nume).toBe(nume);
});

test("adminul retrage aprobarea, iar organizația îl aprobă din nou", async ({ browser, page }) => {
  const { username, nume } = await profesorNou("retras", "Bogdan Retras");

  // The platform admin withdraws it, with a reason; the session is cancelled.
  await intraRapid(page, "admin");
  await page.goto("/admin/profesori");
  await page.getByRole("tab", { name: "Aprobați" }).click();
  await page.getByRole("searchbox", { name: "Caută un profesor" }).fill(username);
  const card = page.locator("article").filter({ hasText: nume });
  await card.getByRole("button", { name: "Retrage aprobarea" }).click();
  await card.getByLabel("Motivul retragerii").fill("Test de retragere.");
  await card.getByRole("button", { name: "Retrage", exact: true }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Retrage aprobarea" }).click();
  await expect(card).toBeHidden();
  await page.getByRole("tab", { name: "Respinși" }).click();
  await page.getByRole("searchbox", { name: "Caută un profesor" }).fill(username);
  await expect(page.locator("article").filter({ hasText: nume }).getByText(/Aprobare retrasă pe .*: Test de retragere\./)).toBeVisible();

  const prof = await browser.newPage();
  await intraRapid(prof, username);
  await prof.goto("/");
  await expect(prof).toHaveURL(/\/in-asteptare$/);
  await expect(prof.getByText("Test de retragere.").first()).toBeVisible();

  // Liceul de Test's admin approves them again from the panel.
  const director = await browser.newPage();
  await intraRapid(director, "director");
  await director.goto("/organizatie");
  await director.getByRole("tab", { name: "Profesori" }).click();
  await director.getByRole("searchbox", { name: "Caută un membru" }).fill(username);
  const membru = director.locator("article").filter({ hasText: nume });
  await expect(membru.getByText(/i-a retras aprobarea de profesor/)).toBeVisible();
  await membru.getByRole("button", { name: "Aprobă din nou" }).click();
  await director.getByRole("alertdialog").getByRole("button", { name: "Aprobă din nou" }).click();
  await expect(membru.getByText(/i-a retras aprobarea/)).toBeHidden();

  await prof.goto("/profesori");
  await expect(prof).toHaveURL(/\/profesori$/);
  await expect(prof.getByRole("heading", { name: "Ședințele mele" })).toBeVisible();
  await Promise.all([prof.close(), director.close()]);
});

test("panoul organizației caută și filtrează membrii", async ({ page }) => {
  const { username, nume } = await profesorNou("cautat", "Cezar Căutat");
  await intraRapid(page, "director");
  await page.goto("/organizatie");
  await page.getByRole("tab", { name: "Profesori" }).click();
  const lista = page.locator(".tv-profesori");
  await expect(lista.getByRole("heading", { name: "Irina Marin" })).toBeVisible();
  // Without diacritics, and the unique username keeps it to this test's professor.
  await page.getByRole("searchbox", { name: "Caută un membru" }).fill(`cezar cautat ${username}`);
  await expect(lista.getByRole("heading", { name: nume })).toBeVisible();
  await expect(lista.getByRole("heading", { name: "Irina Marin" })).toBeHidden();
  await page.getByRole("combobox", { name: "Stare" }).selectOption("in_asteptare");
  await expect(page.getByText("Niciun profesor nu se potrivește căutării și filtrelor.")).toBeVisible();
});
