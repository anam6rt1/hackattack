import { afterEach, describe, expect, it, vi } from "vitest";
import {
  aplicaStil, aplicaTema, CHEIE, CHEIE_STIL, citestePreferinta, citesteStil, interfataDinStil, SCRIPT_TEMA, stilDinInterfata, temaAutomata, TEME,
} from "@/lib/tema-sezon";
import { CLASE, numeClasa } from "@/lib/clase";
import { PARTICIPARE, textLoc, TIPURI_SEDINTA } from "@/lib/sedinte";

const la = (luna, ora) => new Date(2026, luna, 15, ora, 30);

describe("temaAutomata()", () => {
  it.each([
    [0, "iarna"], [1, "iarna"], [2, "primavara"], [3, "primavara"], [4, "primavara"], [5, "vara"],
    [6, "vara"], [7, "vara"], [8, "toamna"], [9, "toamna"], [10, "toamna"], [11, "iarna"],
  ])("luna %i e %s", (luna, anotimp) => expect(temaAutomata(la(luna, 12))).toBe(`${anotimp}-zi`));

  it.each([[6, "noapte"], [7, "zi"], [19, "zi"], [20, "noapte"], [0, "noapte"], [23, "noapte"]])("ora %i e %s", (ora, moment) =>
    expect(temaAutomata(la(9, ora))).toBe(`toamna-${moment}`));
});

describe("SCRIPT_TEMA din <head>", () => {
  afterEach(() => vi.useRealTimers());

  // Scriptul rulează înainte de React; trebuie să aleagă aceeași temă ca temaAutomata() în orice lună și oră.
  it.each(Array.from({ length: 12 }, (_, luna) => [luna]))("luna %i, la 3 ore diferite, e ca temaAutomata()", (luna) => {
    for (const ora of [3, 12, 21]) {
      vi.useFakeTimers({ now: la(luna, ora) });
      new Function(SCRIPT_TEMA)();
      expect(document.documentElement.getAttribute("data-tema")).toBe(temaAutomata(la(luna, ora)));
    }
  });

  it("respectă tema aleasă și stilul salvat", () => {
    localStorage.setItem(CHEIE, "iarna-noapte");
    localStorage.setItem(CHEIE_STIL, "pixel");
    new Function(SCRIPT_TEMA)();
    expect(document.documentElement.getAttribute("data-tema")).toBe("iarna-noapte");
    expect(document.documentElement.getAttribute("data-theme")).toBe("pixel");
  });

  it("ignoră valori necunoscute", () => {
    localStorage.setItem(CHEIE, "vara-amurg");
    localStorage.setItem(CHEIE_STIL, "<script>");
    new Function(SCRIPT_TEMA)();
    expect(document.documentElement.getAttribute("data-tema")).toMatch(/^(primavara|vara|toamna|iarna)-(zi|noapte)$/);
    expect(document.documentElement.getAttribute("data-theme")).toBe("acvatic");
  });
});

describe("tema și stilul", () => {
  it("sunt 8 teme, câte două pe anotimp", () => {
    expect(TEME).toHaveLength(8);
    expect(TEME.map((t) => t.cod)).toContain("primavara-noapte");
    expect(TEME.find((t) => t.cod === "vara-zi").nume).toBe("Vară · zi");
  });

  it("aplicaTema() pune tema aleasă", () => {
    expect(aplicaTema("toamna-noapte")).toBe("toamna-noapte");
    expect(document.documentElement.dataset.tema).toBe("toamna-noapte");
  });

  it.each(["auto", null, "necunoscuta"])("aplicaTema(%s) alege după ceas", (p) => expect(aplicaTema(p)).toBe(temaAutomata()));

  it("preferința implicită e automată", () => expect(citestePreferinta()).toBe("auto"));

  it("preferința salvată e citită", () => {
    localStorage.setItem(CHEIE, "iarna-zi");
    expect(citestePreferinta()).toBe("iarna-zi");
  });

  it.each([["realist", "realist"], ["pixel", "pixel"], ["acvatic", "acvatic"], ["neon", "acvatic"], [null, "acvatic"]])(
    "stilul salvat %s se citește ca %s",
    (salvat, citit) => {
      if (salvat) localStorage.setItem(CHEIE_STIL, salvat);
      expect(citesteStil()).toBe(citit);
    },
  );

  it("fără acces la localStorage nu cade", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("blocat"); });
    expect(citestePreferinta()).toBe("auto");
    expect(citesteStil()).toBe("acvatic");
  });

  it("aplicaStil() refuză un stil necunoscut", () => {
    aplicaStil("realist");
    expect(document.documentElement.dataset.theme).toBe("realist");
    aplicaStil("altul");
    expect(document.documentElement.dataset.theme).toBe("acvatic");
  });

  it.each([["insule", "acvatic"], ["realist", "realist"], ["pixel", "pixel"], ["vechi", null], [null, null]])(
    "interfața %s e stilul %s",
    (cod, stil) => expect(stilDinInterfata(cod)).toBe(stil),
  );

  it("stilul se salvează ca interfață", () => {
    expect(interfataDinStil("acvatic")).toBe("insule");
    expect(interfataDinStil("pixel")).toBe("pixel");
  });
});

describe("clasele și ședințele", () => {
  it("clasele sunt de la IX la Bac", () => expect(CLASE).toEqual(["IX", "X", "XI", "XII", "Bac"]));
  it.each([["IX", "clasa a IX-a"], ["XII", "clasa a XII-a"], ["Bac", "Bac"]])("%s se afișează „%s”", (c, t) => expect(numeClasa(c)).toBe(t));

  it.each([
    [{ organizatie: "Liceul de Test", localitate: "Sectorul 1", judet: "București" }, "Liceul de Test · Sectorul 1, București"],
    [{ localitate: "Alba Iulia", judet: "Alba" }, "Alba Iulia, Alba"],
    [{ judet: "Cluj" }, "Cluj"],
    [{ organizatie: "Școala Bărăbanț", judet: "Alba" }, "Școala Bărăbanț · Alba"],
    [null, ""],
  ])("locul %o se scrie „%s”", (loc, text) => expect(textLoc(loc)).toBe(text));

  it("tipurile și participarea au nume românești", () => {
    expect(TIPURI_SEDINTA.fata_in_fata).toBe("Față în față");
    expect(PARTICIPARE.la_sala).toBe("la sală");
  });
});
