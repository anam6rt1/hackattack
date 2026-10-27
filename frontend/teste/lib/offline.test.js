import "fake-indexeddb/auto";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "@/lib/api";
import { asiguraProprietar, areDateOffline, citeste, golesteTot, scrie, toate } from "@/lib/offline/depozit";
import { improspateazaCheie, decripteaza } from "@/lib/offline/cheie";
import { raspundeOffline } from "@/lib/offline/adaptor";
import { actiuniInAsteptare, actiuniRefuzate, sincronizeaza } from "@/lib/offline/sincronizare";
import { apiFals } from "../ajutor";

const b64 = (b) => btoa(String.fromCharCode(...new Uint8Array(b)));
const CHEIE = new Uint8Array(32).map((_, i) => i * 7);
const grile = [1, 2, 3, 4, 5].map((id) => ({ id, tip: "grila", text: `Întrebarea ${id}`, variante: [{ id: id * 10 + 1, text: "Bun" }, { id: id * 10 + 2, text: "Rău" }] }));
const intrebari = [...grile, { id: 6, tip: "estimare", text: "Anul?", variante: [] }];
const chei = [...grile.map((q) => ({ id: q.id, corecta: q.id * 10 + 1, numeric: null, explicatie: `De ce ${q.id}` })), { id: 6, corecta: null, numeric: 1883, explicatie: null }];

// Cheia de răspunsuri criptată ca pe server: nonce (12) + text criptat + etichetă, AES-GCM.
async function cripteaza(date, cheie = CHEIE) {
  const k = await crypto.subtle.importKey("raw", cheie, { name: "AES-GCM" }, false, ["encrypt"]);
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const ct = new Uint8Array(await crypto.subtle.encrypt({ name: "AES-GCM", iv }, k, new TextEncoder().encode(JSON.stringify(date))));
  return b64(new Uint8Array([...iv, ...ct]));
}

const lectie = { id: 5, titlu: "Romanul", materieId: 1, materie: "Română", clasa: "Bac", capitole: [{ id: 50, titlu: "Capitolul 1" }], progres: null, intrebariTest: 5 };

async function pregateste() {
  await asiguraProprietar(7);
  apiFals({ "GET /utilizatori/me/cheie-offline": { cheie: b64(CHEIE), versiune: 1 } });
  await improspateazaCheie(api);
  await scrie("pachete", { parcursId: 5, uid: 7, titlu: "Romanul", materieId: 1, materie: "Română", clasa: "Bac", lectie,
    capitole: { 50: { id: 50, titlu: "Capitolul 1", continut: "Text offline" } }, fisiere: [], intrebari, cheie: await cripteaza(chei),
    intrebariPeTest: 5, versiuneCheie: 1, descarcataLa: new Date().toISOString(), marime: 1000 });
}

beforeEach(async () => { await golesteTot(); await pregateste(); });
afterEach(() => { vi.restoreAllMocks(); Object.defineProperty(navigator, "onLine", { value: true, configurable: true }); });

describe("lecția descărcată", () => {
  it("lecția și capitolul se deschid din depozit", async () => {
    expect((await raspundeOffline("/parcursuri/5")).titlu).toBe("Romanul");
    expect((await raspundeOffline("/cursuri/50")).continut).toBe("Text offline");
    await expect(raspundeOffline("/parcursuri/9")).rejects.toThrow(/nu este descărcată/);
  });

  it("cheia decriptează răspunsurile; altă cheie cere descărcarea din nou", async () => {
    const p = await citeste("pachete", 5);
    expect((await decripteaza(p.cheie)).get(6).numeric).toBe(1883);
    await scrie("pachete", { ...p, cheie: await cripteaza(chei, new Uint8Array(32)) });
    await expect(decripteaza((await citeste("pachete", 5)).cheie)).rejects.toThrow(/descărcată din nou/);
  });

  it("testul se notează pe loc, intră în coadă și lecția apare promovată", async () => {
    const t = await raspundeOffline("/parcursuri/5/quiz", { metoda: "POST" });
    expect(t.id).toBeLessThan(0);
    expect(t.intrebari).toHaveLength(5);
    expect(JSON.stringify(t)).not.toMatch(/corecta|De ce/);
    let r;
    for (const q of t.intrebari) r = await raspundeOffline(`/parcursuri/5/quiz/${t.id}/raspuns`, { metoda: "POST", corp: { intrebareId: q.id, variantaId: q.id * 10 + 1 } });
    expect(r.final).toMatchObject({ promovat: true, corecte: 5, necesare: 4, offline: true });
    expect(r.final.corectii[0].explicatie).toMatch(/De ce/);
    const coada = await actiuniInAsteptare();
    expect(coada.map((o) => o.tip)).toEqual(["test"]);
    expect(coada[0].date.raspunsuri).toHaveLength(5);
    expect((await raspundeOffline("/parcursuri/5")).progres.promovatLa).toBeTruthy();
  });

  it("un test început offline se reia, iar un răspuns dat de două ori e refuzat", async () => {
    const t = await raspundeOffline("/parcursuri/5/quiz", { metoda: "POST" });
    const q = t.intrebari[0];
    await raspundeOffline(`/parcursuri/5/quiz/${t.id}/raspuns`, { metoda: "POST", corp: { intrebareId: q.id, variantaId: q.id * 10 + 2 } });
    const reluat = await raspundeOffline("/parcursuri/5/quiz", { metoda: "POST" });
    expect(reluat.id).toBe(t.id);
    expect(reluat.raspunse).toBe(1);
    await expect(raspundeOffline(`/parcursuri/5/quiz/${t.id}/raspuns`, { metoda: "POST", corp: { intrebareId: q.id, variantaId: q.id * 10 + 1 } })).rejects.toThrow(/deja/);
  });

  it("simularea folosește întrebările descărcate și se notează la final", async () => {
    expect(await raspundeOffline("/simulari/optiuni")).toEqual([{ materieId: 1, clasa: "Bac", materie: "Română", disponibile: 6 }]);
    const s = await raspundeOffline("/simulari", { metoda: "POST", corp: { materieId: 1, tip: "bac", clasa: "Bac", numarIntrebari: 6, durataMinute: 10 } });
    expect(s.intrebari.every((q) => q.variantaCorectaId === null)).toBe(true);
    for (const q of s.intrebari) await raspundeOffline(`/simulari/${s.id}/raspuns`, { metoda: "POST", corp: q.tip === "grila" ? { intrebareId: q.id, variantaId: q.id * 10 + 1 } : { intrebareId: q.id, valoare: 1883 } });
    const final = await raspundeOffline(`/simulari/${s.id}/finalizare`, { metoda: "POST" });
    expect(final.corecte).toBe(6);
    expect(final.notaOrientativa).toBe(10);
    expect((await actiuniInAsteptare()).map((o) => o.tip)).toEqual(["simulare"]);
  });

  it("fără internet, api() răspunde din depozit; o acțiune care cere server explică de ce nu merge", async () => {
    Object.defineProperty(navigator, "onLine", { value: false, configurable: true });
    expect((await api("/parcursuri/5")).titlu).toBe("Romanul");
    await expect(api("/rezervari", { metoda: "POST", corp: {} })).rejects.toMatchObject({ status: 0, message: expect.stringMatching(/internet/) });
  });

  it("un GET eșuat din cauza rețelei folosește ultima copie păstrată", async () => {
    apiFals({ "GET /teme": { elemente: [{ id: 1 }], total: 1 } });
    await api("/teme");
    await vi.waitFor(async () => expect(await citeste("raspunsuri", "/teme")).toBeTruthy());
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("Failed to fetch"); }));
    expect((await api("/teme")).total).toBe(1);
  });
});

describe("sincronizarea", () => {
  it("trimite coada, păstrează refuzurile și anunță XP-ul", async () => {
    const t = await raspundeOffline("/parcursuri/5/quiz", { metoda: "POST" });
    for (const q of t.intrebari) await raspundeOffline(`/parcursuri/5/quiz/${t.id}/raspuns`, { metoda: "POST", corp: { intrebareId: q.id, variantaId: q.id * 10 + 1 } });
    await raspundeOffline("/cursuri/50/acces", { metoda: "POST" });
    const [test, capitol] = await actiuniInAsteptare();
    const cereri = apiFals({ "POST /sincronizare": { rezultate: [
      { idClient: test.idClient, stare: "aplicata", rezultat: { parcursId: 5, promovat: true, xpCastigat: 40 } },
      { idClient: capitol.idClient, stare: "respinsa", mesaj: "Lecția nu mai este disponibilă." },
    ] } });
    const anunt = vi.fn(); window.addEventListener("telebac-sincronizare", (e) => anunt(e.detail), { once: true });
    const r = await sincronizeaza();
    expect(r).toMatchObject({ aplicate: 1, refuzate: 1, xp: 40 });
    expect(cereri[0].corp.operatii.map((o) => o.tip)).toEqual(["test", "capitol_deschis"]);
    expect(await actiuniInAsteptare()).toEqual([]);
    expect((await actiuniRefuzate())[0].mesaj).toMatch(/nu mai este/);
    expect(anunt).toHaveBeenCalledWith(expect.objectContaining({ xp: 40 }));
  });

  it("fără răspuns de la server, acțiunile rămân în coadă", async () => {
    await raspundeOffline("/cursuri/50/acces", { metoda: "POST" });
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("Failed to fetch"); }));
    await expect(sincronizeaza()).rejects.toBeTruthy();
    expect(await actiuniInAsteptare()).toHaveLength(1);
  });

  it("ieșirea din cont șterge tot, iar alt cont pornește de la zero", async () => {
    expect(areDateOffline()).toBe(true);
    await asiguraProprietar(8);
    expect(await toate("pachete")).toEqual([]);
    await golesteTot();
    expect(areDateOffline()).toBe(false);
  });
});
