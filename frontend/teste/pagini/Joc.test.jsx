import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import PaginaJoc from "@/app/joc/page";
import { SesiuneProvider } from "@/lib/sesiune";
import { apiFals, router } from "../ajutor";

const MATERII = [{ id: 3, denumire: "Biologie" }, { id: 1, denumire: "Limba și literatura română" }];

function porneste() {
  const cereri = apiFals({
    "GET /utilizatori/me": { id: 1, nume: "Ana", rol: "elev", organizatii: [] },
    "GET /materii": { elemente: MATERII },
    "POST /trivia/camere": { cod: "ABC234" },
  });
  render(<SesiuneProvider><PaginaJoc /></SesiuneProvider>);
  return { u: userEvent.setup(), cereri };
}

describe("Triviador: crearea unei camere", () => {
  it("nicio materie nu e bifată din oficiu, iar butonul spune de ce e oprit", async () => {
    porneste();
    expect(await screen.findByLabelText("Biologie")).not.toBeChecked();
    expect(screen.getByLabelText("Limba și literatura română")).not.toBeChecked();
    const creeaza = screen.getByRole("button", { name: "Creează o cameră" });
    expect(creeaza).toBeDisabled();
    expect(creeaza).toHaveAccessibleDescription("Bifează cel puțin o materie ca să creezi camera.");
  });

  it("după ce gazda bifează o materie, camera se creează cu ea", async () => {
    const { u, cereri } = porneste();
    await u.click(await screen.findByLabelText("Limba și literatura română"));
    const creeaza = screen.getByRole("button", { name: "Creează o cameră" });
    expect(creeaza).toBeEnabled();
    expect(screen.queryByText(/Bifează cel puțin o materie/)).not.toBeInTheDocument();
    await u.click(creeaza);
    await waitFor(() => expect(router.push).toHaveBeenCalledWith("/joc/ABC234"));
    expect(cereri.find((c) => c.metoda === "POST").corp.materieIds).toEqual([1]);
  });

  it("debifarea ultimei materii oprește din nou butonul", async () => {
    const { u } = porneste();
    const romana = await screen.findByLabelText("Limba și literatura română");
    await u.click(romana);
    await u.click(romana);
    expect(screen.getByRole("button", { name: "Creează o cameră" })).toBeDisabled();
  });
});
