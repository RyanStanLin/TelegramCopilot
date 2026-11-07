"use client";
import { PageHeader, Stack } from "@primer/react";
import { useRouter, useSearchParams } from "next/navigation";
import { useMemo, useState } from "react";
import { useI18n } from "@/i18n/provider";
import { query } from "@/lib/api";
import { useLoad } from "@/lib/use-load";
import type { Row, Page } from "@/lib/types";
import { Failure, Loading, Pager } from "@/components/Status";
import { RecordsTable } from "@/components/RecordsTable";
import { HistoryFilters } from "./history/HistoryFilters";
export function History({ section }: { section: "events" | "deliveries" }) {
  const { t, locale } = useI18n();
  const params = useSearchParams();
  const router = useRouter();
  const defaults = useMemo(
    () => ({
      from: new Date(Date.now() - 7 * 86400_000).toISOString(),
      to: new Date().toISOString(),
    }),
    [],
  );
  const [from, setFrom] = useState(params.get("from") ?? defaults.from);
  const [to, setTo] = useState(params.get("to") ?? defaults.to);
  const [filter, setFilter] = useState(
    params.get(section === "events" ? "type" : "channel") ?? "",
  );
  const [entity, setEntity] = useState(
    params.get(section === "events" ? "targetId" : "resourceId") ?? "",
  );
  const page = Math.max(1, Number(params.get("page")) || 1);
  const [revision, refresh] = useState(0);
  const options = Object.fromEntries(params.entries());
  const data = useLoad<Page<Row>>(
    `/${section}?${query({ ...(section === "events" ? defaults : {}), ...options, page, pageSize: 25 })}`,
    revision,
  );
  const fields =
    section === "events"
      ? ["at", "name", "type", "onlineSeconds", "detail"]
      : [
          "dueAt",
          "channel",
          "resourceId",
          "status",
          "externalId",
          "error",
          "updatedAt",
        ];
  const render = (v: unknown, key: string) => {
    if (v === null || v === undefined) return t("none");
    if (["at", "dueAt", "updatedAt"].includes(key))
      return new Intl.DateTimeFormat(locale, {
        dateStyle: "medium",
        timeStyle: "medium",
      }).format(new Date(String(v)));
    if (typeof v === "number") return new Intl.NumberFormat(locale).format(v);
    if (
      ["type", "channel", "status"].includes(key) ||
      (key === "detail" &&
        [
          "Worker started",
          "Worker stopped",
          "Observation resumed",
          "ExpiredOnlineStatus",
        ].includes(String(v)))
    )
      return t(String(v));
    return String(v);
  };
  return (
    <Stack gap="normal">
      <PageHeader>
        <PageHeader.TitleArea>
          <PageHeader.Title as="h1">{t(section)}</PageHeader.Title>
        </PageHeader.TitleArea>
      </PageHeader>
      <HistoryFilters
        section={section}
        from={from}
        to={to}
        filter={filter}
        entity={entity}
        setFrom={setFrom}
        setTo={setTo}
        setFilter={setFilter}
        setEntity={setEntity}
        apply={() =>
          router.push(
            `/${section}?${query({ ...(section === "events" ? { from, to, type: filter, targetId: entity } : { channel: filter, resourceId: entity }), page: 1 })}`,
          )
        }
      />
      {data.loading ? (
        <Loading />
      ) : data.error ? (
        <Failure retry={() => refresh((v) => v + 1)} />
      ) : (
        data.data && (
          <>
            <RecordsTable<Row>
              title={t(section)}
              rows={data.data.items}
              rowId={(r) => String(r.id)}
              columns={fields.map((key) => ({
                key,
                label: key === "resourceId" ? "resource" : key,
                render: (r) => render(r[key], key),
              }))}
            />
            <Pager
              page={page}
              total={data.data.total}
              size={25}
              change={(p) =>
                router.push(`/${section}?${query({ ...options, page: p })}`)
              }
            />
          </>
        )
      )}
    </Stack>
  );
}
