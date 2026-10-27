import type { NextConfig } from "next";

// The browser talks only to this site; /api/... is forwarded to the .NET API (database/src/MarkAi.Api).
// Same origin means the login cookie is first-party on every device, including behind a public tunnel.
const API_INTERN = process.env.API_INTERN_URL || "http://localhost:5080";
// The API's upload limit per Lecție file (Fisiere__MaxMb in markai.env, as for the API; 20 by default).
const MAX_FISIER_MB = Math.max(1, Number(process.env.Fisiere__MaxMb) || 20);
// MARKAI_EXPORT=1: the static copy packed into the desktop (Electron) and mobile (Capacitor) apps, written to out/ (in
// export mode distDir is the output folder). It has no
// /api proxy (the apps reach the server themselves, see lib/platforma.js) and no page.web.js files: those keep the site's old
// addresses (/parcurs/7, /joc/ABC234) working, which a static export cannot serve.
const EXPORT = process.env.MARKAI_EXPORT === "1";

const nextConfig: NextConfig = {
  // The browser tests build their own copy (.next-e2e, pointed at the test API) next to the real one.
  distDir: process.env.MARKAI_DIST_DIR || (EXPORT ? "out" : ".next"),
  pageExtensions: EXPORT ? ["tsx", "ts", "jsx", "js"] : ["web.js", "tsx", "ts", "jsx", "js"],
  ...(EXPORT
    ? { output: "export" as const, trailingSlash: true, images: { unoptimized: true } }
    : {
        async rewrites() {
          return [{ source: "/api/:path*", destination: `${API_INTERN}/:path*` }];
        },
      }),
  experimental: {
    agentFeedback: true,
    // /api/* requests are buffered up to this size (default 10 MB, even for a plain rewrite like ours); a larger body is cut off
    // silently. The API reads (and refuses with a clear 413) uploads up to 4 times its limit plus 1 MB, so the proxy passes as much.
    proxyClientMaxBodySize: `${4 * (MAX_FISIER_MB + 1)}mb`,
  },
  cacheComponents: false,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
