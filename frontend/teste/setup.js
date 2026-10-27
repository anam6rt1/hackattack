// Common to every test: DOM matchers, a clean DOM and storage, and the Next.js pieces that need a running app.
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { parametri, router } from "./ajutor";
import { uitaCereriInCurs } from "@/lib/api";

vi.mock("next/navigation", () => ({
  useRouter: () => router,
  usePathname: () => window.location.pathname,
  useSearchParams: () => new URLSearchParams(window.location.search),
  useParams: () => parametri,
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
  // A request still in flight from this test must not answer the next one's.
  uitaCereriInCurs();
  sessionStorage.clear();
  localStorage.clear();
  router.replace.mockClear();
  router.push.mockClear();
  for (const k of Object.keys(parametri)) delete parametri[k];
  window.history.replaceState(null, "", "/");
});
