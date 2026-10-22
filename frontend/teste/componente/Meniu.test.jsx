import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import Meniu from "@/components/Meniu";
import { TemaProvider, useTema } from "@/components/Tema";
import { SesiuneProvider, useSesiune } from "@/lib/sesiune";
import { CHEIE, CHEIE_STIL } from "@/lib/tema-sezon";
import { apiFals, raspuns, router } from "../ajutor";

const PROGRES = { nivel: { nivel: 3, titlu: "Cititor", xpTotal: 340 } };

function porneste(utilizator, rute = {}, cale = "/") {
  window.history.replaceState(null, "", cale);
  const cereri = apiFals({
    "GET /utilizatori/me": utilizator ?? raspuns(401),
    "GET /progres": PROGRES,
    "GET /utilizatori/me/preferinte": { mod: "auto", tema: null, interfata: "insule", avatar: null },
    "PUT /utilizatori/me/preferinte": raspuns(200, {}),
    "POST /autentificare/iesire": raspuns(204),
    ...rute,
  });
  const u = userEvent.setup();
  render(<SesiuneProvider><TemaProvider><Meniu /></TemaProvider></SesiuneProvider>);
  return { u, cereri };
}

const linkuri = () => within(screen.getByRole("navigation", { name: "Meniul principal" })).getAllByRole("link").map((l) => l.textContent.replace("nou", "").trim());

describe("meniul", () => {
  it("vizitatorul vede doar logo-ul", async () => {
    porneste(null);
    await waitFor(() => expect(screen.getByRole("link", { name: /TeleBac/ })).toBeInTheDocument());
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Meniu/ })).not.toBeInTheDocument();
  });

  it.each([
    ["elev", {}, ["Acasă", "Antrenament", "Materiale", "Triviador", "Profesori", "Linia AI"]],
    ["profesor", { stareProfesor: "aprobat" }, ["Acasă", "Ședințele mele", "Conținut", "Elevi", "Linia AI"]],
    ["organizatie", { stareProfesor: "aprobat" }, ["Acasă", "Organizația", "Ședințele mele", "Conținut", "Linia AI"]],
    ["admin", {}, ["Acasă", "Organizații", "Profesori", "Conținut", "Mesaje"]],
  ])("rolul %s are paginile lui", async (rol, extra, pagini) => {
    porneste({ id: 1, nume: "Ana Pop", rol, ...extra });
    await screen.findByRole("navigation", { name: "Meniul principal" });
    expect(linkuri()).toEqual(pagini);
  });

  it("profesorul neaprobat nu are pagini, doar contul", async () => {
    porneste({ id: 2, nume: "Paul", rol: "profesor", stareProfesor: "in_asteptare" });
    expect(await screen.findByRole("button", { name: /Paul/ })).toBeInTheDocument();
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });

  it("pagina curentă e marcată", async () => {
    porneste({ id: 1, nume: "Ana", rol: "elev" }, {}, "/materiale/3");
    const nav = await screen.findByRole("navigation", { name: "Meniul principal" });
    expect(within(nav).getByRole("link", { name: /Materiale/ })).toHaveAttribute("aria-current", "page");
    expect(within(nav).getByRole("link", { name: /Acasă/ })).not.toHaveAttribute("aria-current");
  });

  it("Linia AI e marcată „nou” pentru elevi", async () => {
    porneste({ id: 1, nume: "Ana", rol: "elev" });
    const nav = await screen.findByRole("navigation", { name: "Meniul principal" });
    expect(within(nav).getByRole("link", { name: /Linia AI/ })).toHaveTextContent("nou");
  });

  it("elevul își vede nivelul și XP-ul", async () => {
    porneste({ id: 1, nume: "Ana Maria", rol: "elev" });
    expect(await screen.findByText("Nivel 3 · 340 XP")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Ana/ })).toHaveTextContent("Ana");
  });

  it("profesorul nu cere progresul", async () => {
    const { cereri } = porneste({ id: 2, nume: "Irina", rol: "profesor", stareProfesor: "aprobat" });
    await screen.findByRole("navigation");
    expect(cereri.some((c) => c.cale === "/progres")).toBe(false);
  });

  it("butonul „Meniu” deschide lista pe telefon", async () => {
    const { u } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    const buton = await screen.findByRole("button", { name: /Meniu/ });
    expect(buton).toHaveAttribute("aria-expanded", "false");
    await u.click(buton);
    expect(buton).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("navigation")).toHaveClass("mn-deschis");
    await u.click(within(screen.getByRole("navigation")).getByRole("link", { name: /Triviador/ }));
    expect(buton).toHaveAttribute("aria-expanded", "false");
  });

  it("contul deschide setările, profilul, temele și stilurile", async () => {
    const { u } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    await u.click(await screen.findByRole("button", { name: /Ana/ }));
    const meniu = screen.getByRole("menu");
    expect(within(meniu).getByRole("link", { name: /Setările contului/ })).toHaveAttribute("href", "/setari");
    expect(within(meniu).getByRole("link", { name: /Profil/ })).toHaveAttribute("href", "/profil");
    expect(within(meniu).getAllByRole("button", { name: /·|Automat/ })).toHaveLength(9);
    expect(within(meniu).getByRole("button", { name: "Pixel" })).toBeInTheDocument();
  });

  it("Escape și clicul în afară închid meniul contului", async () => {
    const { u } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    await u.click(await screen.findByRole("button", { name: /Ana/ }));
    await u.keyboard("{Escape}");
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: /Ana/ }));
    await u.click(document.body);
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("alegerea unei teme o aplică, o ține minte și o salvează în cont", async () => {
    const { u, cereri } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    await u.click(await screen.findByRole("button", { name: /Ana/ }));
    await u.click(screen.getByRole("button", { name: "Iarnă · noapte" }));
    expect(document.documentElement.dataset.tema).toBe("iarna-noapte");
    expect(localStorage.getItem(CHEIE)).toBe("iarna-noapte");
    await waitFor(() => expect(cereri.find((c) => c.metoda === "PUT")?.corp).toEqual({ mod: "static", tema: "iarna-noapte" }));
    expect(screen.getByRole("button", { name: "Iarnă · noapte" })).toHaveClass("mn-tema-activa");
  });

  it("„Automat” se salvează ca mod automat", async () => {
    const { u, cereri } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    await u.click(await screen.findByRole("button", { name: /Ana/ }));
    await u.click(screen.getByRole("button", { name: /Automat/ }));
    await waitFor(() => expect(cereri.find((c) => c.metoda === "PUT")?.corp).toEqual({ mod: "auto" }));
  });

  it("stilul ales se salvează ca interfață", async () => {
    const { u, cereri } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    await u.click(await screen.findByRole("button", { name: /Ana/ }));
    await u.click(screen.getByRole("button", { name: "Realist" }));
    expect(document.documentElement.dataset.theme).toBe("realist");
    expect(localStorage.getItem(CHEIE_STIL)).toBe("realist");
    await waitFor(() => expect(cereri.find((c) => c.metoda === "PUT")?.corp).toEqual({ interfata: "realist" }));
  });

  it("ieșirea din cont duce acasă", async () => {
    const { u } = porneste({ id: 1, nume: "Ana", rol: "elev" });
    await u.click(await screen.findByRole("button", { name: /Ana/ }));
    await u.click(screen.getByRole("button", { name: "Ieși din cont" }));
    await waitFor(() => expect(router.push).toHaveBeenCalledWith("/"));
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
  });

  it("fără avatar arată inițiala numelui", async () => {
    porneste({ id: 1, nume: "ștefan", rol: "elev" });
    const buton = await screen.findByRole("button", { name: /ștefan/ });
    expect(buton.querySelector(".mn-initiala")).toHaveTextContent("Ș");
  });
});

function ArataTema() {
  const { preferinta, stil, avatar } = useTema();
  const { iese } = useSesiune();
  return (
    <>
      <p>{`${preferinta} ${stil} ${avatar ?? "fără"}`}</p>
      <button onClick={() => iese()}>Ieși</button>
    </>
  );
}

describe("TemaProvider", () => {
  const randeaza = (utilizator, preferinte) => {
    const cereri = apiFals({ "GET /utilizatori/me": utilizator ?? raspuns(401), "GET /utilizatori/me/preferinte": preferinte,
      "POST /autentificare/iesire": raspuns(204) });
    render(<SesiuneProvider><TemaProvider><ArataTema /></TemaProvider></SesiuneProvider>);
    return cereri;
  };

  it("vizitatorul păstrează alegerea din browser", async () => {
    localStorage.setItem(CHEIE, "vara-zi");
    localStorage.setItem(CHEIE_STIL, "pixel");
    const cereri = randeaza(null);
    expect(screen.getByText("vara-zi pixel fără")).toBeInTheDocument();
    await waitFor(() => expect(cereri).toHaveLength(1));
  });

  it("preferințele contului au prioritate", async () => {
    localStorage.setItem(CHEIE, "vara-zi");
    randeaza({ id: 1, nume: "Ana", rol: "elev" }, { mod: "static", tema: "toamna-noapte", interfata: "realist", avatar: "vulpe" });
    expect(await screen.findByText("toamna-noapte realist vulpe")).toBeInTheDocument();
    expect(localStorage.getItem(CHEIE)).toBe("toamna-noapte");
  });

  it("modul automat al contului înlocuiește o temă fixă din browser", async () => {
    localStorage.setItem(CHEIE, "vara-zi");
    randeaza({ id: 1, nume: "Ana", rol: "elev" }, { mod: "auto", tema: "vara-zi", interfata: "insule", avatar: null });
    expect(await screen.findByText("auto acvatic fără")).toBeInTheDocument();
  });

  it("avatarul e al contului: altul fără avatar îl șterge din browser", async () => {
    localStorage.setItem("bac_avatar", "vulpe");
    randeaza({ id: 2, nume: "Irina", rol: "profesor" }, { mod: "auto", interfata: "insule", avatar: null });
    await screen.findByText("auto acvatic fără");
    expect(localStorage.getItem("bac_avatar")).toBeNull();
  });

  it("un cont care n-a ales un stil primește stilul implicit, nu pe al contului de dinainte", async () => {
    localStorage.setItem(CHEIE_STIL, "pixel");
    randeaza({ id: 3, nume: "Dan", rol: "elev" }, { mod: "auto", tema: null, interfata: null, avatar: null });
    expect(await screen.findByText("auto acvatic fără")).toBeInTheDocument();
    expect(localStorage.getItem(CHEIE_STIL)).toBe("acvatic");
    expect(document.documentElement.dataset.theme).toBe("acvatic");
  });

  it("ieșirea din cont pune la loc aspectul implicit", async () => {
    const u = userEvent.setup();
    randeaza({ id: 1, nume: "Ana", rol: "elev" }, { mod: "static", tema: "toamna-noapte", interfata: "pixel", avatar: "vulpe" });
    expect(await screen.findByText("toamna-noapte pixel vulpe")).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Ieși" }));
    expect(await screen.findByText("auto acvatic fără")).toBeInTheDocument();
    expect(localStorage.getItem(CHEIE)).toBe("auto");
    expect(localStorage.getItem(CHEIE_STIL)).toBe("acvatic");
    expect(localStorage.getItem("bac_avatar")).toBeNull();
    expect(document.documentElement.dataset.theme).toBe("acvatic");
  });
});
