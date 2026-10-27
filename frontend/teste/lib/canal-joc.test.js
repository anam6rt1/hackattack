import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// A fake SignalR connection: the tests drive its state and events.
const conexiune = vi.hoisted(() => ({}));
vi.mock("@microsoft/signalr", () => {
  class Builder {
    withUrl(url, optiuni) { conexiune.url = url; conexiune.optiuni = optiuni; return this; }
    withAutomaticReconnect() { return this; }
    withKeepAliveInterval(ms) { conexiune.keepAlive = ms; return this; }
    withServerTimeout(ms) { conexiune.timeout = ms; return this; }
    configureLogging() { return this; }
    build() { return conexiune.obiect; }
  }
  return { HubConnectionBuilder: Builder, HubConnectionState: { Connected: "Connected" }, LogLevel: { Warning: 3 } };
});

import { deschideCanal } from "@/lib/canal-joc";

let apeluri;
beforeEach(() => {
  vi.useFakeTimers();
  const handlers = {};
  apeluri = { start: 0, stop: 0 };
  conexiune.obiect = {
    state: "Connected",
    handlers,
    on: (nume, f) => { handlers[nume] = f; },
    onreconnecting: (f) => { handlers.reconnecting = f; },
    onreconnected: (f) => { handlers.reconnected = f; },
    start: vi.fn(async () => { apeluri.start++; }),
    stop: vi.fn(async () => { apeluri.stop++; }),
    invoke: vi.fn(async (metoda) => (metoda === "Intra" ? { cod: "ABC234", faza: "lobby" } : undefined)),
  };
});
afterEach(() => vi.useRealTimers());

const evenimente = () => ({ onStare: vi.fn(), onConectat: vi.fn(), onDeconectat: vi.fn(), onEroare: vi.fn() });

describe("deschideCanal()", () => {
  it("închide clar partida după repornirea serverului", async () => {
    let instanta = 'prima'
    conexiune.obiect.invoke = vi.fn(async metoda => metoda === 'Instanta' ? instanta : { cod: 'ABC234' })
    const e = evenimente()
    deschideCanal('ABC234', e)
    await vi.runAllTimersAsync()
    instanta = 'noua'
    await conexiune.obiect.handlers.reconnected()
    expect(e.onEroare).toHaveBeenCalledWith(expect.objectContaining({ cod: 'SERVER_RESTART' }))
    expect(apeluri.stop).toBe(1)
    expect(conexiune.obiect.invoke.mock.calls.filter(x => x[0] === 'Intra')).toHaveLength(1)
  })
  it("se conectează la /hub/joc cu cookie-ul, cu timpii serverului", () => {
    deschideCanal("ABC234", evenimente());
    expect(conexiune.url).toBe("/api/hub/joc");
    expect(conexiune.optiuni).toEqual({ withCredentials: true });
    expect([conexiune.keepAlive, conexiune.timeout]).toEqual([5000, 15000]);
  });

  it("în aplicații se conectează direct la server, cu token de joc", async () => {
    window.Capacitor = { isNativePlatform: () => true };
    localStorage.setItem("telebac-server", "https://scoala.example.ro");
    try {
      deschideCanal("ABC234", evenimente());
      expect(conexiune.url).toBe("https://scoala.example.ro/api/hub/joc");
      expect(conexiune.optiuni.withCredentials).toBeUndefined();
      expect(typeof conexiune.optiuni.accessTokenFactory).toBe("function");
    } finally { delete window.Capacitor; }
  });

  it("pornește abia la următorul tick, apoi intră în cameră", async () => {
    const e = evenimente();
    deschideCanal("ABC234", e);
    expect(apeluri.start).toBe(0);
    await vi.runAllTimersAsync();
    expect(conexiune.obiect.invoke).toHaveBeenCalledWith("Intra", "ABC234");
    expect(e.onStare).toHaveBeenCalledWith({ cod: "ABC234", faza: "lobby" });
    expect(e.onConectat).toHaveBeenCalled();
  });

  it("închis înainte de pornire nu se conectează deloc", async () => {
    const canal = deschideCanal("ABC234", evenimente());
    canal.inchide();
    await vi.runAllTimersAsync();
    expect(apeluri.start).toBe(0);
  });

  it("o eroare la pornire ajunge la onEroare", async () => {
    conexiune.obiect.start = vi.fn(async () => { throw new Error("negociere eșuată"); });
    const e = evenimente();
    deschideCanal("ABC234", e);
    await vi.runAllTimersAsync();
    expect(e.onEroare).toHaveBeenCalled();
  });

  it("starea primită de la server ajunge la onStare", async () => {
    const e = evenimente();
    deschideCanal("ABC234", e);
    conexiune.obiect.handlers.stare({ faza: "alegere" });
    expect(e.onStare).toHaveBeenCalledWith({ faza: "alegere" });
  });

  it("la reconectare intră din nou în cameră", async () => {
    const e = evenimente();
    deschideCanal("ABC234", e);
    conexiune.obiect.handlers.reconnecting();
    expect(e.onDeconectat).toHaveBeenCalled();
    conexiune.obiect.handlers.reconnected();
    await vi.runAllTimersAsync();
    expect(conexiune.obiect.invoke).toHaveBeenCalledWith("Intra", "ABC234");
  });

  it("trimite() cheamă metoda serverului cu argumentele", async () => {
    const canal = deschideCanal("ABC234", evenimente());
    await canal.trimite("Ataca", "i3");
    expect(conexiune.obiect.invoke).toHaveBeenCalledWith("Ataca", "i3");
  });

  it("trimite() fără conexiune spune clar de ce", async () => {
    conexiune.obiect.state = "Disconnected";
    const canal = deschideCanal("ABC234", evenimente());
    await expect(canal.trimite("Porneste")).rejects.toThrow("Nu ești conectat la cameră.");
  });

  it("trimite() păstrează doar mesajul serverului", async () => {
    conexiune.obiect.invoke = vi.fn(async () => {
      throw new Error("An unexpected error occurred invoking 'Porneste' on the server. HubException: Doar gazda poate face asta.");
    });
    const canal = deschideCanal("ABC234", evenimente());
    await expect(canal.trimite("Porneste")).rejects.toThrow(/^Doar gazda poate face asta\.$/);
  });

  it("trimite() fără mesaj are un mesaj implicit", async () => {
    conexiune.obiect.invoke = vi.fn(async () => { throw new Error(""); });
    const canal = deschideCanal("ABC234", evenimente());
    await expect(canal.trimite("Ataca", "i1")).rejects.toThrow("Acțiunea nu a reușit.");
  });

  it("inchide() oprește conexiunea", () => {
    deschideCanal("ABC234", evenimente()).inchide();
    expect(apeluri.stop).toBe(1);
  });
});
