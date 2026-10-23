import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import Harta from "@/app/joc/Harta";
import { HARTA_DEMO, insula } from "@/app/joc/date-harta";

// A map like the server's: islands on a grid, bridges between neighbours (GeneratorHarta on the API).
function hartaDe(n) {
  const coloane = Math.ceil(Math.sqrt(n * 1.6));
  const insule = Array.from({ length: n }, (_, i) => ({
    id: `i${i}`, nume: `Lecția ${i + 1}`, autor: `${(i % 3) + 1} capitole`, x: 40 + (i % coloane) * 250 + 125, y: 70 + Math.floor(i / coloane) * 210 + 105,
    seed: i + 1, semn: ["castel", "conac", "turn", "casa", "clopot", "far", "moara"][i % 7],
  }));
  const poduri = insule.slice(1).map((ins, i) => [insule[i].id, ins.id]);
  return { latime: 80 + coloane * 250, inaltime: 110 + Math.ceil(n / coloane) * 210, insule, poduri };
}

const JUCATORI = [{ id: "7", nume: "George", culoare: "#e5483b" }, { id: "8", nume: "Maria", culoare: "#7a52d8" }];

describe("harta Triviador", () => {
  it.each(Array.from({ length: 22 }, (_, i) => [i + 9]))("cu %i insule desenează toate insulele și etichetele", (n) => {
    const { container } = render(<Harta harta={hartaDe(n)} />);
    expect(container.querySelectorAll("g.tv-insula")).toHaveLength(n);
    expect(container.querySelectorAll(".tv-eticheta")).toHaveLength(n);
    expect(screen.getByText(`Lecția ${n}`)).toBeInTheDocument();
  });

  it("până la 15 insule are autorul și cine deține insula", () => {
    const { container } = render(<Harta harta={hartaDe(15)} />);
    expect(container.querySelectorAll(".tv-chip")).toHaveLength(15);
    expect(screen.getAllByText("Liberă")).toHaveLength(15);
  });

  it("peste 15 insule păstrează doar numele, ca să nu le acopere", () => {
    const { container } = render(<Harta harta={hartaDe(16)} />);
    expect(container.querySelectorAll(".tv-chip")).toHaveLength(0);
    expect(container.querySelectorAll(".tv-eticheta small")).toHaveLength(0);
  });

  it("proporțiile urmează harta serverului", () => {
    const h = hartaDe(20);
    const { container } = render(<Harta harta={h} />);
    expect(container.querySelector(".tv-harta").style.aspectRatio).toBe(`${h.latime} / ${h.inaltime}`);
    expect(container.querySelector("svg").getAttribute("viewBox")).toBe(`0 0 ${h.latime} ${h.inaltime}`);
  });

  it("insulele ocupate și cetățile își arată stăpânul", () => {
    render(<Harta harta={hartaDe(9)} jucatori={JUCATORI} cetati={["i0", "i8"]} proprietari={{ i0: "7", i8: "8", i4: "7" }} />);
    expect(screen.getByText("Cetatea lui George")).toBeInTheDocument();
    expect(screen.getByText("Cetatea lui Maria")).toBeInTheDocument();
    expect(screen.getAllByText("George")).toHaveLength(1);
    expect(screen.getAllByText("Liberă")).toHaveLength(6);
  });

  it("doar țintele se pot ataca, cu clic sau din tastatură", async () => {
    const onAlege = vi.fn();
    const u = userEvent.setup();
    render(<Harta harta={hartaDe(9)} tinte={["i1", "i3"]} onAlege={onAlege} />);
    const tinte = screen.getAllByRole("button");
    expect(tinte.map((t) => t.getAttribute("aria-label"))).toEqual(["Atacă Lecția 2", "Atacă Lecția 4"]);
    await u.click(tinte[0]);
    tinte[1].focus();
    await u.keyboard("{Enter}");
    await u.keyboard(" ");
    expect(onAlege.mock.calls).toEqual([["i1"], ["i3"], ["i3"]]);
  });

  it("o insulă care nu e țintă nu reacționează", async () => {
    const onAlege = vi.fn();
    const u = userEvent.setup();
    const { container } = render(<Harta harta={hartaDe(9)} tinte={[]} onAlege={onAlege} />);
    await u.click(container.querySelector("g.tv-insula"));
    expect(onAlege).not.toHaveBeenCalled();
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });

  it("insula atacată clipește", () => {
    const { container } = render(<Harta harta={hartaDe(9)} atacata="i2" />);
    expect(container.querySelectorAll(".tv-clipeste")).toHaveLength(1);
  });

  it("fiecare pod e desenat", () => {
    const h = hartaDe(12);
    const { container } = render(<Harta harta={h} />);
    // Two lines per bridge (the rope and the planks).
    expect(container.querySelectorAll("line")).toHaveLength(h.poduri.length * 2);
  });

  it("harta de pe pagina /joc are cele 9 opere", () => {
    render(<Harta />);
    expect(screen.getByText("Moromeții")).toBeInTheDocument();
    expect(HARTA_DEMO.insule).toHaveLength(9);
    expect(insula("plumb", HARTA_DEMO).autor).toBe("George Bacovia");
  });

  it("podurile hărții demo leagă doar insule existente", () => {
    const ids = new Set(HARTA_DEMO.insule.map((i) => i.id));
    expect(HARTA_DEMO.poduri.every(([a, b]) => ids.has(a) && ids.has(b))).toBe(true);
  });
});
