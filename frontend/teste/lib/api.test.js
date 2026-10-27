import { describe, expect, it, vi } from "vitest";
import { api, dataOra, EroareApi, idYoutube, marime, urlApi } from "@/lib/api";
import { apiFals, raspuns } from "../ajutor";

describe("api()", () => {
  it("trimite cookie-ul și antetul X-MarkAi la fiecare cerere", async () => {
    const cereri = apiFals({ "GET /materii": { elemente: [] } });
    await api("/materii");
    expect(cereri[0]).toMatchObject({ metoda: "GET", cale: "/materii", credentials: "include" });
    expect(cereri[0].antete["X-MarkAi"]).toBe("1");
  });

  it("trimite corpul ca JSON, cu Content-Type", async () => {
    const cereri = apiFals({ "POST /autentificare": { id: 1 } });
    await api("/autentificare", { metoda: "POST", corp: { identificator: "ana", parola: "x" } });
    expect(cereri[0].corp).toEqual({ identificator: "ana", parola: "x" });
    expect(cereri[0].antete["Content-Type"]).toBe("application/json");
  });

  it("trimite un formular fără Content-Type (browserul pune granița multipart)", async () => {
    const cereri = apiFals({ "POST /parcursuri/1/fisiere": raspuns(201, { id: 3 }) });
    const f = new FormData();
    f.append("fisier", new Blob(["x"]), "a.pdf");
    await api("/parcursuri/1/fisiere", { metoda: "POST", formular: f });
    expect(cereri[0].corp).toBeInstanceOf(FormData);
    expect(cereri[0].antete["Content-Type"]).toBeUndefined();
  });

  it("întoarce corpul răspunsului", async () => {
    apiFals({ "GET /utilizatori/me": { id: 7, nume: "Ana" } });
    await expect(api("/utilizatori/me")).resolves.toEqual({ id: 7, nume: "Ana" });
  });

  it("GET-urile identice simultane fac o singură cerere, cu copii separate", async () => {
    const cereri = apiFals({ "GET /progres": { nivel: { nivel: 3 } } });
    const [a, b] = await Promise.all([api("/progres"), api("/progres")]);
    expect(cereri).toHaveLength(1);
    expect(a).toEqual(b);
    a.nivel.nivel = 99;
    expect(b.nivel.nivel).toBe(3);
    await api("/progres");
    expect(cereri).toHaveLength(2);
  });

  it("după o modificare, un GET nou nu primește răspunsul unuia pornit înainte", async () => {
    let n = 0;
    const cereri = apiFals({ "GET /utilizatori/me": () => ({ id: ++n }), "POST /autentificare": { id: 1 } });
    const vechi = api("/utilizatori/me");
    await api("/autentificare", { metoda: "POST", corp: {} });
    const nou = api("/utilizatori/me");
    expect((await vechi).id).not.toBe((await nou).id);
    expect(cereri.filter((c) => c.cale === "/utilizatori/me")).toHaveLength(2);
  });

  it("POST-urile nu sunt comasate", async () => {
    const cereri = apiFals({ "POST /teme/1/predare": raspuns(204) });
    await Promise.all([api("/teme/1/predare", { metoda: "POST" }), api("/teme/1/predare", { metoda: "POST" })]);
    expect(cereri).toHaveLength(2);
  });

  it("întoarce null la 204", async () => {
    apiFals({ "POST /autentificare/iesire": raspuns(204) });
    await expect(api("/autentificare/iesire", { metoda: "POST" })).resolves.toBeNull();
  });

  it("aruncă mesajul serverului, cu statusul și corpul", async () => {
    apiFals({ "POST /autentificare": raspuns(403, { cod: "email_neconfirmat", email: "a@b.ro", mesaj: "Contul nu este confirmat încă." }) });
    const e = await api("/autentificare", { metoda: "POST", corp: {} }).catch((x) => x);
    expect(e).toBeInstanceOf(EroareApi);
    expect(e.status).toBe(403);
    expect(e.message).toBe("Contul nu este confirmat încă.");
    expect(e.date).toMatchObject({ cod: "email_neconfirmat", email: "a@b.ro" });
  });

  it.each([
    [401, "Trebuie să intri în cont."],
    [403, "Nu ai acces aici."],
    [500, "A apărut o eroare. Încearcă din nou."],
    [404, "A apărut o eroare. Încearcă din nou."],
  ])("fără mesaj de la server, %i are un mesaj implicit", async (status, mesaj) => {
    apiFals({ "GET /x": raspuns(status, null) });
    await expect(api("/x")).rejects.toThrow(mesaj);
  });

  it("un corp care nu e JSON nu strică mesajul", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("<html>Bad gateway</html>", { status: 502 })));
    await expect(api("/x")).rejects.toMatchObject({ status: 502, message: "A apărut o eroare. Încearcă din nou." });
  });

  it("fără rețea dă statusul 0 și un mesaj clar", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("Failed to fetch"); }));
    await expect(api("/x")).rejects.toMatchObject({ status: 0, message: expect.stringContaining("Nu ne putem conecta") });
  });

  it("folosește adresa din NEXT_PUBLIC_API_URL", async () => {
    const fetch = vi.fn(async () => new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", fetch);
    await api("/judete");
    expect(fetch.mock.calls[0][0]).toBe("/api/judete");
  });
});

describe("urlApi()", () => {
  it("pune prefixul API-ului", () => expect(urlApi("/profesori/3/poza")).toBe("/api/profesori/3/poza"));
  it.each([null, undefined, ""])("fără cale (%s) e null", (c) => expect(urlApi(c)).toBeNull());
});

describe("marime()", () => {
  it.each([
    [0, "1 KB"],
    [300, "1 KB"],
    [850 * 1024, "850 KB"],
    [1024 * 1024 - 1, "1024 KB"],
    [1024 * 1024, "1,0 MB"],
    [12.4 * 1024 * 1024, "12,4 MB"],
    [20 * 1024 * 1024, "20,0 MB"],
  ])("%i octeți = %s", (o, text) => expect(marime(o)).toBe(text));
});

describe("idYoutube()", () => {
  it.each([
    ["https://www.youtube.com/watch?v=abc123", "abc123"],
    ["https://youtube.com/watch?v=xyz&t=30", "xyz"],
    ["https://youtu.be/abc123", "abc123"],
    ["https://m.youtube.com/watch?v=mob", "mob"],
    ["https://www.youtube.com/embed/emb42", "emb42"],
    ["https://www.youtube.com/shorts/scurt", "scurt"],
  ])("%s → %s", (url, id) => expect(idYoutube(url)).toBe(id));

  it.each([
    [null], [""], ["nu e link"], ["https://vimeo.com/123"], ["https://youtu.be/"], ["https://notyoutube.com.evil.ro/watch?v=x"],
  ])("%s nu e YouTube", (url) => expect(idYoutube(url)).toBeNull());
});

describe("dataOra()", () => {
  it("formatează în română", () => {
    const text = dataOra("2026-10-21T15:30:00Z");
    expect(text).toMatch(/21/);
    expect(text).toMatch(/oct/);
  });
  it("fără dată e gol", () => expect(dataOra(null)).toBe(""));
});
