import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { useConfirmare } from "@/components/Confirmare";

// A page that asks and shows the answer.
function Pagina({ optiuni }) {
  const [confirma, fereastra] = useConfirmare();
  const [raspuns, setRaspuns] = useState("—");
  return (
    <div>
      <button onClick={async () => setRaspuns(String(await confirma(optiuni)))}>Șterge lecția</button>
      <p data-testid="raspuns">{raspuns}</p>
      {fereastra}
    </div>
  );
}

const deschide = async (optiuni = { mesaj: "Ștergi lecția „Ion”?" }) => {
  const u = userEvent.setup();
  render(<Pagina optiuni={optiuni} />);
  await u.click(screen.getByText("Șterge lecția"));
  return u;
};

describe("useConfirmare()", () => {
  it("nu arată nimic până nu se cere confirmarea", () => {
    render(<Pagina optiuni={{ mesaj: "x" }} />);
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("arată titlul și mesajul", async () => {
    await deschide();
    const f = screen.getByRole("alertdialog");
    expect(f).toHaveAccessibleName("Ești sigur?");
    expect(f).toHaveAccessibleDescription("Ștergi lecția „Ion”?");
  });

  it("„Da” răspunde true și închide fereastra", async () => {
    const u = await deschide();
    await u.click(screen.getByRole("button", { name: "Da" }));
    expect(screen.getByTestId("raspuns")).toHaveTextContent("true");
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("„Anulează” răspunde false", async () => {
    const u = await deschide();
    await u.click(screen.getByRole("button", { name: "Anulează" }));
    expect(screen.getByTestId("raspuns")).toHaveTextContent("false");
  });

  it("Escape anulează", async () => {
    const u = await deschide();
    await u.keyboard("{Escape}");
    expect(screen.getByTestId("raspuns")).toHaveTextContent("false");
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("un clic în afara ferestrei anulează, unul în ea nu", async () => {
    const u = await deschide();
    await u.click(screen.getByRole("alertdialog"));
    expect(screen.getByRole("alertdialog")).toBeInTheDocument();
    await u.click(screen.getByRole("alertdialog").parentElement);
    expect(screen.getByTestId("raspuns")).toHaveTextContent("false");
  });

  it("textele butoanelor și titlul se pot alege", async () => {
    await deschide({ titlu: "Înlocuiești PDF-ul?", mesaj: "Lecția are deja un PDF.", da: "Înlocuiește", nu: "Păstrează-l" });
    expect(screen.getByRole("alertdialog")).toHaveAccessibleName("Înlocuiești PDF-ul?");
    expect(screen.getByRole("button", { name: "Înlocuiește" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Păstrează-l" })).toBeInTheDocument();
  });

  it("la o ștergere focusul începe pe „Anulează”, iar butonul e roșu", async () => {
    await deschide({ mesaj: "Ștergi?", da: "Șterge", pericol: true });
    expect(screen.getByRole("button", { name: "Anulează" })).toHaveFocus();
    expect(screen.getByRole("button", { name: "Șterge" })).toHaveClass("tv-rosu");
  });

  it("fără pericol, focusul e pe „Da”, deci Enter confirmă", async () => {
    const u = await deschide({ mesaj: "Continui?" });
    expect(screen.getByRole("button", { name: "Da" })).toHaveFocus();
    await u.keyboard("{Enter}");
    expect(screen.getByTestId("raspuns")).toHaveTextContent("true");
  });

  it("la pericol, Enter din greșeală anulează", async () => {
    const u = await deschide({ mesaj: "Ștergi?", da: "Șterge", pericol: true });
    await u.keyboard("{Enter}");
    expect(screen.getByTestId("raspuns")).toHaveTextContent("false");
  });

  it("se poate cere din nou după un răspuns", async () => {
    const u = await deschide();
    await u.click(screen.getByRole("button", { name: "Da" }));
    await u.click(screen.getByText("Șterge lecția"));
    expect(screen.getByRole("alertdialog")).toBeInTheDocument();
    await act(async () => { await u.click(screen.getByRole("button", { name: "Anulează" })); });
    expect(screen.getByTestId("raspuns")).toHaveTextContent("false");
  });
});
