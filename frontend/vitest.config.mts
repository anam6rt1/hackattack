import { fileURLToPath } from "node:url";
import react from "@vitejs/plugin-react";
import { transformWithOxc } from "vite";
import { defineConfig } from "vitest/config";

// The app writes JSX in plain .js files too (Next.js allows it); Vite only expects JSX in .jsx/.tsx, so these are compiled first.
const jsxInJs = {
  name: "jsx-in-js",
  enforce: "pre" as const,
  async transform(cod: string, id: string) {
    if (!/\.js$/.test(id) || id.includes("/node_modules/")) return null;
    return transformWithOxc(cod, id, { lang: "jsx", jsx: { runtime: "automatic" } });
  },
};

// Unit and component tests (Vitest + React Testing Library) in teste/; the browser tests (Playwright) live elsewhere.
export default defineConfig({
  plugins: [jsxInJs, react()],
  resolve: { alias: { "@": fileURLToPath(new URL(".", import.meta.url)) } },
  test: {
    environment: "jsdom",
    include: ["teste/**/*.test.{js,jsx,ts,tsx}"],
    setupFiles: ["teste/setup.js"],
    env: { NEXT_PUBLIC_API_URL: "/api" },
    restoreMocks: true,
  },
});
