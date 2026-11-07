import { expect, type Page, type TestInfo } from "@playwright/test";

export async function verifyPresenceLayout(page: Page) {
  await page.evaluate(() => document.fonts.ready);
  const headers = page.getByRole("table").first().getByRole("columnheader");
  const first = await headers.nth(0).boundingBox();
  const second = await headers.nth(1).boundingBox();
  expect(first).not.toBeNull();
  expect(second).not.toBeNull();
  expect(Math.abs(first!.y - second!.y)).toBeLessThan(1);
  expect(second!.x).toBeGreaterThan(first!.x);
  const days = page.locator("a[data-level]");
  const day = await days.nth(0).boundingBox();
  const nextWeek = await days.nth(7).boundingBox();
  expect(day!.width).toBeGreaterThanOrEqual(24);
  expect(day!.height).toBeGreaterThanOrEqual(24);
  expect(nextWeek!.x).toBeGreaterThan(day!.x);
  const tokens = await page.evaluate(() => {
    const style = getComputedStyle(document.documentElement);
    return [
      "--control-medium-size",
      "--stack-gap-condensed",
      "--borderWidth-thin",
    ].map((name) => style.getPropertyValue(name).trim());
  });
  expect(tokens.every(Boolean)).toBe(true);
}

export async function verifyResponsivePresence(page: Page, info: TestInfo) {
  for (const width of [768, 375, 320]) {
    await page.setViewportSize({ width, height: 900 });
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    );
    expect(overflow).toBeLessThanOrEqual(1);
    await expect(
      page.getByRole("link", { name: "导出 CSV", exact: true }),
    ).toBeVisible();
    if (width === 375)
      await page.screenshot({
        path: info.outputPath("presence-narrow.png"),
        fullPage: true,
      });
  }
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.emulateMedia({ colorScheme: "dark" });
  await page.screenshot({
    path: info.outputPath("presence-dark.png"),
    fullPage: false,
  });
  const theme = await page.evaluate(() => ({
    actual: getComputedStyle(document.body).backgroundColor,
    expected: getComputedStyle(document.documentElement)
      .getPropertyValue("--bgColor-default")
      .trim(),
  }));
  expect(theme.actual).not.toBe("rgba(0, 0, 0, 0)");
  expect(theme.expected).not.toBe("");
  await page.emulateMedia({ colorScheme: "light" });
  await page.evaluate(() => (document.documentElement.style.fontSize = "200%"));
  await expect(
    page.getByRole("link", { name: "导出 CSV", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth,
    ),
  ).toBeLessThanOrEqual(1);
  await page.evaluate(() => (document.documentElement.style.fontSize = ""));
}
