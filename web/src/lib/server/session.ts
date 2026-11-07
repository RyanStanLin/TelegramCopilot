import "server-only";
import { createHmac, timingSafeEqual } from "node:crypto";
import { cookies } from "next/headers";
import { credential, secret } from "./store";

export const origin = () =>
  process.env.WEBAUTHN_ORIGIN ?? "http://localhost:3000";
export const rpId = () =>
  process.env.WEBAUTHN_RP_ID ?? new URL(origin()).hostname;
export const cookieOptions = () => ({
  httpOnly: true,
  secure: new URL(origin()).protocol === "https:",
  sameSite: "strict" as const,
  path: "/",
});
export function sameOrigin(request: Request) {
  if (request.headers.get("origin") !== origin())
    throw new Error("OriginMismatch");
}
function sign(value: string) {
  return createHmac("sha256", secret("session-key"))
    .update(value)
    .digest("base64url");
}
export async function issueSession(id: string) {
  const value = Buffer.from(
    JSON.stringify({ id, exp: Date.now() + 8 * 60 * 60 * 1000 }),
  ).toString("base64url");
  (await cookies()).set("copilot-session", `${value}.${sign(value)}`, {
    ...cookieOptions(),
    maxAge: 8 * 60 * 60,
  });
}
export async function authenticated() {
  const token = (await cookies()).get("copilot-session")?.value;
  if (!token) return false;
  try {
    const [value, signature, extra] = token.split(".");
    if (!value || !signature || extra) return false;
    const expected = Buffer.from(sign(value));
    const received = Buffer.from(signature);
    if (
      expected.length !== received.length ||
      !timingSafeEqual(expected, received)
    )
      return false;
    const data = JSON.parse(Buffer.from(value, "base64url").toString());
    return (
      typeof data.exp === "number" &&
      data.exp > Date.now() &&
      credential()?.id === data.id
    );
  } catch {
    return false;
  }
}
export function authError(error: unknown) {
  const code =
    error instanceof Error &&
    [
      "OriginMismatch",
      "AuthenticationConflict",
      "PasskeyAlreadyRegistered",
      "InvalidChallenge",
      "VerificationFailed",
    ].includes(error.message)
      ? error.message
      : "VerificationFailed";
  return Response.json(
    { code },
    { status: code === "PasskeyAlreadyRegistered" ? 409 : 400 },
  );
}
