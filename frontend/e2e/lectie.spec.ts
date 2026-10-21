import { expect, test } from "@playwright/test";
import { elevNou, intraRapid } from "./ajutor";

const lectie = () => process.env.E2E_LECTIE!;

test("elevul citește un capitol, trece testul și lecția apare terminată", async ({ page }) => {
  await intraRapid(page, await elevNou("cititor", { clasa: "XII" }));
  await page.goto("/materiale");
  await page.getByRole("link", { name: /Limba și literatura română/ }).first().click();
  await page.getByRole("link", { name: /Luceafărul \(E2E\)/ }).click();
  await expect(page).toHaveURL(new RegExp(`/parcurs/${lectie()}$`));

  // The first chapter is recommended; its notes mark it as read and the recommendation moves on.
  await page.getByRole("button", { name: /Capitolul 1: Geneza poemului \(recomandat acum\)/ }).click();
  await page.getByRole("dialog").getByRole("button", { name: /Materiale/ }).click();
  await expect(page.getByText("„Luceafărul” a apărut în 1883.")).toBeVisible();
  await page.getByRole("button", { name: "Înapoi la hartă" }).click();
  await expect(page.getByRole("button", { name: /Capitolul 2: Cătălina și Hyperion \(recomandat acum\)/ })).toBeVisible();

  await page.getByRole("link", { name: "Începe testul" }).click();
  await expect(page.getByText(/Întrebarea 1 din 6 · ai nevoie de 5 răspunsuri corecte/)).toBeVisible();
  for (let i = 1; i <= 6; i++) {
    await page.getByRole("button", { name: /Răspunsul bun/ }).click();
    // After the last answer the summary takes the question's place.
    if (i === 6) break;
    await expect(page.getByText("Corect!")).toBeVisible();
    await page.getByRole("button", { name: "Următoarea →" }).click();
  }
  await expect(page.getByText("Felicitări! 6 din 6 corecte. Ai câștigat 100 XP.")).toBeVisible();
  await expect(page.getByText("Explicația 1.")).toBeVisible();

  await page.getByRole("link", { name: "Înapoi la lecție →" }).click();
  await expect(page.getByRole("link", { name: "Recapitulează" })).toBeVisible();
  await page.goto("/");
  await expect(page.getByText("100 XP").first()).toBeVisible();
});

test("un test picat arată corecturile și cere încă o încercare", async ({ page }) => {
  await intraRapid(page, await elevNou("grabit"));
  await page.goto(`/test/${lectie()}`);
  for (let i = 1; i <= 6; i++) {
    await page.getByRole("button", { name: /Altă variantă A/ }).click();
    if (i === 6) break;
    await expect(page.getByText("Greșit. Vezi răspunsul corect la final.")).toBeVisible();
    await page.getByRole("button", { name: "Următoarea →" }).click();
  }
  await expect(page.getByText("0 din 6 corecte. Ai nevoie de 5 ca să treci lecția.")).toBeVisible();
  await expect(page.getByText("✗ Ai ales: Altă variantă A · Corect: Răspunsul bun").first()).toBeVisible();
  await page.getByRole("button", { name: "Test nou" }).click();
  await expect(page.getByText(/Întrebarea 1 din 6/)).toBeVisible();
});

test("lecția e în căutare, fără diacritice", async ({ page }) => {
  await intraRapid(page, await elevNou("caut"));
  await page.goto("/materiale");
  await page.getByPlaceholder(/ex\. Luceafărul/).fill("luceafarul e2e");
  await expect(page.getByRole("link", { name: /Luceafărul \(E2E\)/ }).first()).toBeVisible();
});
