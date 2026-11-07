"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { allResources, api } from "@/lib/api";
import { useLoad } from "@/lib/use-load";
import type { Kind, Resource } from "@/lib/types";
import { schema } from "./schema";
import { formValues, settingsValues } from "./form-values";
export function useResourceEditor(kind: Kind, id: string) {
  const router = useRouter();
  const form = useRef<HTMLFormElement>(null);
  const existing = useLoad<Resource>(id === "new" ? null : `/${kind}/${id}`);
  const [name, setName] = useState("");
  const [enabled, setEnabled] = useState(true);
  const [values, setValues] = useState(() => formValues(kind));
  const [references, setReferences] = useState<Record<string, Resource[]>>({});
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [invalid, setInvalid] = useState<string[]>([]);
  useEffect(() => {
    if (existing.data) {
      setName(existing.data.name);
      setEnabled(existing.data.enabled);
      setValues(formValues(kind, existing.data.settings));
    }
  }, [existing.data, kind]);
  useEffect(() => {
    let active = true;
    const kinds = [
      ...new Set(
        schema[kind].flatMap((f) => (f.reference ? [f.reference] : [])),
      ),
    ];
    Promise.all(kinds.map(async (k) => [k, await allResources(k)] as const))
      .then((pairs) => {
        if (active) setReferences(Object.fromEntries(pairs));
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, [kind]);
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (dirty) e.preventDefault();
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [dirty]);
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    const errors = schema[kind]
      .filter(
        (f) =>
          (!f.when || f.when(values)) &&
          f.required &&
          !String(values[f.key] ?? "").trim(),
      )
      .map((f) => f.key);
    if (!name.trim()) errors.unshift("name");
    try {
      settingsValues(kind, values);
    } catch {
      errors.push("parameters");
    }
    setInvalid(errors);
    setError(false);
    if (errors.length) {
      setTimeout(
        () =>
          form.current
            ?.querySelector<HTMLElement>('[aria-invalid="true"]')
            ?.focus(),
        0,
      );
      return;
    }
    setBusy(true);
    try {
      await api(`/${kind}${id === "new" ? "" : `/${id}`}`, {
        method: id === "new" ? "POST" : "PUT",
        body: JSON.stringify({
          name,
          enabled,
          settings: settingsValues(kind, values),
          revision: existing.data?.revision,
        }),
      });
      setDirty(false);
      router.push(`/${kind}`);
      router.refresh();
    } catch {
      setError(true);
    } finally {
      setBusy(false);
    }
  }
  return {
    router,
    form,
    existing,
    name,
    setName,
    enabled,
    setEnabled,
    values,
    setValues,
    references,
    dirty,
    setDirty,
    busy,
    error,
    invalid,
    save,
  };
}
