import { defineConfig } from "@playwright/test";

// Browser tests. They need the API and the website to be running already.
// Do not start them by hand: run scripts/run-e2e.ps1 from the repository root.
// That script starts everything on temporary databases and cleans up afterwards.
export default defineConfig({
  testDir: "./e2e",
  // The tests tell one story in order (admin registers a teacher, the teacher logs in, ...).
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  reporter: [["list"], ["json", { outputFile: "e2e-results.json" }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:5183",
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
  },
});
