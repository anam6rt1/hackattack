// Common to every test: DOM matchers, a clean DOM and storage, and the Next.js pieces that need a running app.
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { router } from "./ajutor";

vi.mock("next/navigation", () => ({
  useRouter: () => router,
  usePathname: () => window.location.pathname,
  useSearchParams: () => new URLSearchParams(window.location.search),
}));

// A plain link: the tests check where it goes, not Next's prefetching.
vi.mock("next/link", async () => {
  const { createElement } = await import("react");
  return {
    default: ({ href, children, ...rest }) => {
      delete rest.prefetch;
      return createElement("a", { href, ...rest }, children);
    },
  };
});

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  localStorage.clear();
  router.replace.mockClear();
  router.push.mockClear();
  window.history.replaceState(null, "", "/");
});
