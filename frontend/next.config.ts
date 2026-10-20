import type { NextConfig } from "next";

// The browser talks only to this site; /api/... is forwarded to the .NET API (database/src/MarkAi.Api).
// Same origin means the login cookie is first-party on every device, including behind a public tunnel.
const API_INTERN = process.env.API_INTERN_URL || "http://localhost:5080";

const nextConfig: NextConfig = {
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${API_INTERN}/:path*` }];
  },
  experimental: {
    agentFeedback: true,
    // /api/* requests are buffered up to this size (default 10 MB, even for a plain rewrite like ours). Chapter files are up to
    // 20 MB and the API refuses larger requests itself (21 MB), so the proxy must let a little more through.
    proxyClientMaxBodySize: "22mb",
  },
  cacheComponents: true,
  partialPrefetching: true,
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
