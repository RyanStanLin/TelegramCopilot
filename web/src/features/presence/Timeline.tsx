"use client";
import { useI18n } from "@/i18n/provider";
import { RecordsTable } from "@/components/RecordsTable";
import type { Interval } from "@/lib/types";
export function Timeline({
  rows,
  timeZone,
}: {
  rows: Interval[];
  timeZone: string;
}) {
  const { t, locale } = useI18n();
  const date = (value: string) =>
    new Intl.DateTimeFormat(locale, {
      dateStyle: "medium",
      timeStyle: "medium",
      timeZone,
    }).format(new Date(value));
  const number = (v?: number) =>
    v === undefined || v === null
      ? t("none")
      : new Intl.NumberFormat(locale, { maximumFractionDigits: 3 }).format(v);
  return (
    <RecordsTable<Interval>
      title={t("timeline")}
      rows={rows}
      rowId={(r) => `${r.id}-${r.start}`}
      columns={[
        { key: "state", label: "state", render: (r) => t(r.state) },
        {
          key: "start",
          label: "start",
          render: (r) => date(r.start),
        },
        { key: "end", label: "end", render: (r) => date(r.end) },
        {
          key: "seconds",
          label: "seconds",
          render: (r) => number(r.seconds),
        },
        {
          key: "gap",
          label: "gap",
          render: (r) => number(r.gapSincePreviousOnlineSeconds),
        },
        {
          key: "reason",
          label: "reason",
          render: (r) => t(r.reason),
        },
        {
          key: "boundary",
          label: "boundary",
          render: (r) => t(r.startKnown && r.endKnown ? "known" : "partial"),
        },
        {
          key: "observed",
          label: "observedAt",
          render: (r) => date(r.observedAt),
        },
        {
          key: "telegram",
          label: "telegramAt",
          render: (r) => (r.telegramAt ? date(r.telegramAt) : t("none")),
        },
      ]}
    />
  );
}
