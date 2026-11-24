/// <reference types="vitest" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// During development the website runs on port 5173 and the API on port 5241.
// The proxy sends every "/api" call to the API, so the browser sees one address
// and no cross-site (CORS) setup is needed while developing.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: process.env.API_URL ?? "http://localhost:5241",
        changeOrigin: true,
      },
    },
  },
  test: {
    // Unit tests live next to the code. The browser tests in "e2e" are run by Playwright, not here.
    include: ["src/**/*.test.ts"],
  },
});
