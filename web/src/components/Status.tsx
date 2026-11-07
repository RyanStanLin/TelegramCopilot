"use client";
import { Banner, Button, Spinner, Stack } from "@primer/react";
import { useEffect, useRef } from "react";
import { useI18n } from "@/i18n/provider";
export function Loading() {
  const { t } = useI18n();
  return <Spinner size="medium" srText={t("loading")} delay />;
}
export function Failure({
  retry,
  message = "error",
  focus = false,
}: {
  retry?: () => void;
  message?: string;
  focus?: boolean;
}) {
  const { t } = useI18n();
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (focus) ref.current?.focus();
  }, [focus]);
  return (
    <div ref={ref} tabIndex={-1}>
      <Banner
        variant="critical"
        title={t(message)}
        primaryAction={
          retry ? (
            <Button size="medium" onClick={retry}>
              {t("retry")}
            </Button>
          ) : undefined
        }
      />
    </div>
  );
}
export function Pager({
  page,
  total,
  size,
  change,
}: {
  page: number;
  total: number;
  size: number;
  change: (page: number) => void;
}) {
  const { t, locale } = useI18n();
  return (
    <Stack direction="horizontal" align="center" gap="normal" wrap="wrap">
      {page > 1 && (
        <Button size="medium" onClick={() => change(page - 1)}>
          {t("previous")}
        </Button>
      )}
      <span role="status">
        {t("page")} {new Intl.NumberFormat(locale).format(page)} · {t("total")}{" "}
        {new Intl.NumberFormat(locale).format(total)}
      </span>
      {page * size < total && (
        <Button size="medium" onClick={() => change(page + 1)}>
          {t("next")}
        </Button>
      )}
    </Stack>
  );
}
