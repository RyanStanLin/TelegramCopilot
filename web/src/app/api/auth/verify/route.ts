import {
  verifyAuthenticationResponse,
  verifyRegistrationResponse,
} from "@simplewebauthn/server";
import { cookies } from "next/headers";
import {
  consumeChallenge,
  credential,
  saveCredential,
  updateCounter,
} from "@/lib/server/store";
import {
  authError,
  issueSession,
  origin,
  rpId,
  sameOrigin,
} from "@/lib/server/session";
export const runtime = "nodejs";
export async function POST(request: Request) {
  try {
    sameOrigin(request);
    if (Number(request.headers.get("content-length") ?? 0) > 65536)
      throw new Error("VerificationFailed");
    const id = (await cookies()).get("copilot-challenge")?.value;
    (await cookies()).delete("copilot-challenge");
    const challenge = id ? consumeChallenge(id) : null;
    if (!challenge || challenge.expires < Date.now())
      throw new Error("InvalidChallenge");
    const response = await request.json();
    const key = credential();
    if (challenge.mode === "register") {
      if (key) throw new Error("PasskeyAlreadyRegistered");
      const result = await verifyRegistrationResponse({
        response,
        expectedChallenge: challenge.challenge,
        expectedOrigin: origin(),
        expectedRPID: rpId(),
        requireUserVerification: true,
      });
      if (!result.verified || !result.registrationInfo)
        throw new Error("VerificationFailed");
      const value = result.registrationInfo.credential;
      try {
        saveCredential({
          ...value,
          publicKey: Buffer.from(value.publicKey).toString("base64url"),
          registeredAt: new Date().toISOString(),
        });
      } catch {
        throw new Error("PasskeyAlreadyRegistered");
      }
      await issueSession(value.id);
    } else {
      if (!key) throw new Error("VerificationFailed");
      const result = await verifyAuthenticationResponse({
        response,
        expectedChallenge: challenge.challenge,
        expectedOrigin: origin(),
        expectedRPID: rpId(),
        requireUserVerification: true,
        credential: {
          ...key,
          publicKey: new Uint8Array(Buffer.from(key.publicKey, "base64url")),
        },
      });
      if (!result.verified) throw new Error("VerificationFailed");
      updateCounter(key.id, key.counter, result.authenticationInfo.newCounter);
      await issueSession(key.id);
    }
    return Response.json({ verified: true });
  } catch (e) {
    return authError(e);
  }
}
