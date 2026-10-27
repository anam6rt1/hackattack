import { render, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import Redirectioneaza from "@/components/Redirectioneaza";
import { rute } from "@/lib/rute";
import { adresaApi, adresaSite, alegeServer, platforma } from "@/lib/platforma";
import { router } from "../ajutor";

afterEach(() => { delete window.telebacDesktop; delete window.Capacitor; vi.unstubAllGlobals(); });

describe("adresele vechi ale site-ului", () => {
  it("duc la pagina nouă, cu filtrele și ancora păstrate", async () => {
    window.history.replaceState(null, "", "/materiale/3?clasa=XI#lectii");
    render(<Redirectioneaza catre={rute.materie("3")} />);
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/materiale/materie?id=3&clasa=XI#lectii"));
  });
  it("un parametru din adresa nouă nu este înlocuit de unul vechi", async () => {
    window.history.replaceState(null, "", "/parcurs/9?id=1");
    render(<Redirectioneaza catre={rute.parcurs("9")} />);
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/parcurs?id=9"));
  });
});

describe("platforma", () => {
  it("în browser folosește site-ul și /api", () => {
    expect(platforma()).toBe("web");
    expect(adresaSite()).toBe(window.location.origin);
  });
  it("aplicația mobilă vorbește direct cu serverul ales", () => {
    window.Capacitor = { isNativePlatform: () => true };
    localStorage.setItem("telebac-server", "https://scoala.example.ro");
    expect(platforma()).toBe("mobil");
    expect(adresaSite()).toBe("https://scoala.example.ro");
    expect(adresaApi()).toBe("https://scoala.example.ro/api");
  });
  it("aplicația desktop păstrează /api (îl trimite procesul principal)", () => {
    window.telebacDesktop = {};
    expect(platforma()).toBe("desktop");
    expect(adresaApi()).not.toContain("example");
  });
  it("refuză un server fără HTTPS sau care nu răspunde", async () => {
    window.Capacitor = { isNativePlatform: () => true };
    await expect(alegeServer("http://scoala.example.ro")).rejects.toThrow(/HTTPS/);
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 404 })));
    await expect(alegeServer("scoala.example.ro")).rejects.toThrow(/nu răspunde/);
    expect(localStorage.getItem("telebac-server")).toBeNull();
  });
  it("în aplicația desktop serverul vine din procesul principal", () => {
    window.telebacDesktop = { server: "https://desktop.example.ro" };
    expect(adresaSite()).toBe("https://desktop.example.ro");
  });
});
