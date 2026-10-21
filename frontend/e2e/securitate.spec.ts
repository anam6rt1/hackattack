import { expect, test } from "@playwright/test";
import { elevNou, intra, intraRapid } from "./ajutor";

test("cookie-ul sesiunii nu poate fi citit din pagină", async ({ page, context }) => {
  await intra(page, await elevNou("cookie"));
  const cookie = (await context.cookies()).find((c) => c.name === "markai_sesiune");
  expect(cookie?.httpOnly).toBe(true);
  expect(cookie?.sameSite).toBe("Lax");
  expect(await page.evaluate(() => document.cookie)).not.toContain("markai_sesiune");
});

test("răspunsurile API-ului prin site au antetele de securitate", async ({ page }) => {
  const r = await page.request.get("/api/judete");
  expect(r.headers()["x-content-type-options"]).toBe("nosniff");
  expect(r.headers()["x-frame-options"]).toBe("DENY");
  expect(r.headers()["content-security-policy"]).toContain("default-src 'none'");
});

test("o cerere fără antetul site-ului (alt site) e refuzată", async ({ page }) => {
  await intraRapid(page, await elevNou("csrf"));
  const r = await page.request.patch("/api/utilizatori/me", { data: { nume: "Schimbat de alt site" } });
  expect(r.status()).toBe(403);
  await page.goto("/");
  await expect(page.getByRole("button", { name: /^Elev / })).toBeVisible();
});

test("„Ieși peste tot” închide și sesiunea de pe alt dispozitiv", async ({ page, browser }) => {
  const elev = await elevNou("peste");
  await intraRapid(page, elev);
  const altul = await browser.newPage();
  await intraRapid(altul, elev);
  await page.goto("/setari");
  await page.getByRole("button", { name: "Securitatea contului" }).click();
  await page.getByRole("button", { name: "Ieși peste tot" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Ieși peste tot" }).click();
  await expect(page).toHaveURL(/\/$/);
  await altul.goto("/materiale");
  await expect(altul).toHaveURL(/inapoi=%2Fmateriale/);
  await altul.close();
});
