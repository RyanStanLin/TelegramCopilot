const origin = new URL(process.env.TEST_WEB_URL ?? "http://localhost:3000")
  .origin;
import {
  expect,
  type APIRequestContext,
  type Page,
  type BrowserContext,
} from "@playwright/test";
export async function beginRegistrationRace(request: APIRequestContext) {
  const firstOptions = await request.post("/api/auth/options", {
    headers: { Origin: origin },
  });
  expect((await firstOptions.json()).mode).toBe("register");
}
export async function verifySingleton(request: APIRequestContext) {
  const competingRegistration = await request.post("/api/auth/verify", {
    headers: { Origin: origin },
    data: {},
  });
  expect(competingRegistration.status()).toBe(409);
  expect((await competingRegistration.json()).code).toBe(
    "PasskeyAlreadyRegistered",
  );
  const replay = await request.post("/api/auth/verify", {
    headers: { Origin: origin },
    data: {},
  });
  expect(replay.status()).toBe(400);
  expect((await replay.json()).code).toBe("InvalidChallenge");
  const subsequent = await request.post("/api/auth/options", {
    headers: { Origin: origin },
  });
  expect((await subsequent.json()).mode).toBe("authenticate");
}

export async function registerOwner(page: Page, context: BrowserContext) {
  const cdp = await context.newCDPSession(page);
  await cdp.send("WebAuthn.enable");
  await cdp.send("WebAuthn.addVirtualAuthenticator", {
    options: {
      protocol: "ctap2",
      transport: "internal",
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  });
  await page.goto("/targets");
  await expect(page).toHaveURL(/login/);
  await page.getByRole("button", { name: "注册通行密钥", exact: true }).click();
  await expect(page).toHaveURL(/targets/);
  return cdp;
}
