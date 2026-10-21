import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { apiFals, raspuns, router } from "../ajutor";

// The dashboards fetch a lot; the start page only has to pick the right one.
vi.mock("@/components/acasa/AcasaElev", () => ({ default: ({ utilizator }) => <p>Panou elev {utilizator.nume}</p> }));
vi.mock("@/components/acasa/AcasaProfesor", () => ({ default: ({ utilizator }) => <p>Panou profesor {utilizator.nume}</p> }));
vi.mock("@/components/acasa/AcasaAdmin", () => ({ default: () => <p>Panou admin</p> }));
vi.mock("@/components/Tema", () => ({ TemaProvider: ({ children }) => children }));

import Acasa from "@/app/page";
import Furnizori from "@/components/Furnizori";
import { useSesiune } from "@/lib/sesiune";

const randeaza = (utilizator) => {
  apiFals({ "GET /utilizatori/me": utilizator ?? raspuns(401), "GET /judete": { elemente: [] }, "GET /organizatii": { elemente: [] } });
  return render(<Furnizori><Acasa /></Furnizori>);
};

describe("pagina de start", () => {
  it("cât timp se verifică sesiunea arată „Se încarcă”", () => {
    apiFals({ "GET /utilizatori/me": () => new Promise(() => {}) });
    render(<Furnizori><Acasa /></Furnizori>);
    expect(screen.getByText("Se încarcă...")).toBeInTheDocument();
  });

  it("vizitatorul vede prezentarea și intrarea", async () => {
    randeaza(null);
    expect(await screen.findByRole("button", { name: "Intră în cont" })).toBeInTheDocument();
  });

  it.each([
    [{ id: 1, nume: "Ana", rol: "elev" }, "Panou elev Ana"],
    [{ id: 2, nume: "Irina", rol: "profesor", stareProfesor: "aprobat" }, "Panou profesor Irina"],
    [{ id: 3, nume: "Dana", rol: "organizatie", stareProfesor: "aprobat" }, "Panou profesor Dana"],
    [{ id: 4, nume: "Admin", rol: "admin" }, "Panou admin"],
  ])("contul %o vede %s", async (u, text) => {
    randeaza(u);
    expect(await screen.findByText(text)).toBeInTheDocument();
  });

  it("profesorul neaprobat e trimis la /in-asteptare", async () => {
    randeaza({ id: 2, nume: "Paul", rol: "profesor", stareProfesor: "in_asteptare" });
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/in-asteptare"));
    expect(screen.getByText("Se încarcă...")).toBeInTheDocument();
  });

  it("după intrare, linkul de cameră din codul QR e deschis", async () => {
    sessionStorage.setItem("markai-inapoi", "/joc/ABC234");
    randeaza({ id: 1, nume: "Ana", rol: "elev" });
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/joc/ABC234"));
  });

  it("o adresă din alt site nu e urmată", async () => {
    window.history.replaceState(null, "", "/?inapoi=https%3A%2F%2Frau.ro");
    randeaza({ id: 1, nume: "Ana", rol: "elev" });
    await screen.findByText("Panou elev Ana");
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("vizitatorului nu i se consumă adresa de întoarcere înainte de intrare", async () => {
    sessionStorage.setItem("markai-inapoi", "/joc/ABC234");
    randeaza(null);
    await screen.findByRole("button", { name: "Intră în cont" });
    expect(sessionStorage.getItem("markai-inapoi")).toBe("/joc/ABC234");
  });
});

// A page with its own state (like a filter); after an account change it must start over.
function PaginaCuStare() {
  const { utilizator, iese, intra } = useSesiune();
  const [filtru, setFiltru] = useState("toate");
  return (
    <div>
      <p>cont: {utilizator === undefined ? "?" : utilizator?.nume ?? "vizitator"}</p>
      <p>filtru: {filtru}</p>
      <button onClick={() => setFiltru("XI")}>Filtrează</button>
      <button onClick={() => iese()}>Ieși</button>
      <button onClick={() => intra("irina", "x")}>Intră Irina</button>
    </div>
  );
}

describe("Furnizori: paginile se reconstruiesc la schimbarea contului", () => {
  it("prima încărcare a sesiunii nu șterge starea", async () => {
    apiFals({ "GET /utilizatori/me": async () => { await new Promise((r) => setTimeout(r, 30)); return { id: 1, nume: "Ana" }; } });
    const u = userEvent.setup();
    render(<Furnizori><PaginaCuStare /></Furnizori>);
    await u.click(screen.getByText("Filtrează"));
    expect(await screen.findByText("cont: Ana")).toBeInTheDocument();
    expect(screen.getByText("filtru: XI")).toBeInTheDocument();
  });

  it("ieșirea șterge starea paginii", async () => {
    apiFals({ "GET /utilizatori/me": { id: 1, nume: "Ana" }, "POST /autentificare/iesire": raspuns(204) });
    const u = userEvent.setup();
    render(<Furnizori><PaginaCuStare /></Furnizori>);
    await screen.findByText("cont: Ana");
    await u.click(screen.getByText("Filtrează"));
    await u.click(screen.getByText("Ieși"));
    expect(await screen.findByText("cont: vizitator")).toBeInTheDocument();
    expect(screen.getByText("filtru: toate")).toBeInTheDocument();
  });

  it("intrarea cu alt cont pornește pagina curată", async () => {
    apiFals({ "GET /utilizatori/me": raspuns(401), "POST /autentificare": { id: 2, nume: "Irina" } });
    const u = userEvent.setup();
    render(<Furnizori><PaginaCuStare /></Furnizori>);
    await screen.findByText("cont: vizitator");
    await u.click(screen.getByText("Filtrează"));
    await act(async () => { await u.click(screen.getByText("Intră Irina")); });
    expect(await screen.findByText("cont: Irina")).toBeInTheDocument();
    expect(screen.getByText("filtru: toate")).toBeInTheDocument();
  });

  it("reîncărcarea aceluiași cont păstrează starea", async () => {
    let n = 0;
    apiFals({ "GET /utilizatori/me": () => ({ id: 1, nume: n++ ? "Ana Pop" : "Ana" }) });
    const u = userEvent.setup();
    function Reincarca() {
      const { reincarca } = useSesiune();
      return <button onClick={() => reincarca()}>Reîncarcă</button>;
    }
    render(<Furnizori><PaginaCuStare /><Reincarca /></Furnizori>);
    await screen.findByText("cont: Ana");
    await u.click(screen.getByText("Filtrează"));
    await u.click(screen.getByText("Reîncarcă"));
    expect(await screen.findByText("cont: Ana Pop")).toBeInTheDocument();
    expect(screen.getByText("filtru: XI")).toBeInTheDocument();
  });
});
