"use client";
import { Button, FormControl, Select, Stack, TextInput } from "@primer/react";
import { useState } from "react";
import { useI18n } from "@/i18n/provider";
import type { Resource } from "@/lib/types";
export type PresenceFilter = {
  target: string;
  from: string;
  to: string;
  timeZone: string;
  period: string;
  state: string;
};
export function PresenceFilters({
  current,
  targets,
  apply,
}: {
  current: PresenceFilter;
  targets: Resource[];
  apply: (value: PresenceFilter) => void;
}) {
  const { t } = useI18n();
  const [values, setValues] = useState(current);
  const change = (key: keyof PresenceFilter, value: string) =>
    setValues((v) => ({ ...v, [key]: value }));
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        apply(values);
      }}
    >
      <Stack gap="normal">
        <Stack direction="horizontal" gap="normal" wrap="wrap" align="end">
          <FormControl>
            <FormControl.Label>{t("targetId")}</FormControl.Label>
            <Select
              size="medium"
              value={values.target}
              onChange={(e) => change("target", e.target.value)}
            >
              <Select.Option value="">{t("none")}</Select.Option>
              {targets.map((r) => (
                <Select.Option key={r.id} value={r.id}>
                  {r.name}
                </Select.Option>
              ))}
            </Select>
          </FormControl>
          {(["from", "to", "timeZone"] as const).map((key) => (
            <FormControl key={key}>
              <FormControl.Label>{t(key)}</FormControl.Label>
              <TextInput
                size="medium"
                value={values[key]}
                onChange={(e) => change(key, e.target.value)}
              />
            </FormControl>
          ))}
          <FormControl>
            <FormControl.Label>{t("period")}</FormControl.Label>
            <Select
              size="medium"
              value={values.period}
              onChange={(e) => change("period", e.target.value)}
            >
              {["day", "week", "month", "year"].map((p) => (
                <Select.Option key={p} value={p}>
                  {t(p)}
                </Select.Option>
              ))}
            </Select>
          </FormControl>
          <FormControl>
            <FormControl.Label>{t("state")}</FormControl.Label>
            <Select
              size="medium"
              value={values.state}
              onChange={(e) => change("state", e.target.value)}
            >
              <Select.Option value="">{t("all")}</Select.Option>
              {["Online", "Offline", "Unknown"].map((p) => (
                <Select.Option key={p} value={p}>
                  {t(p)}
                </Select.Option>
              ))}
            </Select>
          </FormControl>
          <Button size="medium" type="submit">
            {t("apply")}
          </Button>
        </Stack>
      </Stack>
    </form>
  );
}
