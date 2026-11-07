"use client";
import { useI18n } from "@/i18n/provider";
import { RecordsTable } from "@/components/RecordsTable";
import type { Bucket } from "@/lib/types";
export function StatisticsTable({
  rows,
  title,
  timeZone,
}: {
  rows: Bucket[];
  title: string;
  timeZone: string;
}) {
  const { t, locale } = useI18n();
  const number = (value: number | undefined) =>
    value === undefined || value === null
      ? t("none")
      : new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(
          value,
        );
  return (
    <RecordsTable<Bucket>
      title={title}
      rows={rows}
      rowId={(r) => r.from}
      columns={[
        {
          key: "from",
          label: "from",
          render: (r) =>
            new Intl.DateTimeFormat(locale, {
              timeZone,
              dateStyle: "medium",
            }).format(new Date(r.from)),
        },
        ...[
          "count",
          "onlineSeconds",
          "offlineSeconds",
          "unknownSeconds",
          "averageSessionSeconds",
        ].map((key) => ({
          key,
          label: key,
          render: (r: Bucket) => number(r[key as keyof Bucket] as number),
        })),
        {
          key: "coverage",
          label: "coverageRatio",
          render: (r) =>
            new Intl.NumberFormat(locale, {
              style: "percent",
              maximumFractionDigits: 2,
            }).format(r.coverageRatio),
        },
      ]}
    />
  );
}
