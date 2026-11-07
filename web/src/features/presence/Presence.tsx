"use client";
import { Button, PageHeader, Stack } from "@primer/react";
import { useI18n } from "@/i18n/provider";
import { query } from "@/lib/api";
import { Failure, Loading, Pager } from "@/components/Status";
import { Timeline } from "./Timeline";
import { PresenceFilters } from "./PresenceFilters";
import { StatisticsTable } from "./StatisticsTable";
import { Contribution } from "./Contribution";
import { usePresence } from "./use-presence";
export function Presence() {
  const { t } = useI18n();
  const {
    current,
    targets,
    params,
    router,
    base,
    valid,
    error,
    timeline,
    statistics,
    calendar,
    page,
    bucketPage,
    navigate,
    refresh,
  } = usePresence();
  return (
    <Stack gap="spacious">
      <PageHeader>
        <PageHeader.TitleArea>
          <PageHeader.Title as="h1">{t("presence")}</PageHeader.Title>
        </PageHeader.TitleArea>
        <PageHeader.Actions>
          {base && (
            <Button
              as="a"
              href={`/api/backend${base}/export.csv?${query({ from: current.from, to: current.to, state: current.state })}`}
              size="medium"
            >
              {t("export")}
            </Button>
          )}
        </PageHeader.Actions>
      </PageHeader>
      <PresenceFilters
        key={params.toString()}
        current={current}
        targets={targets}
        apply={(v) => router.push(`/presence?${query({ ...v, page: 1 })}`)}
      />
      <p>{t("precision")}</p>
      {!current.target ? (
        <p>{t("noTarget")}</p>
      ) : !valid ||
        error ||
        timeline.error ||
        statistics.error ||
        calendar.error ? (
        <Failure retry={() => refresh((v) => v + 1)} />
      ) : (
        <>
          {statistics.loading ? (
            <Loading />
          ) : (
            statistics.data && (
              <StatisticsTable
                title={t("summary")}
                rows={[statistics.data.totals]}
                timeZone={current.timeZone}
              />
            )
          )}
          {calendar.data && (
            <Contribution
              days={calendar.data.buckets.items}
              target={current.target}
              timeZone={current.timeZone}
            />
          )}
          {statistics.data && (
            <>
              <StatisticsTable
                title={t(current.period)}
                rows={statistics.data.buckets.items}
                timeZone={current.timeZone}
              />
              <Pager
                page={bucketPage}
                total={statistics.data.buckets.total}
                size={25}
                change={(p) => navigate({ bucketPage: p })}
              />
            </>
          )}
          {timeline.loading ? (
            <Loading />
          ) : (
            timeline.data && (
              <>
                <Timeline
                  rows={timeline.data.items}
                  timeZone={current.timeZone}
                />
                <Pager
                  page={page}
                  total={timeline.data.total}
                  size={25}
                  change={(p) => navigate({ page: p })}
                />
              </>
            )
          )}
        </>
      )}
    </Stack>
  );
}
