"use client";
import { Stack } from "@primer/react";
import { useI18n } from "@/i18n/provider";
import css from "./Contribution.module.css";

export function ContributionLegend() {
  const { t, locale } = useI18n();
  const number = new Intl.NumberFormat(locale);
  const ranges = [[0], [1, 2], [3, 5], [6, 9], [10]];
  return (
    <Stack direction="horizontal" gap="condensed" wrap="wrap" align="center">
      {ranges.map((range, level) => (
        <Stack
          key={level}
          direction="horizontal"
          gap="condensed"
          align="center"
        >
          <span
            className={`${css.cell} ${css.key}`}
            data-level={level}
            aria-hidden="true"
          />
          <span>
            {range.map((value) => number.format(value)).join("–")}
            {level === 4 && "+"}
          </span>
        </Stack>
      ))}
      <span>{t("unknownDay")}</span>
    </Stack>
  );
}
