import { expect, test } from "@playwright/test";
import { elevNou, intraRapid } from "./ajutor";

test("administratorul are paginile lui", async ({ page }) => {
  await intraRapid(page, "admin");
  await page.goto("/");
  const meniu = page.getByRole("navigation", { name: "Meniul principal" });
  for (const [link, titlu] of [["Organizații", "Organizații"], ["Profesori", "Profesorii"], ["Mesaje", "Mesaje trimise"]] as const) {
    await meniu.getByRole("link", { name: link }).click();
    await expect(page.getByRole("heading", { name: titlu, level: 1 })).toBeVisible();
  }
  await meniu.getByRole("link", { name: "Conținut" }).click();
  await expect(page).toHaveURL(/\/admin\/continut/);
  await expect(page.getByText("Luceafărul (E2E)").first()).toBeVisible();
});

test("mesajele trimise de furnizorul de test arată codul", async ({ page }) => {
  const elev = await elevNou("mesaj");
  await intraRapid(page, "admin");
  await page.goto("/admin/mesaje");
  await expect(page.getByText(`${elev}@example.invalid`).first()).toBeVisible();
  await expect(page.getByText(/\b\d{6}\b/).first()).toBeVisible();
});

test("un elev nu intră în paginile de administrare", async ({ page }) => {
  await intraRapid(page, await elevNou("curios"));
  for (const cale of ["/admin/organizatii", "/admin/profesori", "/admin/mesaje", "/organizatie"]) {
    await page.goto(cale);
    await expect(page).toHaveURL(/\/$/);
  }
});
