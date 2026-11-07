"use client";
import { Button, FormControl, Select, Stack } from "@primer/react";
import { useEffect, useState } from "react";
import { useI18n, type Locale } from "@/i18n/provider";
export function Language() {
  const { locale, setLocale, t } = useI18n();
  const [value, setValue] = useState<Locale>(locale);
  useEffect(() => setValue(locale), [locale]);
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        setLocale(value);
      }}
    >
      <Stack direction="horizontal" align="end" gap="condensed" wrap="wrap">
        <FormControl>
          <FormControl.Label>{t("language")}</FormControl.Label>
          <Select
            size="medium"
            value={value}
            onChange={(e) => setValue(e.target.value as Locale)}
          >
            <Select.Option value="zh-CN">简体中文</Select.Option>
            <Select.Option value="en">English</Select.Option>
          </Select>
        </FormControl>
        <Button type="submit" size="medium">
          {t("languageApply")}
        </Button>
      </Stack>
    </form>
  );
}
