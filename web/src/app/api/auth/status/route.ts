import { credential } from "@/lib/server/store";
import { authenticated } from "@/lib/server/session";
export const runtime = "nodejs";
export async function GET() {
  const key = credential();
  return Response.json(
    {
      registered: !!key,
      authenticated: await authenticated(),
      registeredAt: key?.registeredAt,
    },
    { headers: { "Cache-Control": "no-store" } },
  );
}
