import { authenticated, origin } from "@/lib/server/session";
export const runtime = "nodejs";
export const dynamic = "force-dynamic";
async function proxy(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  if (!(await authenticated()))
    return Response.json({ code: "Unauthenticated" }, { status: 401 });
  if (request.method !== "GET" && request.headers.get("origin") !== origin())
    return Response.json({ code: "OriginMismatch" }, { status: 403 });
  const { path } = await context.params;
  if (
    !path.length ||
    path.some((s) => !/^[a-zA-Z0-9_.-]+$/.test(s) || s === ".." || s === ".")
  )
    return Response.json({ code: "InvalidPath" }, { status: 400 });
  const base = process.env.TELEGRAMCOPILOT_API_URL ?? "http://127.0.0.1:5080";
  const url = new URL(
    `/api/${path.join("/")}${new URL(request.url).search}`,
    base,
  );
  try {
    const response = await fetch(url, {
      method: request.method,
      cache: "no-store",
      redirect: "error",
      headers: { "Content-Type": "application/json" },
      body:
        request.method === "GET" || request.method === "HEAD"
          ? undefined
          : await request.text(),
      signal: AbortSignal.timeout(90_000),
    });
    const headers = new Headers({ "Cache-Control": "no-store" });
    for (const key of ["Content-Type", "Content-Disposition"]) {
      const value = response.headers.get(key);
      if (value) headers.set(key, value);
    }
    return new Response(response.body, { status: response.status, headers });
  } catch {
    return Response.json({ code: "BackendUnavailable" }, { status: 502 });
  }
}
export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE };
