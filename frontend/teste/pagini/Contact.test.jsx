import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import PaginaContact from "@/app/contact/page";
import ContactAdmin from "@/app/admin/contact/page";
import SubsolSite from "@/components/SubsolSite";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals, raspuns } from "../ajutor";

const elev = { id: 3, nume: "George Pavel", email: "george@example.invalid", rol: "elev", organizatii: [] };

function porneste(utilizator, rute = {}) {
  const cereri = apiFals({
    "GET /utilizatori/me": utilizator ?? raspuns(401, { mesaj: "Neautentificat" }),
    "GET /confidentialitate": { contact: null },
    "POST /contact": raspuns(204),
    ...rute,
  });
  render(<SesiuneProvider><PaginaContact /></SesiuneProvider>);
  return { u: userEvent.setup(), trimise: () => cereri.filter((c) => c.metoda === "POST") };
}

afterEach(() => window.history.replaceState(null, "", "/"));

describe("pagina de contact", () => {
  it("vizitatorul fără cont își lasă numele și emailul și primește confirmarea", async () => {
    const { u, trimise } = porneste(null);
    await u.click(await screen.findByRole("radio", { name: "Școli și organizații" }));
    await u.type(screen.getByLabelText("Numele tău"), "Ana Pop");
    await u.type(screen.getByLabelText("Emailul la care îți răspundem"), "ana@liceu.ro");
    await u.type(screen.getByLabelText("Mesajul"), "Vrem școala pe platformă.");
    expect(screen.getByText("25 / 4000 caractere")).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Trimite mesajul" }));
    expect(await screen.findByRole("status")).toHaveTextContent("pe adresa de email pe care ai scris-o");
    expect(trimise()[0].corp).toMatchObject({ subiect: "organizatii", nume: "Ana Pop", email: "ana@liceu.ro", text: "Vrem școala pe platformă.", pagina: null, site: "" });
    await u.click(screen.getByRole("button", { name: "Trimite alt mesaj" }));
    expect(screen.getByLabelText("Mesajul")).toHaveValue("");
  });

  it("cu cont nu cere numele, trimite pagina de pe care s-a venit, fără parametri", async () => {
    window.history.replaceState(null, "", "/contact?de=" + encodeURIComponent("/parcurs?id=7"));
    const { u, trimise } = porneste(elev);
    expect(await screen.findByText("George Pavel")).toBeInTheDocument();
    expect(screen.queryByLabelText("Numele tău")).not.toBeInTheDocument();
    expect(screen.getByText("/parcurs")).toBeInTheDocument();
    await u.click(screen.getByRole("radio", { name: "Ceva nu merge" }));
    await u.type(screen.getByLabelText("Mesajul"), "Nu se încarcă harta.");
    await u.click(screen.getByRole("button", { name: "Trimite mesajul" }));
    await screen.findByText(/pe adresa de email a contului/);
    expect(trimise()[0].corp).toMatchObject({ subiect: "problema", pagina: "/parcurs", text: "Nu se încarcă harta." });
  });

  it("fără subiect nu trimite nimic, iar eroarea serverului apare lângă formular", async () => {
    const { u, trimise } = porneste(elev, { "POST /contact": raspuns(409, { mesaj: "Ai trimis deja 5 mesaje astăzi." }) });
    await u.type(await screen.findByLabelText("Mesajul"), "Salut");
    await u.click(screen.getByRole("button", { name: "Trimite mesajul" }));
    expect(trimise()).toHaveLength(0);
    await u.click(screen.getByRole("radio", { name: "Altceva" }));
    await u.click(screen.getByRole("button", { name: "Trimite mesajul" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Ai trimis deja 5 mesaje astăzi.");
  });

  it("arată adresa operatorului doar când e configurată", async () => {
    porneste(null, { "GET /confidentialitate": { contact: "contact@scoala.ro" } });
    expect(await screen.findByRole("link", { name: "contact@scoala.ro" })).toHaveAttribute("href", "mailto:contact@scoala.ro");
  });
});

describe("subsolul site-ului", () => {
  it("duce la contact cu pagina curentă, fără parametrii ei", () => {
    window.history.replaceState(null, "", "/materiale?token=secret");
    render(<SubsolSite />);
    expect(screen.getByRole("link", { name: /Contact/ })).toHaveAttribute("href", "/contact?de=%2Fmateriale");
  });
});

describe("admin → mesaje de contact", () => {
  const mesaj = { id: 5, subiect: "cont", text: "Nu primesc codul.", pagina: "/", creatLa: "2026-10-26T10:00:00Z", rezolvatLa: null, cont: false, nume: "Ana Pop", email: "ana@liceu.ro", rol: null };

  it("arată mesajul, linkul de răspuns pe email și îl marchează rezolvat", async () => {
    let rezolvat = false;
    const cereri = apiFals({
      "GET /utilizatori/me": { id: 1, nume: "Admin", rol: "admin", organizatii: [] },
      "GET /admin/contact": () => (rezolvat ? { total: 0, elemente: [] } : { total: 1, elemente: [mesaj] }),
      "POST /admin/contact/5/rezolvat": () => { rezolvat = true; return raspuns(204); },
    });
    render(<SesiuneProvider><ContactAdmin /></SesiuneProvider>);
    const card = (await screen.findByRole("heading", { name: "Ana Pop" })).closest("article");
    expect(within(card).getByText(/Fără cont/)).toBeInTheDocument();
    expect(within(card).getByRole("link", { name: "Răspunde pe email" })).toHaveAttribute("href", "mailto:ana@liceu.ro?subject=Re%3A%20TeleBac%20%C2%B7%20Contul%20meu");
    await userEvent.setup().click(within(card).getByRole("button", { name: "Marchează rezolvat" }));
    expect(await screen.findByText("Niciun mesaj de rezolvat.")).toBeInTheDocument();
    expect(cereri.some((c) => c.metoda === "POST" && c.cale === "/admin/contact/5/rezolvat")).toBe(true);
  });
});
