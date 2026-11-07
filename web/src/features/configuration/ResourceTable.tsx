"use client";
import { Button, Link, Stack } from "@primer/react";
import NextLink from "next/link";
import { useI18n } from "@/i18n/provider";
import type { Kind, Resource } from "@/lib/types";
import { RecordsTable } from "@/components/RecordsTable";
export function ResourceTable({
  kind,
  rows,
  onDelete,
}: {
  kind: Kind;
  rows: Resource[];
  onDelete: (resource: Resource) => void;
}) {
  const { t, locale } = useI18n();
  const date = (value: string) =>
    new Intl.DateTimeFormat(locale, {
      dateStyle: "medium",
      timeStyle: "medium",
    }).format(new Date(value));
  return (
    <RecordsTable<Resource>
      title={t(kind)}
      rows={rows}
      rowId={(r) => r.id}
      columns={[
        {
          key: "name",
          label: "name",
          render: (r) => (
            <Link as={NextLink} href={`/${kind}/${r.id}`}>
              {r.name}
            </Link>
          ),
        },
        {
          key: "state",
          label: "state",
          render: (r) => t(r.enabled ? "enabled" : "disabled"),
        },
        {
          key: "updated",
          label: "updatedAt",
          render: (r) => date(r.updatedAt),
        },
        {
          key: "actions",
          label: "actions",
          render: (r) => (
            <Stack direction="horizontal" gap="condensed" wrap="wrap">
              {kind === "targets" && (
                <Link as={NextLink} href={`/presence?target=${r.id}`}>
                  {t("view")}
                </Link>
              )}
              <Button
                as={NextLink}
                href={`/${kind}/${r.id}`}
                size="medium"
                aria-label={`${t("edit")} ${r.name}`}
              >
                {t("edit")}
              </Button>
              <Button
                size="medium"
                variant="danger"
                aria-label={`${t("delete")} ${r.name}`}
                onClick={() => onDelete(r)}
              >
                {t("delete")}
              </Button>
            </Stack>
          ),
        },
      ]}
    />
  );
}
