import { test, expect } from "@playwright/test";

test("login_status_failure_can_be_retried", async ({ page }) => {
  let attempts = 0;
  await page.route("**/api/auth/status", async (route) => {
    attempts++;
    if (attempts === 1)
      await route.fulfill({ status: 503, body: "unavailable" });
    else await route.continue();
  });
  await page.goto("/login");
  await page.getByRole("button", { name: "重试", exact: true }).click();
  await expect(
    page.getByRole("button", { name: /^(注册通行密钥|使用通行密钥登录)$/ }),
  ).toBeVisible();
  expect(attempts).toBe(2);
});
