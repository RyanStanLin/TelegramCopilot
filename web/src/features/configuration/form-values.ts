import type { Kind } from "@/lib/types";
import { schema } from "./schema";
export function formValues(kind: Kind, input: Record<string, unknown> = {}) {
  const values: Record<string, unknown> = {};
  schema[kind].forEach((f) => {
    values[f.key] = input[f.key] ?? f.initial ?? "";
  });
  if (kind === "destinations") {
    const e = input.encryption as {
      key: string;
      iv: string;
      mode: string;
    } | null;
    Object.assign(values, {
      encryptionEnabled: !!e,
      encryptionKey: e?.key ?? "",
      encryptionIV: e?.iv ?? "",
      encryptionMode: e?.mode ?? "CBC",
    });
  }
  schema[kind].forEach((f) => {
    if (f.type === "json" && typeof values[f.key] !== "string")
      values[f.key] = JSON.stringify(values[f.key], null, 2);
    if (f.type === "list" && Array.isArray(values[f.key]))
      values[f.key] = (values[f.key] as string[]).join("\n");
  });
  return values;
}
export function settingsValues(kind: Kind, input: Record<string, unknown>) {
  const values = { ...input };
  schema[kind].forEach((f) => {
    if (f.type === "number") values[f.key] = Number(values[f.key]);
    if (f.type === "json")
      values[f.key] = JSON.parse(String(values[f.key] || "{}"));
    if (f.type === "list")
      values[f.key] = String(values[f.key])
        .split("\n")
        .map((s) => s.trim())
        .filter(Boolean);
    if (f.reference && !values[f.key]) values[f.key] = null;
  });
  if (kind === "destinations") {
    values.encryption = values.encryptionEnabled
      ? {
          key: values.encryptionKey,
          iv: values.encryptionIV,
          mode: values.encryptionMode,
        }
      : null;
    [
      "encryptionEnabled",
      "encryptionKey",
      "encryptionIV",
      "encryptionMode",
    ].forEach((k) => delete values[k]);
  }
  return values;
}
