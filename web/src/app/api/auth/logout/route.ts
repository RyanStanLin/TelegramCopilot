import { cookies } from "next/headers";
import { sameOrigin, authError } from "@/lib/server/session";
export async function POST(request: Request) {
  try {
    sameOrigin(request);
    (await cookies()).delete("copilot-session");
    return Response.json({ ok: true });
  } catch (e) {
    return authError(e);
  }
}
