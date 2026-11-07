"use client";
import { Table } from "@primer/react/experimental";
import { useId, type ReactNode } from "react";
import { useI18n } from "@/i18n/provider";
export type Column<T> = {
  key: string;
  label: string;
  render: (row: T) => ReactNode;
};
export function RecordsTable<T>({
  title,
  rows,
  columns,
  rowId,
}: {
  title: string;
  rows: T[];
  columns: Column<T>[];
  rowId: (row: T) => string;
}) {
  const id = useId();
  const { t } = useI18n();
  if (!rows.length) return <p role="status">{t("empty")}</p>;
  return (
    <Table.Container>
      <Table.Title as="h2" id={id}>
        {title}
      </Table.Title>
      <Table
        aria-labelledby={id}
        cellPadding="normal"
        gridTemplateColumns={`repeat(${columns.length}, minmax(max-content, 1fr))`}
      >
        <Table.Head>
          <Table.Row>
            {columns.map((c) => (
              <Table.Header key={c.key}>{t(c.label)}</Table.Header>
            ))}
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {rows.map((row) => (
            <Table.Row key={rowId(row)}>
              {columns.map((c, i) => (
                <Table.Cell key={c.key} scope={i === 0 ? "row" : undefined}>
                  {c.render(row)}
                </Table.Cell>
              ))}
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
    </Table.Container>
  );
}
