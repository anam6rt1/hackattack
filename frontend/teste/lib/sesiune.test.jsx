import { act, render, renderHook, screen, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { adresaInapoi, esteProfesor, preiaInapoi, profesorAprobat, SesiuneProvider, usePagina, useSesiune } from "@/lib/sesiune";
import { apiFals, raspuns, router } from "../ajutor";

describe("adresaInapoi()", () => {
  it.each(["/joc/ABC234", "/", "/materiale/3?clasa=XI", "/parcurs/12#capitol"])("%s e o adresă din site", (a) => expect(adresaInapoi(a)).toBe(a));

  it.each([
    "//alt-site.ro/joc", "/\\alt-site.ro", "https://alt-site.ro", "javascript:alert(1)", "joc/ABC", "", null, undefined, 42, {},
  ])("%s nu e acceptată", (a) => expect(adresaInapoi(a)).toBeNull());
});

describe("preiaInapoi()", () => {
  it("ia adresa ținută în tab și o șterge", () => {
    sessionStorage.setItem("markai-inapoi", "/joc/ABC234");
    expect(preiaInapoi()).toBe("/joc/ABC234");
    expect(sessionStorage.getItem("markai-inapoi")).toBeNull();
    expect(preiaInapoi()).toBeNull();
  });

  it("fără adresă ținută, ia parametrul ?inapoi=", () => {
    window.history.replaceState(null, "", "/?inapoi=%2Fjoc%2FXYZ789");
    expect(preiaInapoi()).toBe("/joc/XYZ789");
  });

  it("adresa ținută are prioritate față de parametru", () => {
    sessionStorage.setItem("markai-inapoi", "/profesori");
    window.history.replaceState(null, "", "/?inapoi=%2Fjoc%2FXYZ789");
    expect(preiaInapoi()).toBe("/profesori");
  });

  it("refuză o adresă din alt site, și din parametru", () => {
    window.history.replaceState(null, "", "/?inapoi=https%3A%2F%2Fphishing.ro");
    expect(preiaInapoi()).toBeNull();
    sessionStorage.setItem("markai-inapoi", "//phishing.ro");
    expect(preiaInapoi()).toBeNull();
  });

  it("fără nimic e null", () => expect(preiaInapoi()).toBeNull());
});

describe("rolurile", () => {
  it.each([
    [{ rol: "profesor" }, true], [{ rol: "organizatie" }, true], [{ rol: "elev" }, false], [{ rol: "admin" }, false], [null, false], [undefined, false],
  ])("esteProfesor(%o) = %s", (u, da) => expect(esteProfesor(u)).toBe(da));

  it.each([
    [{ rol: "profesor", stareProfesor: "aprobat" }, true],
    [{ rol: "organizatie", stareProfesor: "aprobat" }, true],
    [{ rol: "profesor", stareProfesor: "in_asteptare" }, false],
    [{ rol: "profesor", stareProfesor: "respins" }, false],
    [{ rol: "elev", stareProfesor: "aprobat" }, false],
  ])("profesorAprobat(%o) = %s", (u, da) => expect(profesorAprobat(u)).toBe(da));
});

const ANA = { id: 7, nume: "Ana Pop", rol: "elev" };
const cuSesiune = ({ children }) => <SesiuneProvider>{children}</SesiuneProvider>;

describe("SesiuneProvider", () => {
  it("începe cu undefined, apoi utilizatorul din /utilizatori/me", async () => {
    apiFals({ "GET /utilizatori/me": ANA });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    expect(result.current.utilizator).toBeUndefined();
    await waitFor(() => expect(result.current.utilizator).toEqual(ANA));
  });

  it("un vizitator e null", async () => {
    apiFals({ "GET /utilizatori/me": raspuns(401) });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toBeNull());
  });

  it("fără server e tot null (nu rămâne la „se încarcă”)", async () => {
    apiFals({ "GET /utilizatori/me": raspuns(500) });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toBeNull());
  });

  it("intra() pune utilizatorul întors de server", async () => {
    const cereri = apiFals({ "GET /utilizatori/me": raspuns(401), "POST /autentificare": ANA });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toBeNull());
    await act(() => result.current.intra("ana", "parola-buna"));
    expect(result.current.utilizator).toEqual(ANA);
    expect(cereri.at(-1).corp).toEqual({ identificator: "ana", parola: "parola-buna" });
  });

  it("intra() greșit lasă vizitatorul și aruncă mesajul", async () => {
    apiFals({ "GET /utilizatori/me": raspuns(401), "POST /autentificare": raspuns(401, { mesaj: "Email/utilizator sau parolă greșită." }) });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toBeNull());
    await expect(result.current.intra("ana", "x")).rejects.toThrow("parolă greșită");
    expect(result.current.utilizator).toBeNull();
  });

  it("înregistrarea nu intră în cont", async () => {
    apiFals({ "GET /utilizatori/me": raspuns(401), "POST /autentificare/inregistrare": raspuns(202, { email: "a@b.ro", mesaj: "Ți-am trimis un cod" }) });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toBeNull());
    await expect(result.current.inregistreazaElev({ nume: "Ana" })).resolves.toMatchObject({ email: "a@b.ro" });
    expect(result.current.utilizator).toBeNull();
  });

  it("confirmaEmail() intră în cont", async () => {
    apiFals({ "GET /utilizatori/me": raspuns(401), "POST /autentificare/confirma-email": ANA });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toBeNull());
    await act(() => result.current.confirmaEmail("ana@example.ro", "123456"));
    expect(result.current.utilizator).toEqual(ANA);
  });

  it("iese() scoate utilizatorul chiar dacă serverul nu răspunde", async () => {
    apiFals({ "GET /utilizatori/me": ANA, "POST /autentificare/iesire": raspuns(500) });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator).toEqual(ANA));
    await act(() => result.current.iese());
    expect(result.current.utilizator).toBeNull();
  });

  it("reincarca() citește din nou contul", async () => {
    let rol = "profesor";
    apiFals({ "GET /utilizatori/me": () => ({ id: 3, rol, stareProfesor: "aprobat" }) });
    const { result } = renderHook(() => useSesiune(), { wrapper: cuSesiune });
    await waitFor(() => expect(result.current.utilizator?.rol).toBe("profesor"));
    rol = "organizatie";
    await act(() => result.current.reincarca());
    expect(result.current.utilizator.rol).toBe("organizatie");
  });
});

function Protejata({ roluri }) {
  const u = usePagina(...roluri);
  return <p>{u ? `Bun venit, ${u.nume}` : "nimic"}</p>;
}

const randeazaPagina = (utilizator, roluri = []) => {
  apiFals({ "GET /utilizatori/me": utilizator ?? raspuns(401) });
  return render(<SesiuneProvider><Protejata roluri={roluri} /></SesiuneProvider>);
};

describe("usePagina()", () => {
  it("vizitatorul e trimis la start, cu adresa de întoarcere", async () => {
    window.history.replaceState(null, "", "/joc/ABC234?x=1");
    randeazaPagina(null);
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/?inapoi=" + encodeURIComponent("/joc/ABC234?x=1")));
    expect(sessionStorage.getItem("markai-inapoi")).toBe("/joc/ABC234?x=1");
    expect(screen.getByText("nimic")).toBeInTheDocument();
  });

  it("profesorul neaprobat ajunge la /in-asteptare", async () => {
    randeazaPagina({ id: 2, nume: "Paul", rol: "profesor", stareProfesor: "in_asteptare" });
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/in-asteptare"));
  });

  it("alt rol e trimis acasă", async () => {
    randeazaPagina({ id: 1, nume: "Ana", rol: "elev" }, ["admin"]);
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/"));
    expect(screen.getByText("nimic")).toBeInTheDocument();
  });

  it("rolul permis vede pagina", async () => {
    randeazaPagina({ id: 1, nume: "Ana", rol: "elev" }, ["elev"]);
    expect(await screen.findByText("Bun venit, Ana")).toBeInTheDocument();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("administratorul unei organizații vede paginile de profesor", async () => {
    randeazaPagina({ id: 4, nume: "Dana", rol: "organizatie", stareProfesor: "aprobat" }, ["profesor"]);
    expect(await screen.findByText("Bun venit, Dana")).toBeInTheDocument();
  });

  it("profesorul nu vede paginile administratorului de organizație", async () => {
    randeazaPagina({ id: 5, nume: "Irina", rol: "profesor", stareProfesor: "aprobat" }, ["organizatie"]);
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/"));
  });

  it("fără roluri cerute, orice cont vede pagina", async () => {
    randeazaPagina({ id: 9, nume: "Admin", rol: "admin" });
    expect(await screen.findByText("Bun venit, Admin")).toBeInTheDocument();
  });

  it("cât timp se verifică sesiunea nu se redirecționează", () => {
    apiFals({ "GET /utilizatori/me": () => new Promise(() => {}) });
    render(<SesiuneProvider><Protejata roluri={[]} /></SesiuneProvider>);
    expect(router.replace).not.toHaveBeenCalled();
    expect(screen.getByText("nimic")).toBeInTheDocument();
  });
});
