import { expect, test } from "@playwright/test";
import { elevNou, intraRapid } from "./ajutor";

test("PDF-ul lecției se deschide în browser, într-un tab nou", async ({ page }) => {
  await intraRapid(page, await elevNou("pdf"));
  await page.goto(`/parcurs/${process.env.E2E_LECTIE}`);
  const link = page.getByRole("link", { name: /Luceafărul - rezumat\.pdf/ });
  await expect(link).toContainText("se deschide");
  await expect(link).toHaveAttribute("target", "_blank");
  // The same address with the student's cookie: shown inline, as a PDF, with a strict policy.
  const r = await page.request.get((await link.getAttribute("href"))!);
  expect(r.status()).toBe(200);
  expect(r.headers()["content-type"]).toContain("application/pdf");
  expect(r.headers()["content-disposition"]).toContain("inline");
  expect(r.headers()["content-security-policy"]).toContain("frame-ancestors 'none'");
});

test("fișierul unei lecții nu se deschide fără cont", async ({ page }) => {
  await page.goto("/");
  const r = await page.request.get("/api/parcursuri/" + process.env.E2E_LECTIE + "/fisiere");
  expect(r.status()).toBe(401);
});

test("filtrul de clasă arată doar lecțiile clasei alese", async ({ page }) => {
  await intraRapid(page, await elevNou("clasa"));
  await page.goto("/materiale/1");
  const clase = page.getByRole("group", { name: "Filtru după clasă" });
  await clase.getByRole("button", { name: "a IX-a" }).click();
  await expect(page.getByRole("link", { name: /Ion, clasa a IX-a \(E2E\)/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Luceafărul \(E2E\)/ })).toBeHidden();
  await clase.getByRole("button", { name: "Toate" }).click();
  await expect(page.getByRole("link", { name: /Luceafărul \(E2E\)/ })).toBeVisible();
});

test("„Aproape de mine” cere județul din Setări", async ({ page }) => {
  await intraRapid(page, await elevNou("local"));
  await page.goto("/materiale/1");
  await expect(page.getByRole("group", { name: "Filtru după loc" }).getByRole("button", { name: "Aproape de mine" })).toBeDisabled();
  await expect(page.getByRole("link", { name: "Alege-ți județul în Setări ca să vezi lecțiile din apropiere." })).toBeVisible();
});

test("lecția doar pentru organizație o văd doar membrii ei", async ({ page, browser }) => {
  await intraRapid(page, await elevNou("strain"));
  await page.goto("/materiale/1");
  await expect(page.getByRole("link", { name: /Luceafărul \(E2E\)/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Doar pentru liceu \(E2E\)/ })).toBeHidden();
  await page.goto(`/parcurs/${process.env.E2E_LECTIE_PRIVATA}`);
  await expect(page.getByText(/nu există/i).first()).toBeVisible();

  // A student of Liceul de Test (confirmed by its director) sees it.
  const membru = await elevNou("membru", { organizatieId: 1 });
  const director = await browser.newPage();
  await intraRapid(director, "director");
  await director.goto("/organizatie");
  await director.locator("article").filter({ hasText: membru }).getByRole("button", { name: "Aprobă" }).click();
  await director.close();
  const m = await browser.newPage();
  await intraRapid(m, membru);
  await m.goto("/materiale/1");
  await expect(m.getByRole("link", { name: /Doar pentru liceu \(E2E\)/ })).toBeVisible();
  await m.close();
});
