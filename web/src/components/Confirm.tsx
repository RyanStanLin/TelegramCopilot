"use client";
import { Button, Dialog, Stack, type DialogHeaderProps } from "@primer/react";
import type { ReactNode } from "react";
import { useI18n } from "@/i18n/provider";

function ConfirmHeader({ dialogLabelId, title, onClose }: DialogHeaderProps) {
  const { t } = useI18n();
  return (
    <Dialog.Header>
      <Stack
        direction="horizontal"
        align="start"
        justify="space-between"
        gap="normal"
      >
        <Dialog.Title id={dialogLabelId}>{title}</Dialog.Title>
        <Button size="medium" onClick={() => onClose("close-button")}>
          {t("close")}
        </Button>
      </Stack>
    </Dialog.Header>
  );
}

export function Confirm({
  title,
  confirmButtonContent,
  cancelButtonContent,
  confirmButtonType = "normal",
  confirmButtonLoading = false,
  onClose,
  children,
}: {
  title: ReactNode;
  confirmButtonContent: ReactNode;
  cancelButtonContent: ReactNode;
  confirmButtonType?: "normal" | "primary" | "danger";
  confirmButtonLoading?: boolean;
  onClose: (gesture: "confirm" | "cancel" | "close-button" | "escape") => void;
  children?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <Dialog
      title={title}
      role="alertdialog"
      width="medium"
      renderHeader={ConfirmHeader}
      onClose={onClose}
      footerButtons={[
        {
          content: cancelButtonContent,
          size: "medium",
          onClick: () => onClose("cancel"),
          autoFocus: true,
        },
        {
          content: confirmButtonContent,
          size: "medium",
          buttonType: confirmButtonType,
          onClick: () => onClose("confirm"),
          loading: confirmButtonLoading,
          loadingAnnouncement: t("loading"),
        },
      ]}
    >
      {children}
    </Dialog>
  );
}
