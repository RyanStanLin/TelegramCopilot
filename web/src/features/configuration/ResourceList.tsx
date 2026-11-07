"use client";
import {
  Button,
  FormControl,
  PageHeader,
  Stack,
  TextInput,
} from "@primer/react";
import { SearchIcon, PlusIcon } from "@primer/octicons-react";
import NextLink from "next/link";
import { useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Confirm } from "@/components/Confirm";
import { useI18n } from "@/i18n/provider";
import { api, query } from "@/lib/api";
import { useLoad } from "@/lib/use-load";
import type { Kind, Resource, Page } from "@/lib/types";
import { Failure, Loading, Pager } from "@/components/Status";
import { ResourceTable } from "./ResourceTable";
import { SystemInfo } from "./SystemInfo";
export function ResourceList({ kind }: { kind: Kind }) {
  const { t } = useI18n();
  const router = useRouter();
  const params = useSearchParams();
  const page = Math.max(1, Number(params.get("page")) || 1);
  const search = params.get("search") ?? "";
  const [text, setText] = useState(search);
  const [revision, refresh] = useState(0);
  const [remove, setRemove] = useState<Resource>();
  const [error, setError] = useState(false);
  const [busy, setBusy] = useState(false);
  const searchInput = useRef<HTMLInputElement>(null);
  const data = useLoad<Page<Resource>>(
    `/${kind}?${query({ page, pageSize: 25, search })}`,
    revision,
  );
  return (
    <Stack gap="normal">
      <PageHeader>
        <PageHeader.TitleArea>
          <PageHeader.Title as="h1">{t(kind)}</PageHeader.Title>
        </PageHeader.TitleArea>
        <PageHeader.Actions>
          {(kind !== "settings" || !data.data?.total) && (
            <Button
              as={NextLink}
              href={`/${kind}/new`}
              size="medium"
              variant="primary"
              leadingVisual={PlusIcon}
            >
              {t("new")}
            </Button>
          )}
        </PageHeader.Actions>
      </PageHeader>
      <form
        role="search"
        onSubmit={(e) => {
          e.preventDefault();
          router.push(`/${kind}?${query({ search: text, page: 1 })}`);
        }}
      >
        <Stack direction="horizontal" align="end" gap="condensed" wrap="wrap">
          <FormControl>
            <FormControl.Label>{t("search")}</FormControl.Label>
            <TextInput
              ref={searchInput}
              size="medium"
              leadingVisual={SearchIcon}
              value={text}
              onChange={(e) => setText(e.target.value)}
            />
          </FormControl>
          <Button size="medium" type="submit">
            {t("apply")}
          </Button>
        </Stack>
      </form>
      {data.loading ? (
        <Loading />
      ) : data.error ? (
        <Failure retry={() => refresh((v) => v + 1)} />
      ) : (
        data.data && (
          <>
            <ResourceTable
              kind={kind}
              rows={data.data.items}
              onDelete={(resource) => {
                setRemove(resource);
                setError(false);
              }}
            />
            <Pager
              page={page}
              total={data.data.total}
              size={25}
              change={(p) =>
                router.push(`/${kind}?${query({ search, page: p })}`)
              }
            />
          </>
        )
      )}
      {kind === "settings" && <SystemInfo />}
      {remove && (
        <Confirm
          title={`${t("delete")} ${remove.name}`}
          confirmButtonContent={t("confirmDelete")}
          cancelButtonContent={t("cancel")}
          confirmButtonType="danger"
          confirmButtonLoading={busy}
          onClose={async (gesture) => {
            if (busy) return;
            if (gesture !== "confirm") {
              setRemove(undefined);
              return;
            }
            setBusy(true);
            try {
              await api(`/${kind}/${remove.id}?revision=${remove.revision}`, {
                method: "DELETE",
              });
              setRemove(undefined);
              refresh((x) => x + 1);
              requestAnimationFrame(() => searchInput.current?.focus());
            } catch {
              setError(true);
            } finally {
              setBusy(false);
            }
          }}
        >
          {t("removeQuestion")}
          {error && <Failure message="saveFailed" />}
        </Confirm>
      )}
    </Stack>
  );
}
