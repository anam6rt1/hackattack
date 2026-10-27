import { expect, test } from "@playwright/test";
import { elevNou, intraRapid } from "./ajutor";

test("un vizitator scrie din subsol, iar administratorul îi vede mesajul și îl rezolvă", async ({ page, browserName }) => {
  const email = `vizitator-${browserName}-${Date.now()}@example.invalid`;
  await page.goto("/");
  await page.getByRole("contentinfo").getByRole("link", { name: /Contact/ }).click();
  await expect(page).toHaveURL(/\/contact\/?$/);
  await expect(page.getByRole("heading", { name: "Scrie-ne", level: 1 })).toBeVisible();
  await page.getByRole("radio", { name: "Școli și organizații" }).check();
  await page.getByLabel("Numele tău").fill("Ana Vizitator");
  await page.getByLabel("Emailul la care îți răspundem").fill(email);
  await page.getByLabel("Mesajul", { exact: true }).fill("Vrem să aducem școala pe TeleBac.");
  await page.getByRole("button", { name: "Trimite mesajul" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Mesajul a ajuns la administratori" })).toBeVisible();

  await intraRapid(page, "admin");
  await page.goto("/");
  await page.getByRole("navigation", { name: "Meniul principal" }).getByRole("link", { name: "Contact" }).click();
  await expect(page.getByRole("heading", { name: "Mesaje de contact", level: 1 })).toBeVisible();
  const card = page.locator("article").filter({ hasText: email });
  await expect(card.getByText("Fără cont")).toBeVisible();
  await expect(card.getByRole("link", { name: "Răspunde pe email" })).toHaveAttribute("href", new RegExp(`^mailto:${email}`));
  await card.getByRole("button", { name: "Marchează rezolvat" }).click();
  await expect(card).toHaveCount(0);
  await page.getByRole("tab", { name: "Rezolvate" }).click();
  await expect(page.locator("article").filter({ hasText: email })).toBeVisible();
});

test("un elev scrie de pe o pagină, fără să-și scrie din nou numele, iar pagina ajunge cu mesajul", async ({ page }) => {
  await intraRapid(page, await elevNou("contact"));
  await page.goto("/");
  await page.locator(".mn-cont-buton").click();
  await expect(page.getByRole("link", { name: "Contact și ajutor" })).toHaveAttribute("href", "/contact");
  await page.goto("/materiale");
  await page.getByRole("contentinfo").getByRole("link", { name: /Contact/ }).click();
  await expect(page).toHaveURL(/\/contact\/?\?de=%2Fmateriale/);
  await expect(page.getByText(/Trimiți ca/)).toBeVisible();
  await expect(page.getByText("Trimitem și pagina de pe care ai venit")).toContainText("/materiale");
  await expect(page.getByLabel("Numele tău")).toHaveCount(0);
  await page.getByRole("radio", { name: "O idee" }).check();
  await page.getByLabel("Mesajul", { exact: true }).fill("Aș vrea mai multe întrebări la Luceafărul.");
  await page.getByRole("button", { name: "Trimite mesajul" }).click();
  await expect(page.getByText("Răspunsul vine pe adresa de email a contului.")).toBeVisible();
});
