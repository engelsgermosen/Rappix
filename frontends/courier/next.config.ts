import path from "node:path";

import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // Cada frontend es un proyecto independiente: fija la raiz para que Next no la
  // infiera de un lockfile en un directorio padre (cambiaria la ruta de standalone).
  outputFileTracingRoot: path.join(__dirname),
  reactStrictMode: true,
  images: {
    remotePatterns: [
      { protocol: "http", hostname: "localhost" },
      { protocol: "http", hostname: "minio" },
      { protocol: "https", hostname: "**" },
    ],
  },
};

export default nextConfig;
