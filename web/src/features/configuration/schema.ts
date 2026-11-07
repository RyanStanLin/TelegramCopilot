import type { Kind } from "@/lib/types";
export type Field = {
  key: string;
  label?: string;
  type?: "number" | "password" | "textarea" | "json" | "list" | "boolean";
  required?: boolean;
  initial?: unknown;
  options?: string[];
  reference?: Kind;
  hint?: string;
  when?: (v: Record<string, unknown>) => boolean;
};
const account: Field = {
  key: "accountId",
  reference: "accounts",
  required: true,
};
const zone: Field = { key: "timeZone", initial: "Asia/Taipei", required: true };
const frequency = (values: string[]) => (v: Record<string, unknown>) =>
  values.includes(String(v.frequency));
export const schema: Record<Kind, Field[]> = {
  accounts: [
    { key: "apiId", type: "number", required: true },
    { key: "apiHash", type: "password", required: true, hint: "secretHint" },
    { key: "phone", required: true },
  ],
  targets: [
    account,
    { key: "identifier", required: true },
    { key: "refreshSeconds", type: "number", initial: 30, required: true },
  ],
  schedules: [
    account,
    { key: "recipient", required: true },
    { key: "message", type: "textarea", required: true },
    {
      key: "frequency",
      options: ["Daily", "Weekly", "Monthly", "Yearly"],
      initial: "Daily",
    },
    zone,
    { key: "localTime", initial: "07:59:00", required: true, hint: "timeHint" },
    {
      key: "dayOfWeek",
      options: ["0", "1", "2", "3", "4", "5", "6"],
      initial: 1,
      type: "number",
      when: frequency(["Weekly"]),
    },
    {
      key: "dayOfMonth",
      type: "number",
      initial: 1,
      when: frequency(["Monthly", "Yearly"]),
    },
    {
      key: "month",
      label: "monthNumber",
      type: "number",
      initial: 1,
      when: frequency(["Yearly"]),
    },
  ],
  destinations: [
    { key: "serverUrl", initial: "https://api.day.app", required: true },
    { key: "deviceKeys", type: "list", required: true, hint: "deviceKeysHint" },
    { key: "encryptionEnabled", type: "boolean", initial: false },
    {
      key: "encryptionKey",
      type: "password",
      hint: "encryptionHint",
      when: (v) => !!v.encryptionEnabled,
    },
    {
      key: "encryptionIV",
      type: "password",
      when: (v) => !!v.encryptionEnabled,
    },
    {
      key: "encryptionMode",
      options: ["CBC", "ECB"],
      initial: "CBC",
      when: (v) => !!v.encryptionEnabled,
    },
  ],
  rules: [
    { key: "destinationId", reference: "destinations", required: true },
    {
      key: "event",
      options: [
        "Online",
        "Offline",
        "Unknown",
        "MonitorRestored",
        "AccountReady",
        "AccountUnavailable",
        "WorkerStarted",
        "WorkerStopped",
        "MessageSent",
        "MessageFailed",
        "Error",
      ],
      initial: "Online",
    },
    { key: "targetId", reference: "targets" },
    { key: "title", initial: "{NAME}", required: true },
    {
      key: "body",
      type: "textarea",
      initial: "{EVENT} {TIME_OF_ONLINE}",
      required: true,
      hint: "templateHelp",
    },
    { key: "subtitle" },
    { key: "group" },
    { key: "parameters", type: "json", initial: "{}", label: "advanced" },
  ],
  settings: [
    zone,
    { key: "heartbeatSeconds", type: "number", initial: 1, required: true },
    { key: "staleAfterSeconds", type: "number", initial: 5, required: true },
    {
      key: "schedulerGraceSeconds",
      type: "number",
      initial: 60,
      required: true,
    },
  ],
};
