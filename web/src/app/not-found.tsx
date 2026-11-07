"use client";
import { Heading, Link, PageLayout, Stack } from "@primer/react";
import NextLink from "next/link";
import { useI18n } from "@/i18n/provider";
import { Language } from "@/components/Language";

export default function NotFound() {
  const { t } = useI18n();
  return (
    <PageLayout containerWidth="medium">
      <PageLayout.Content>
        <Stack gap="normal" padding="normal">
          <Heading as="h1">{t("notFound")}</Heading>
          <Language />
          <Link as={NextLink} href="/targets">
            {t("backToTargets")}
          </Link>
        </Stack>
      </PageLayout.Content>
    </PageLayout>
  );
}
