"use client";
import { BaseStyles } from "@primer/react";
import { ThemeProvider } from "@primer/react/next";
import { I18nProvider } from "@/i18n/provider";
export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider colorMode="auto" dayScheme="light" nightScheme="dark">
      <BaseStyles>
        <I18nProvider>{children}</I18nProvider>
      </BaseStyles>
    </ThemeProvider>
  );
}
