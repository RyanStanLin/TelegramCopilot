import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  workers: 1,
  timeout: 120_000,
  use: {
    baseURL: process.env.TEST_WEB_URL ?? "http://localhost:3000",
    channel: "chrome",
    headless: true,
    actionTimeout: 10_000,
    navigationTimeout: 20_000,
  },
  reporter: [["list"], ["html", { open: "never" }]],
});
