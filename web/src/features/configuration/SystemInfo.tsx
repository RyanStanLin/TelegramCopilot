"use client";
import { Button, Heading, Stack } from "@primer/react";
import { useState } from "react";
import { useI18n } from "@/i18n/provider";
import { useLoad } from "@/lib/use-load";
import { Failure, Loading } from "@/components/Status";
export function SystemInfo() {
  const { t, locale } = useI18n();
  const [revision, refresh] = useState(0);
  const health = useLoad<{
    status: string;
    heartbeat?: string;
    nativeAvailable: boolean;
  }>("/health", revision);
  return (
    <section>
      <Stack gap="normal">
        <Heading as="h2" variant="small">
          {t("health")}
        </Heading>
        {health.loading ? (
          <Loading />
        ) : health.error ? (
          <Failure retry={() => refresh((v) => v + 1)} />
        ) : (
          health.data && (
            <p>
              {t(health.data.status)} · {t("heartbeat")}:{" "}
              {health.data.heartbeat
                ? new Intl.DateTimeFormat(locale, {
                    dateStyle: "medium",
                    timeStyle: "medium",
                  }).format(new Date(health.data.heartbeat))
                : t("none")}
            </p>
          )
        )}
        <Button size="medium" onClick={() => refresh((v) => v + 1)}>
          {t("statusRefresh")}
        </Button>
        <Heading as="h2" variant="small">
          {t("security")}
        </Heading>
        <p>{t("registered")}</p>
      </Stack>
    </section>
  );
}
