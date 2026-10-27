import { expect, test } from "@playwright/test";
import { elevNou, intraRapid } from "./ajutor";

// A whole game against a bot, on the server's real clock: 9 islands, 2 players, 6 rounds (about two minutes).
test("un joc întreg cu un robot, până la XP", async ({ page }) => {
  test.setTimeout(300_000);
  await intraRapid(page, await elevNou("jucator"));
  await page.goto("/joc");
  // Nothing is ticked at first: the host picks the subject (here the one with the test Lecție's questions).
  const creeaza = page.getByRole("button", { name: "Creează o cameră" });
  await expect(page.getByRole("checkbox", { checked: true })).toHaveCount(0);
  await expect(creeaza).toBeDisabled();
  await expect(creeaza).toHaveAccessibleDescription("Bifează cel puțin o materie ca să creezi camera.");
  await page.getByLabel("Limba și literatura română").check();
  await expect(creeaza).toBeEnabled();
  await page.getByLabel("Jucători").selectOption("2");
  await page.getByRole("button", { name: "Creează o cameră" }).click();
  await expect(page).toHaveURL(/\/joc\/camera\?cod=[A-Z0-9]{6}$/);
  await expect(page.getByText("Luceafărul (E2E)").first()).toBeVisible();
  // The easy bot is often wrong, so there are few ties to settle with an estimate (each adds a question).
  await page.getByRole("button", { name: "Ușor" }).click();
  await page.getByRole("button", { name: "Pornește bătălia" }).click();

  // The end-of-game window (the same words are also in the side panel and, on phones, in the line above the map).
  const final = page.locator(".tv-fereastra").getByText("Bătălia s-a terminat").first();
  const deadline = Date.now() + 280_000;
  // The screen changes on the server's clock, so every look and click is short and may miss (it is tried again).
  const scurt = { timeout: 1_000 };
  const incearca = (f: () => Promise<unknown>) => f().then(() => true, () => false);
  while (!(await final.isVisible())) {
    expect(Date.now(), "the game should end in time").toBeLessThan(deadline);
    const bun = page.locator(".tv-varianta:not([disabled])").filter({ hasText: "Răspunsul bun" });
    const trimite = page.getByRole("button", { name: "Trimite răspunsul" });
    const tinte = page.getByRole("button", { name: /^Atacă / });
    if (await bun.isVisible()) await incearca(() => bun.click(scurt));
    else if (await trimite.isVisible())
      await incearca(async () => { await page.getByLabel("Anul estimat").fill("1880", scurt); await trimite.click(scurt); });
    else if ((await tinte.count()) > 0 && !(await page.locator(".tv-fundal").isVisible())) await incearca(() => tinte.first().click(scurt));
    else await page.waitForTimeout(250);
  }
  // Every answer was right, so the student holds islands and earns XP (30% less with a bot).
  await expect(page.getByRole("button", { name: "Joacă din nou" })).toBeVisible();
  await expect(page.locator(".tv-fereastra").getByText(/\+\d+ XP|\d+ XP/).first()).toBeVisible();
  const xp = Number((await page.locator(".tv-fereastra").innerText()).match(/(\d+) XP/)?.[1] ?? 0);
  expect(xp).toBeGreaterThan(0);
});

test("gazda vede al doilea jucător și pe spectator", async ({ page, browser }) => {
  await intraRapid(page, await elevNou("gazda"));
  await page.goto("/joc");
  await page.getByLabel("Limba și literatura română").check();
  await page.getByLabel("Jucători").selectOption("2");
  await page.getByRole("button", { name: "Creează o cameră" }).click();
  await expect(page).toHaveURL(/\/joc\/camera\?cod=[A-Z0-9]{6}$/);
  const cod = new URL(page.url()).searchParams.get("cod")!;
  const [doi, trei] = [await browser.newPage(), await browser.newPage()];
  const [u2, u3] = [await elevNou("doi"), await elevNou("trei")];
  await intraRapid(doi, u2);
  await intraRapid(trei, u3);
  await doi.goto(`/joc/${cod}`);
  await trei.goto(`/joc/${cod}`);
  await expect(page.getByText(`Elev ${u2}`).first()).toBeVisible();
  await expect(page.getByText(`Elev ${u3}`).first()).toBeVisible();
  await expect(trei.getByText("Așteaptă ca gazda să pornească bătălia...")).toBeVisible();
  await expect(page.getByRole("button", { name: "Pornește bătălia" })).toBeEnabled();
  await doi.close();
  await trei.close();
});

test("al doilea jucător intră cu codul scris pe pagina /joc", async ({ page, browser }) => {
  await intraRapid(page, await elevNou("gazda"));
  await page.goto("/joc");
  await page.getByLabel("Limba și literatura română").check();
  await page.getByRole("button", { name: "Creează o cameră" }).click();
  await expect(page).toHaveURL(/\/joc\/camera\?cod=[A-Z0-9]{6}$/);
  const cod = new URL(page.url()).searchParams.get("cod")!;
  const al2 = await browser.newPage();
  const u2 = await elevNou("cod");
  await intraRapid(al2, u2);
  await al2.goto("/joc");
  await al2.getByLabel("Codul camerei").fill(cod.toLowerCase());
  await al2.getByRole("button", { name: "Intră în cameră" }).click();
  await expect(al2).toHaveURL(new RegExp(`/joc/camera\\?cod=${cod}$`));
  await expect(page.getByText(`Elev ${u2}`).first()).toBeVisible();
  await al2.close();
});
