"use client";
import { Heading, Link, Stack } from "@primer/react";
import NextLink from "next/link";
import { useI18n } from "@/i18n/provider";
import { query } from "@/lib/api";
import type { Bucket } from "@/lib/types";
import css from "./Contribution.module.css";
import { ContributionLegend } from "./ContributionLegend";
export function Contribution({
  days,
  target,
  timeZone,
}: {
  days: Bucket[];
  target: string;
  timeZone: string;
}) {
  const { t, locale } = useI18n();
  const day = new Intl.DateTimeFormat(locale, { timeZone, dateStyle: "long" });
  const number = new Intl.NumberFormat(locale);
  const labels = new Intl.DateTimeFormat(locale, {
    weekday: "short",
    timeZone: "UTC",
  });
  const order = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
  const offset = days[0]
    ? order.indexOf(
        new Intl.DateTimeFormat("en", { weekday: "short", timeZone }).format(
          new Date(days[0].from),
        ),
      )
    : 0;
  return (
    <section>
      <Stack gap="condensed">
        <Heading as="h2" variant="small">
          {t("contributions")}
        </Heading>
        {days.length > 0 && (
          <span>
            {day.format(new Date(days[0].from))} –{" "}
            {day.format(new Date(days[days.length - 1].from))}
          </span>
        )}
        <div
          className={css.scroll}
          role="region"
          aria-label={t("contributions")}
          tabIndex={0}
        >
          <div className={css.calendar}>
            {order.map((value, index) => (
              <span key={value} className={css.weekday}>
                {labels.format(new Date(Date.UTC(2026, 0, 5 + index, 12)))}
              </span>
            ))}
            {Array.from({ length: offset }, (_, index) => (
              <span key={`empty-${index}`} aria-hidden="true" />
            ))}
            {days.map((d) => {
              const level =
                d.count === 0
                  ? 0
                  : d.count < 3
                    ? 1
                    : d.count < 6
                      ? 2
                      : d.count < 10
                        ? 3
                        : 4;
              const label = `${day.format(new Date(d.from))}: ${t("count")} ${number.format(d.count)}, ${t("onlineSeconds")} ${number.format(d.onlineSeconds)}, ${t("unknownSeconds")} ${number.format(d.unknownSeconds)}`;
              return (
                <Link
                  as={NextLink}
                  key={d.from}
                  href={`/presence?${query({ target, from: d.from, to: d.to, timeZone, period: "day" })}`}
                  className={css.cell}
                  data-level={level}
                  data-unknown={d.unknownSeconds > 0}
                  aria-label={label}
                />
              );
            })}
          </div>
        </div>
        <ContributionLegend />
      </Stack>
    </section>
  );
}
