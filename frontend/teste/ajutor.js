// Helpers shared by the tests: a fake router and a fake API behind window.fetch.
import { vi } from "vitest";

export const router = { replace: vi.fn(), push: vi.fn(), back: vi.fn(), refresh: vi.fn(), prefetch: vi.fn() };

/**
 * Replaces fetch with a fake API. `rute` maps "METODA /cale" (without the /api prefix and the query string, unless the key has
 * one) to a response: a value (200 JSON), `raspuns(status, corp)`, or a function (cerere) => one of those.
 * Unknown routes answer 404. Returns the list of requests made: { metoda, cale, corp, antete }.
 */
export function apiFals(rute = {}) {
  const cereri = [];
  vi.stubGlobal("fetch", vi.fn(async (url, optiuni = {}) => {
    const u = new URL(url, "http://localhost");
    const cale = u.pathname.replace(/^\/api/, "");
    const metoda = optiuni.method ?? "GET";
    const corp = typeof optiuni.body === "string" ? JSON.parse(optiuni.body) : optiuni.body;
    const cerere = { metoda, cale, query: u.search, corp, antete: optiuni.headers ?? {}, credentials: optiuni.credentials };
    cereri.push(cerere);
    let r = rute[`${metoda} ${cale}${u.search}`] ?? rute[`${metoda} ${cale}`];
    if (typeof r === "function") r = await r(cerere);
    if (r === undefined) r = raspuns(404, { mesaj: "Nu există." });
    if (!(r instanceof Raspuns)) r = raspuns(200, r);
    return new Response(r.status === 204 ? null : JSON.stringify(r.corp), { status: r.status, headers: { "Content-Type": "application/json" } });
  }));
  return cereri;
}

class Raspuns {
  constructor(status, corp) { this.status = status; this.corp = corp; }
}
export const raspuns = (status, corp = null) => new Raspuns(status, corp);

/** A promise you resolve later, to look at the screen while a request is still pending. */
export function amanat() {
  let rezolva, respinge;
  const promisiune = new Promise((a, b) => { rezolva = a; respinge = b; });
  return { promisiune, rezolva, respinge };
}
