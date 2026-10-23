import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import Paginare, { paginiVizibile } from "@/components/Paginare";

// null = „…” (pagini sărite)
describe("paginiVizibile", () => {
  it.each([
    [1, 1, [1]],
    [1, 2, [1, 2]],
    [2, 5, [1, 2, 3, 4, 5]],
    [1, 9, [1, 2, null, 8, 9]],
    [2, 9, [1, 2, 3, null, 8, 9]],
    [3, 9, [1, 2, 3, 4, null, 8, 9]],
    [5, 9, [1, 2, null, 4, 5, 6, null, 8, 9]],
    [7, 9, [1, 2, null, 6, 7, 8, 9]],
    [8, 9, [1, 2, null, 7, 8, 9]],
    [9, 9, [1, 2, null, 8, 9]],
    [4, 6, [1, 2, 3, 4, 5, 6]],
    [50, 100, [1, 2, null, 49, 50, 51, null, 99, 100]],
  ])("pagina %i din %i", (pagina, total, asteptat) => expect(paginiVizibile(pagina, total)).toEqual(asteptat));

  it.each([
    [1, 9, [1, 2, null, 9]],
    [5, 9, [1, null, 4, 5, 6, null, 9]],
    [9, 9, [1, null, 8, 9]],
    [128, 255, [1, null, 127, 128, 129, null, 255]],
  ])("compact: pagina %i din %i are doar prima și ultima la capete", (pagina, total, asteptat) =>
    expect(paginiVizibile(pagina, total, 1)).toEqual(asteptat));

  it("nicio pagină nu apare de două ori și ordinea crește", () => {
    for (let total = 1; total <= 30; total++)
      for (let p = 1; p <= total; p++) {
        const numere = paginiVizibile(p, total).filter((x) => x !== null);
        expect(new Set(numere).size).toBe(numere.length);
        expect([...numere].sort((a, b) => a - b)).toEqual(numere);
        expect(numere).toContain(p);
        expect(numere[0]).toBe(1);
        expect(numere.at(-1)).toBe(total);
      }
  });

  it("„…” apare doar unde chiar lipsesc pagini", () => {
    for (let total = 1; total <= 30; total++)
      for (let p = 1; p <= total; p++) {
        const lista = paginiVizibile(p, total);
        lista.forEach((x, i) => { if (x === null) expect(lista[i + 1] - lista[i - 1]).toBeGreaterThan(1); });
      }
  });
});

describe("bara de pagini", () => {
  const porneste = (pagina, total) => {
    const onAlege = vi.fn();
    render(<Paginare pagina={pagina} totalPagini={total} onAlege={onAlege} />);
    return { u: userEvent.setup(), onAlege };
  };

  it.each([[1], [0], [NaN], [undefined]])("nu apare cu %s pagini", (total) => {
    render(<Paginare pagina={1} totalPagini={total} onAlege={() => {}} />);
    expect(screen.queryByRole("navigation", { name: "Pagini" })).not.toBeInTheDocument();
  });

  it("pagina curentă e marcată, iar săgețile merg înapoi și înainte", async () => {
    const { u, onAlege } = porneste(5, 9);
    expect(screen.getByRole("button", { name: "Pagina 5" })).toHaveAttribute("aria-current", "page");
    await u.click(screen.getByRole("button", { name: "Pagina anterioară" }));
    await u.click(screen.getByRole("button", { name: "Pagina următoare" }));
    await u.click(screen.getByRole("button", { name: "Pagina 9" }));
    expect(onAlege.mock.calls).toEqual([[4], [6], [9]]);
  });

  it("pe prima pagină „‹” e oprit, pe ultima „›”", () => {
    porneste(1, 9);
    expect(screen.getByRole("button", { name: "Pagina anterioară" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Pagina următoare" })).toBeEnabled();
  });

  it("pagina curentă apăsată nu cere nimic", async () => {
    const { u, onAlege } = porneste(5, 9);
    await u.click(screen.getByRole("button", { name: "Pagina 5" }));
    expect(onAlege).not.toHaveBeenCalled();
  });

  it("„…” devine un câmp: Enter duce la pagina scrisă", async () => {
    const { u, onAlege } = porneste(1, 20);
    await u.click(screen.getByRole("button", { name: "Alege pagina" }));
    const camp = screen.getByRole("spinbutton", { name: "Pagina (1–20)" });
    expect(camp).toHaveFocus();
    await u.type(camp, "13{Enter}");
    expect(onAlege).toHaveBeenCalledWith(13);
    expect(screen.queryByRole("spinbutton")).not.toBeInTheDocument();
  });

  it("„Du-mă” merge și el; o pagină în afara listei e adusă la cea mai apropiată", async () => {
    const { u, onAlege } = porneste(1, 20);
    await u.click(screen.getByRole("button", { name: "Alege pagina" }));
    await u.type(screen.getByRole("spinbutton"), "99");
    await u.click(screen.getByRole("button", { name: "Du-mă" }));
    await u.click(screen.getByRole("button", { name: "Alege pagina" }));
    await u.type(screen.getByRole("spinbutton"), "0{Enter}");
    expect(onAlege.mock.calls).toEqual([[20], [1]]);
  });

  it("Escape închide câmpul fără să ceară o pagină; gol nu cere nimic", async () => {
    const { u, onAlege } = porneste(1, 20);
    await u.click(screen.getByRole("button", { name: "Alege pagina" }));
    await u.type(screen.getByRole("spinbutton"), "7{Escape}");
    expect(screen.queryByRole("spinbutton")).not.toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Alege pagina" }));
    await u.click(screen.getByRole("button", { name: "Du-mă" }));
    expect(onAlege).not.toHaveBeenCalled();
  });

  it("cu două „…”, fiecare are câmpul lui", async () => {
    const { u } = porneste(10, 20);
    expect(screen.getAllByRole("button", { name: "Alege pagina" })).toHaveLength(2);
    await u.click(screen.getAllByRole("button", { name: "Alege pagina" })[1]);
    expect(screen.getAllByRole("spinbutton")).toHaveLength(1);
    expect(screen.getAllByRole("button", { name: "Alege pagina" })).toHaveLength(1);
  });

  it("varianta compactă are clasa ei și mai puține pagini", () => {
    render(<Paginare compact pagina={5} totalPagini={9} onAlege={() => {}} />);
    expect(screen.getByRole("navigation", { name: "Pagini" })).toHaveClass("tv-paginare-compacta");
    expect(screen.queryByRole("button", { name: "Pagina 2" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pagina 9" })).toBeInTheDocument();
  });
});
