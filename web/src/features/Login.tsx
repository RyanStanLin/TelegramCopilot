"use client";
import { Banner, Button, Heading, PageLayout, Stack } from "@primer/react";
import {
  startAuthentication,
  startRegistration,
} from "@simplewebauthn/browser";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useI18n } from "@/i18n/provider";
import { Language } from "@/components/Language";
import { Failure, Loading } from "@/components/Status";
export function Login() {
  const { t } = useI18n();
  const router = useRouter();
  const [registered, setRegistered] = useState<boolean>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [revision, refresh] = useState(0);
  useEffect(() => {
    setError(false);
    fetch("/api/auth/status", { cache: "no-store" })
      .then((r) => {
        if (!r.ok) throw new Error();
        return r.json();
      })
      .then((v) => {
        if (v.authenticated) router.replace("/targets");
        setRegistered(v.registered);
      })
      .catch(() => setError(true));
  }, [router, revision]);
  async function login() {
    setBusy(true);
    setError(false);
    try {
      if (!window.PublicKeyCredential) throw new Error();
      const response = await fetch("/api/auth/options", { method: "POST" });
      if (!response.ok) throw new Error();
      const { mode, options } = await response.json();
      const credential =
        mode === "register"
          ? await startRegistration({ optionsJSON: options })
          : await startAuthentication({ optionsJSON: options });
      const result = await fetch("/api/auth/verify", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(credential),
      });
      if (!result.ok) throw new Error();
      router.replace("/targets");
      router.refresh();
    } catch {
      setError(true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <PageLayout containerWidth="medium">
      <PageLayout.Content width="medium">
        <Stack gap="spacious" padding="spacious">
          <Heading as="h1">TelegramCopilot</Heading>
          <Language />
          {error && registered === undefined && (
            <Failure
              message="loginError"
              focus
              retry={() => refresh((v) => v + 1)}
            />
          )}
          {registered === undefined ? (
            !error && <Loading />
          ) : (
            <>
              {!registered && (
                <Banner title={t("registerInfo")} variant="info" />
              )}
              {error && <Failure message="loginError" focus />}
              <Button
                size="medium"
                variant="primary"
                loading={busy}
                loadingAnnouncement={t("loading")}
                onClick={login}
              >
                {t(registered ? "login" : "register")}
              </Button>
            </>
          )}
        </Stack>
      </PageLayout.Content>
    </PageLayout>
  );
}
