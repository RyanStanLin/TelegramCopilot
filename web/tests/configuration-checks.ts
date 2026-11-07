import { expect, type Page } from "@playwright/test";
import { configurationFixtures } from "./configuration-fixtures";

export async function verifyConfigurationPages(page: Page) {
  for (const fixture of configurationFixtures) {
    await page.goto(`/${fixture.kind}/new`);
    const form = page.getByRole("main").locator("form").first();
    if (fixture.kind === "accounts") {
      await form.getByRole("button", { name: "保存更改", exact: true }).click();
      await expect(form.getByLabel(/^名称/)).toBeFocused();
    }
    await form.getByLabel(/^名称/).fill(fixture.name);
    if (fixture.kind !== "settings")
      await form.getByLabel("启用", { exact: true }).uncheck();
    for (const field of fixture.fields) {
      const control = form.getByLabel(new RegExp(`^${field.label}\\s*\\*?$`));
      if ("option" in field)
        await control.selectOption({ label: field.option });
      else if ("checked" in field) await control.setChecked(field.checked);
      else await control.fill(field.value);
    }
    await form.getByRole("button", { name: "保存更改", exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/${fixture.kind}$`));
    if (fixture.kind === "accounts") {
      await expect
        .poll(async () => {
          const response = await page.request.get("/api/backend/events", {
            params: {
              pageSize: 500,
              from: new Date(Date.now() - 86400_000).toISOString(),
              to: new Date(Date.now() + 60_000).toISOString(),
            },
          });
          expect(response.status()).toBe(200);
          return (await response.json()).items.some(
            (event: { name: string }) => event.name === fixture.name,
          );
        })
        .toBe(true);
      await page.goto("/events");
      await expect(
        page.getByRole("cell", { name: fixture.name, exact: true }).first(),
      ).toBeVisible();
      await page.goto("/accounts");
    }
    await page.getByRole("link", { name: fixture.name, exact: true }).click();
    const editor = page.getByRole("main").locator("form").first();
    await expect(editor.getByLabel(/^名称/)).toHaveValue(fixture.name);
    if (fixture.kind === "accounts") {
      await expect(editor.getByLabel(/^API 哈希/)).toHaveValue("***");
      await editor.getByLabel(/^名称/).fill("Unsaved value");
      const cancel = editor.getByRole("button", { name: "取消", exact: true });
      await cancel.click();
      const dialog = page.getByRole("alertdialog");
      await expect(
        dialog.getByRole("button", { name: "返回", exact: true }),
      ).toBeFocused();
      await page.keyboard.press("Escape");
      await expect(dialog).toHaveCount(0);
      await expect(cancel).toBeFocused();
      await expect(editor.getByLabel(/^名称/)).toHaveValue("Unsaved value");
    }
    if (fixture.kind === "destinations") {
      await expect(editor.getByLabel(/^设备密钥/)).toHaveValue("***");
      await expect(editor.getByLabel(/^加密密钥/)).toHaveValue("***");
    }
    await editor.getByLabel(/^名称/).fill(`${fixture.name} updated`);
    await editor.getByRole("button", { name: "保存更改", exact: true }).click();
    await expect(
      page.getByRole("link", { name: `${fixture.name} updated`, exact: true }),
    ).toBeVisible();
  }
  for (const fixture of [...configurationFixtures].reverse()) {
    await page.goto(`/${fixture.kind}`);
    await page
      .getByRole("button", {
        name: `删除 ${fixture.name} updated`,
        exact: true,
      })
      .click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: "删除配置", exact: true })
      .click();
    await expect(
      page.getByRole("link", { name: `${fixture.name} updated`, exact: true }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("textbox", { name: "按名称搜索", exact: true }),
    ).toBeFocused();
  }
}
