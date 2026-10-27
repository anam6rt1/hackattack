import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import HartaInsule from "@/components/HartaInsule";
import TabNivel from "@/components/TabNivel";
import { apiFals } from "../ajutor";

const capitole = (n) => Array.from({ length: n }, (_, i) => ({ id: 100 + i, titlu: `Capitol ${i + 1}`, capitol: null }));
const lectie = (n, progres = null, titlu = "Lecție de test") => ({ id: 5, titlu, capitole: capitole(n), progres });

const butoane = () => screen.getAllByRole("button", { name: /^Capitolul \d+:/ });
const lista = () => within(screen.getByRole("heading", { name: "Capitolele lecției" }).parentElement).getAllByRole("button");

describe("harta unei lecții", () => {
  it.each([1, 2, 3])("cu %i capitole are tot atâtea insule, pe o pagină", (n) => {
    render(<HartaInsule lectie={lectie(n)} />);
    expect(butoane()).toHaveLength(n);
    expect(screen.queryByText(/Insulele următoare/)).not.toBeInTheDocument();
  });

  it.each([[4, 2], [6, 2], [7, 3], [10, 4]])("cu %i capitole sunt %i pagini, câte 3 insule", (n, pagini) => {
    render(<HartaInsule lectie={lectie(n)} />);
    expect(butoane()).toHaveLength(3);
    expect(screen.getByText(`1 / ${pagini}`)).toBeInTheDocument();
  });

  it("lista de sub hartă are toate capitolele", () => {
    render(<HartaInsule lectie={lectie(8)} />);
    expect(lista()).toHaveLength(8);
  });

  it("fără progres, primul capitol e recomandat", () => {
    render(<HartaInsule lectie={lectie(3)} />);
    expect(screen.getByRole("button", { name: "Capitolul 1: Capitol 1 (recomandat acum)" })).toBeInTheDocument();
    expect(screen.getAllByText("Recomandat acum")).toHaveLength(1);
  });

  it("e recomandat capitolul de după cel citit ultima dată", () => {
    render(<HartaInsule lectie={lectie(3, { ultimulCursId: 100 })} />);
    expect(screen.getByRole("button", { name: "Capitolul 2: Capitol 2 (recomandat acum)" })).toBeInTheDocument();
  });

  it("după ultimul capitol nu mai e nimic recomandat", () => {
    render(<HartaInsule lectie={lectie(3, { ultimulCursId: 102 })} />);
    expect(screen.queryByText("Recomandat acum")).not.toBeInTheDocument();
  });

  it("după testul trecut nu mai e nimic recomandat", () => {
    render(<HartaInsule lectie={lectie(3, { promovatLa: "2026-10-20T10:00:00Z", ultimulCursId: null })} />);
    expect(screen.queryByText("Recomandat acum")).not.toBeInTheDocument();
  });

  it("harta se deschide la pagina capitolului recomandat", () => {
    render(<HartaInsule lectie={lectie(7, { ultimulCursId: 104 })} />);
    expect(screen.getByText("2 / 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Capitolul 6: Capitol 6 (recomandat acum)" })).toBeInTheDocument();
  });

  it("paginile se răsfoiesc și butoanele se blochează la capete", async () => {
    const u = userEvent.setup();
    render(<HartaInsule lectie={lectie(5)} />);
    const anterioare = screen.getByRole("button", { name: "← Insulele anterioare" });
    const urmatoare = screen.getByRole("button", { name: "Insulele următoare →" });
    expect(anterioare).toBeDisabled();
    await u.click(urmatoare);
    expect(screen.getByText("2 / 2")).toBeInTheDocument();
    expect(butoane().map((b) => b.textContent.match(/\d+/)[0])).toEqual(["4", "5"]);
    expect(urmatoare).toBeDisabled();
  });

  it("un capitol din listă duce la pagina lui și îl deschide", async () => {
    apiFals({ "GET /cursuri/106": { id: 106, titlu: "Capitol 7", materiale: [], videoUrl: null } });
    const u = userEvent.setup();
    render(<HartaInsule lectie={lectie(7)} />);
    await u.click(lista()[6]);
    expect(screen.getByText("3 / 3")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toHaveTextContent("Capitolul 7: Capitol 7");
  });

  it("opera desenată pornește din locul ei, cu eticheta capitolului", () => {
    const l = { id: 1, titlu: "Luceafărul", capitole: [{ id: 1, titlu: "Partea I", capitol: "Romantismul" }], progres: null };
    render(<HartaInsule lectie={l} />);
    expect(screen.getByText("Capitolul 1 · Romantismul")).toBeInTheDocument();
  });

  it("citirea unui capitol mută recomandarea mai departe", async () => {
    apiFals({ "GET /cursuri/100": { id: 100, titlu: "Capitol 1", materiale: [{ titlu: "Notițe", text: "Text." }], videoUrl: null }, "POST /cursuri/100/acces": null });
    const u = userEvent.setup();
    render(<HartaInsule lectie={lectie(3)} />);
    await u.click(screen.getByRole("button", { name: /Capitolul 1:/ }));
    await u.click(await screen.findByRole("button", { name: /Materiale/ }));
    await u.click(screen.getByRole("button", { name: "Înapoi la hartă" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Capitolul 2: Capitol 2 (recomandat acum)" })).toBeInTheDocument());
  });
});

const CAP = { id: 100, titlu: "Luceafărul", curs: { id: 100, titlu: "Luceafărul" } };
const INSULA = { titlu: "Romantismul", autor: "Capitolul 1" };

function deschide(curs, { onCitit = vi.fn(), onInchide = vi.fn() } = {}) {
  const cereri = apiFals({ "GET /cursuri/100": curs, "POST /cursuri/100/acces": null });
  const u = userEvent.setup();
  render(<TabNivel insula={INSULA} nivel={{ ...CAP, titlu: "Capitolul 1" }} parcursId={5} onInchide={onInchide} onCitit={onCitit} />);
  return { u, cereri, onCitit, onInchide };
}

const acces = (cereri) => cereri.filter((c) => c.metoda === "POST" && c.cale === "/cursuri/100/acces");

describe("fereastra unui capitol", () => {
  const cuVideo = { id: 100, titlu: "Luceafărul", videoUrl: "https://www.youtube.com/watch?v=abc123", materiale: [{ titlu: "Despre poem", text: "A apărut în 1883." }] };
  const faraVideo = { ...cuVideo, videoUrl: null };

  it("are titlul capitolului și al lecției", async () => {
    deschide(faraVideo);
    expect(screen.getByRole("dialog")).toHaveTextContent("Capitolul 1: Luceafărul");
    expect(screen.getByText("Romantismul · Capitolul 1")).toBeInTheDocument();
  });

  it("fără link nu există butonul video", async () => {
    deschide(faraVideo);
    await screen.findByRole("button", { name: /Materiale/ });
    expect(screen.queryByRole("button", { name: /Videoclip/ })).not.toBeInTheDocument();
  });

  it("cu link apar ambele", async () => {
    deschide(cuVideo);
    expect(await screen.findByRole("button", { name: /Videoclip/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Materiale/ })).toBeInTheDocument();
  });

  it("fără video, citit = notițele deschise", async () => {
    const { u, cereri, onCitit } = deschide(faraVideo);
    expect(acces(cereri)).toHaveLength(0);
    await u.click(await screen.findByRole("button", { name: /Materiale/ }));
    expect(screen.getByText("A apărut în 1883.")).toBeInTheDocument();
    await waitFor(() => expect(onCitit).toHaveBeenCalledWith(100));
    expect(acces(cereri)).toHaveLength(1);
  });

  it("cu video, doar notițele nu ajung", async () => {
    const { u, cereri } = deschide(cuVideo);
    await u.click(await screen.findByRole("button", { name: /Materiale/ }));
    await new Promise((r) => setTimeout(r, 50));
    expect(acces(cereri)).toHaveLength(0);
  });

  it("cu video, citit = videoclipul și notițele", async () => {
    const { u, cereri, onCitit } = deschide(cuVideo);
    await u.click(await screen.findByRole("button", { name: /Videoclip/ }));
    await u.click(screen.getByRole("button", { name: "Mai departe: notițele →" }));
    await waitFor(() => expect(onCitit).toHaveBeenCalledWith(100));
    expect(acces(cereri)).toHaveLength(1);
  });

  it("videoclipul YouTube se deschide fără cookie-uri", async () => {
    const { u } = deschide(cuVideo);
    await u.click(await screen.findByRole("button", { name: /Videoclip/ }));
    expect(screen.getByTitle("Luceafărul")).toHaveAttribute("src", "https://www.youtube-nocookie.com/embed/abc123");
  });

  it("alt site video primește un link, nu un iframe", async () => {
    const { u } = deschide({ ...cuVideo, videoUrl: "https://vimeo.com/123" });
    await u.click(await screen.findByRole("button", { name: /Videoclip/ }));
    expect(screen.getByRole("link", { name: "Deschide videoclipul" })).toHaveAttribute("href", "https://vimeo.com/123");
    expect(document.querySelector("iframe")).toBeNull();
  });

  it("părțile văzute primesc ✓", async () => {
    const { u } = deschide(cuVideo);
    await u.click(await screen.findByRole("button", { name: /Videoclip/ }));
    await u.click(screen.getByRole("button", { name: "Înapoi" }));
    expect(screen.getByRole("button", { name: /Videoclip/ })).toHaveTextContent("✓");
    expect(screen.getByRole("button", { name: /Materiale/ })).toHaveTextContent("→");
  });

  it("capitolul fără notițe spune asta", async () => {
    const { u } = deschide({ ...faraVideo, materiale: [] });
    await u.click(await screen.findByRole("button", { name: /Materiale/ }));
    expect(screen.getByText("Capitolul nu are încă notițe.")).toBeInTheDocument();
  });

  it("Escape, × și clicul pe fundal închid", async () => {
    const { u, onInchide } = deschide(faraVideo);
    await u.keyboard("{Escape}");
    await u.click(screen.getByRole("button", { name: "Închide" }));
    await u.click(screen.getByRole("dialog"));
    expect(onInchide).toHaveBeenCalledTimes(3);
  });

  it("are linkul către testul lecției", async () => {
    deschide(faraVideo);
    expect(screen.getByRole("link", { name: "Testul lecției →" })).toHaveAttribute("href", "/test?id=5");
  });

  it("o eroare la încărcare e afișată", async () => {
    apiFals({});
    render(<TabNivel insula={INSULA} nivel={CAP} parcursId={5} onInchide={() => {}} />);
    expect(await screen.findByText("Nu există.")).toBeInTheDocument();
  });
});
