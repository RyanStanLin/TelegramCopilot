import {
  generateAuthenticationOptions,
  generateRegistrationOptions,
} from "@simplewebauthn/server";
import { randomBytes } from "node:crypto";
import { cookies } from "next/headers";
import { credential, database, secret } from "@/lib/server/store";
import {
  sameOrigin,
  authError,
  cookieOptions,
  rpId,
} from "@/lib/server/session";
export const runtime = "nodejs";
export async function POST(request: Request) {
  try {
    sameOrigin(request);
    const db = database();
    db.prepare("DELETE FROM challenges WHERE expires<?").run(Date.now());
    const key = credential();
    const mode = key ? "authenticate" : "register";
    const options = key
      ? await generateAuthenticationOptions({
          rpID: rpId(),
          userVerification: "required",
          allowCredentials: [{ id: key.id, transports: key.transports }],
        })
      : await generateRegistrationOptions({
          rpName: "TelegramCopilot",
          rpID: rpId(),
          userName: "owner",
          userDisplayName: "TelegramCopilot",
          userID: Buffer.from(secret("user-id"), "base64url"),
          attestationType: "none",
          authenticatorSelection: {
            residentKey: "required",
            userVerification: "required",
          },
        });
    const id = randomBytes(32).toString("base64url");
    db.prepare("INSERT INTO challenges VALUES(?,?,?,?)").run(
      id,
      mode,
      options.challenge,
      Date.now() + 120_000,
    );
    (await cookies()).set("copilot-challenge", id, {
      ...cookieOptions(),
      maxAge: 120,
    });
    return Response.json(
      { mode, options },
      { headers: { "Cache-Control": "no-store" } },
    );
  } catch (e) {
    return authError(e);
  }
}
