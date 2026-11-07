"use client";
import {
  Button,
  FormControl,
  Heading,
  Select,
  Stack,
  TextInput,
} from "@primer/react";
import { useState } from "react";
import { useI18n } from "@/i18n/provider";
import { api } from "@/lib/api";
import { useLoad } from "@/lib/use-load";
import { Failure, Loading } from "@/components/Status";
export function AccountLogin({ id }: { id: string }) {
  const { t } = useI18n();
  const [revision, refresh] = useState(0);
  const status = useLoad<{
    authorizationState: string;
    connectionState: string;
  }>(`/accounts/${id}/status`, revision);
  const [type, setType] = useState("code");
  const [value, setValue] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [sent, setSent] = useState(false);
  return (
    <section>
      <Stack gap="normal">
        <Heading as="h2" variant="small">
          {t("accountStatus")}
        </Heading>
        {status.loading ? (
          <Loading />
        ) : status.error ? (
          <Failure retry={() => refresh((x) => x + 1)} />
        ) : (
          <p>
            {t(status.data?.authorizationState ?? "Unknown")} ·{" "}
            {t(status.data?.connectionState ?? "Unknown")}
          </p>
        )}
        <Button size="medium" onClick={() => refresh((x) => x + 1)}>
          {t("statusRefresh")}
        </Button>
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            if (busy) return;
            setBusy(true);
            setError(false);
            try {
              await api(`/accounts/${id}/login`, {
                method: "POST",
                body: JSON.stringify({ type, value }),
              });
              setValue("");
              setSent(true);
              refresh((x) => x + 1);
            } catch {
              setError(true);
            } finally {
              setBusy(false);
            }
          }}
        >
          <Stack gap="normal">
            <FormControl>
              <FormControl.Label>{t("loginType")}</FormControl.Label>
              <Select
                size="medium"
                value={type}
                onChange={(e) => setType(e.target.value)}
              >
                {["code", "password", "email", "emailCode"].map((v) => (
                  <Select.Option key={v} value={v}>
                    {t(v)}
                  </Select.Option>
                ))}
              </Select>
            </FormControl>
            <FormControl>
              <FormControl.Label>{t("loginValue")}</FormControl.Label>
              <TextInput
                size="medium"
                type="password"
                value={value}
                onChange={(e) => setValue(e.target.value)}
                autoComplete="off"
              />
            </FormControl>
            {error && <Failure message="saveFailed" focus />}
            <span role="status">{sent && t("loginSubmitted")}</span>
            <Button
              size="medium"
              type="submit"
              loading={busy}
              loadingAnnouncement={t("loading")}
            >
              {t("send")}
            </Button>
          </Stack>
        </form>
      </Stack>
    </section>
  );
}
