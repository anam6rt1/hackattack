import { expect, test } from "@playwright/test";
import { elevNou, intraRapid } from "./ajutor";

// Two accounts in one browser: each keeps its own look, and logging out goes back to the default one.
test("fiecare cont își păstrează aspectul, iar ieșirea pune la loc aspectul implicit", async ({ page }) => {
  const html = page.locator("html");
  // The account button shows the first name and the level ("Elev Nivel 0 · 0 XP").
  const cont = page.getByRole("banner").getByRole("button", { name: /Nivel \d+/ });
  const [ana, dan] = [await elevNou("ana"), await elevNou("dan")];

  await intraRapid(page, ana);
  await page.goto("/");
  await cont.click();
  await page.getByRole("menu").getByRole("button", { name: "Pixel" }).click();
  await expect(html).toHaveAttribute("data-theme", "pixel");
  if (!(await page.getByRole("menu").isVisible())) await cont.click();
  await page.getByRole("button", { name: "Ieși din cont" }).click();
  await expect(html).toHaveAttribute("data-theme", "acvatic");

  // Dan never chose a style: he gets the default, not Ana's.
  await intraRapid(page, dan);
  await page.goto("/");
  await expect(cont).toBeVisible();
  await expect(html).toHaveAttribute("data-theme", "acvatic");
  await page.reload();
  await expect(html).toHaveAttribute("data-theme", "acvatic");

  // Back as Ana: her choice comes from her account.
  await cont.click();
  await page.getByRole("button", { name: "Ieși din cont" }).click();
  await expect(html).toHaveAttribute("data-theme", "acvatic");
  await intraRapid(page, ana);
  await page.goto("/");
  await expect(cont).toBeVisible();
  await expect(html).toHaveAttribute("data-theme", "pixel");
});
