export class ApiError extends Error {
  constructor(
    public status: number,
    public code?: string,
  ) {
    super(`HTTP ${status}`);
  }
}
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/backend${path}`, {
    ...init,
    cache: "no-store",
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    if (response.status === 401) window.location.assign("/login");
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.code);
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}
export function query(values: Record<string, string | number | undefined>) {
  const search = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== "") search.set(key, String(value));
  });
  return search.toString();
}
export async function allResources(kind: string) {
  const rows: import("./types").Resource[] = [];
  for (let page = 1; ; page++) {
    const result = await api<
      import("./types").Page<import("./types").Resource>
    >(`/${kind}?page=${page}&pageSize=500`);
    rows.push(...result.items);
    if (rows.length >= result.total) return rows;
  }
}
