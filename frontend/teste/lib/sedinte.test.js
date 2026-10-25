import { describe, expect, it } from "vitest";
import { textAlteSesiuni } from "@/lib/sedinte";

describe("textAlteSesiuni", () => {
  it.each([
    [1, "și încă o sesiune"],
    [2, "și alte 2 sesiuni"],
    [19, "și alte 19 sesiuni"],
    [20, "și alte 20 de sesiuni"],
    [21, "și alte 21 de sesiuni"],
    [101, "și alte 101 sesiuni"],
    [100, "și alte 100 de sesiuni"],
  ])("%i → %s", (n, text) => expect(textAlteSesiuni(n)).toBe(text));
});
