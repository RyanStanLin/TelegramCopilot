"use client";
import { Button, Link, NavList, PageLayout, Stack } from "@primer/react";
import NextLink from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useI18n } from "@/i18n/provider";
import { Language } from "./Language";
import css from "./Shell.module.css";
const sections = [
  "targets",
  "presence",
  "accounts",
  "schedules",
  "destinations",
  "rules",
  "events",
  "deliveries",
  "settings",
];
export function Shell({ children }: { children: React.ReactNode }) {
  const { t } = useI18n();
  const path = usePathname();
  const router = useRouter();
  return (
    <>
      <header className={css.header}>
        <Stack
          direction={{ narrow: "vertical", regular: "horizontal" }}
          align="start"
          justify="space-between"
          padding="normal"
        >
          <Stack gap="condensed">
            <strong>TelegramCopilot</strong>
            <Link href="#content">{t("skip")}</Link>
          </Stack>
          <Stack direction="horizontal" align="end" gap="normal" wrap="wrap">
            <Language />
            <Button
              size="medium"
              onClick={async () => {
                await fetch("/api/auth/logout", { method: "POST" });
                router.replace("/login");
              }}
            >
              {t("logout")}
            </Button>
          </Stack>
        </Stack>
      </header>
      <PageLayout containerWidth="xlarge" padding="normal">
        <PageLayout.Pane
          position="start"
          width="small"
          aria-label={t("settings")}
        >
          <NavList aria-label="TelegramCopilot">
            {sections.map((s) => (
              <NavList.Item
                as={NextLink}
                key={s}
                href={`/${s}`}
                aria-current={path.startsWith(`/${s}`) ? "page" : undefined}
              >
                {t(s)}
              </NavList.Item>
            ))}
          </NavList>
        </PageLayout.Pane>
        <PageLayout.Content>
          <section id="content" tabIndex={-1}>
            {children}
          </section>
        </PageLayout.Content>
      </PageLayout>
    </>
  );
}
