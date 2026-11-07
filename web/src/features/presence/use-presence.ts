"use client";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { allResources, query } from "@/lib/api";
import { useLoad } from "@/lib/use-load";
import type { Interval, Resource, Statistics, Page } from "@/lib/types";
import type { PresenceFilter } from "./PresenceFilters";
export function usePresence() {
  const params = useSearchParams();
  const router = useRouter();
  const [targets, setTargets] = useState<Resource[]>([]);
  const [error, setError] = useState(false);
  const [revision, refresh] = useState(0);
  const defaults = useMemo(
    () => ({
      from: new Date(Date.now() - 30 * 86400_000).toISOString(),
      to: new Date().toISOString(),
    }),
    [],
  );
  const current: PresenceFilter = {
    target: params.get("target") ?? "",
    from: params.get("from") ?? defaults.from,
    to: params.get("to") ?? defaults.to,
    timeZone: params.get("timeZone") ?? "Asia/Taipei",
    period: params.get("period") ?? "day",
    state: params.get("state") ?? "",
  };
  const page = Math.max(1, Number(params.get("page")) || 1);
  const bucketPage = Math.max(1, Number(params.get("bucketPage")) || 1);
  const valid =
    Number.isFinite(Date.parse(current.from)) &&
    Number.isFinite(Date.parse(current.to)) &&
    Date.parse(current.from) < Date.parse(current.to);
  const base = current.target && valid ? `/targets/${current.target}` : null;
  const options = {
    from: current.from,
    to: current.to,
    timeZone: current.timeZone,
    period: current.period,
  };
  const timeline = useLoad<Page<Interval>>(
    base
      ? `${base}/timeline?${query({ ...options, page, pageSize: 25, state: current.state })}`
      : null,
    revision,
  );
  const statistics = useLoad<Statistics>(
    base
      ? `${base}/statistics?${query({ ...options, page: bucketPage, pageSize: 25 })}`
      : null,
    revision,
  );
  const calendarFrom = new Date(
    Math.max(
      Date.parse(current.from),
      Date.parse(current.to) - 366 * 86400_000,
    ),
  );
  const calendar = useLoad<Statistics>(
    base
      ? `${base}/statistics?${query({ ...options, from: calendarFrom.toISOString(), period: "day", pageSize: 500 })}`
      : null,
    revision,
  );
  useEffect(() => {
    let active = true;
    setError(false);
    allResources("targets")
      .then((v) => {
        if (active) setTargets(v);
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, [revision]);
  const navigate = (extra: Record<string, string | number>) =>
    router.push(
      `/presence?${query({ ...current, page, bucketPage, ...extra })}`,
    );
  return {
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
  };
}
