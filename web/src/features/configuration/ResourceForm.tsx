"use client";
import {
  Button,
  Checkbox,
  FormControl,
  PageHeader,
  Stack,
  TextInput,
} from "@primer/react";
import { useState } from "react";
import { Confirm } from "@/components/Confirm";
import { useI18n } from "@/i18n/provider";
import type { Kind } from "@/lib/types";
import { Failure, Loading } from "@/components/Status";
import { Fields } from "./Fields";
import { AccountLogin } from "./AccountLogin";
import { useResourceEditor } from "./use-resource-editor";
export function ResourceForm({ kind, id }: { kind: Kind; id: string }) {
  const { t } = useI18n();
  const [cancel, setCancel] = useState(false);
  const {
    router,
    form,
    existing,
    name,
    setName,
    enabled,
    setEnabled,
    values,
    setValues,
    references,
    dirty,
    setDirty,
    busy,
    error,
    invalid,
    save,
  } = useResourceEditor(kind, id);
  if (existing.loading) return <Loading />;
  if (existing.error) return <Failure />;
  return (
    <Stack gap="spacious">
      <PageHeader>
        <PageHeader.TitleArea>
          <PageHeader.Title as="h1">
            {t(id === "new" ? "new" : "edit")} · {t(kind)}
          </PageHeader.Title>
        </PageHeader.TitleArea>
      </PageHeader>
      <form
        ref={form}
        noValidate
        onSubmit={save}
        onChange={() => setDirty(true)}
      >
        <Stack gap="normal">
          {error && <Failure message="saveFailed" focus />}
          <FormControl required>
            <FormControl.Label>{t("name")}</FormControl.Label>
            <TextInput
              size="medium"
              block
              value={name}
              onChange={(e) => setName(e.target.value)}
              validationStatus={invalid.includes("name") ? "error" : undefined}
            />
            {invalid.includes("name") && (
              <FormControl.Validation variant="error">
                {t("required")}
              </FormControl.Validation>
            )}
          </FormControl>
          <FormControl>
            <Checkbox
              checked={enabled}
              onChange={(e) => setEnabled(e.target.checked)}
            />
            <FormControl.Label>{t("enabled")}</FormControl.Label>
          </FormControl>
          <Fields
            kind={kind}
            values={values}
            references={references}
            invalid={invalid}
            change={(key, value) => {
              setValues((v) => ({ ...v, [key]: value }));
              setDirty(true);
            }}
          />
          <Stack direction="horizontal" gap="condensed">
            <Button
              size="medium"
              type="submit"
              variant="primary"
              loading={busy}
              loadingAnnouncement={t("loading")}
            >
              {t("save")}
            </Button>
            <Button
              size="medium"
              onClick={() =>
                dirty ? setCancel(true) : router.push(`/${kind}`)
              }
            >
              {t("cancel")}
            </Button>
          </Stack>
        </Stack>
      </form>
      {kind === "accounts" && id !== "new" && <AccountLogin id={id} />}
      {cancel && (
        <Confirm
          title={t("unsaved")}
          confirmButtonContent={t("cancel")}
          cancelButtonContent={t("back")}
          onClose={(g) => {
            setCancel(false);
            if (g === "confirm") {
              setDirty(false);
              router.push(`/${kind}`);
            }
          }}
        />
      )}
    </Stack>
  );
}
