import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Self-contained server output for the Docker image (frontend/Dockerfile).
  output: "standalone",
};

export default nextConfig;
