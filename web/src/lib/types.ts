export type Kind =
  "accounts" | "targets" | "schedules" | "destinations" | "rules" | "settings";
export type Resource = {
  id: string;
  kind: string;
  name: string;
  enabled: boolean;
  settings: Record<string, unknown>;
  revision: number;
  createdAt: string;
  updatedAt: string;
};
export type Page<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};
export type Row = { id: string; [key: string]: unknown };
export type Interval = {
  id: number;
  targetId: string;
  state: string;
  start: string;
  end: string;
  seconds: number;
  reason: string;
  observedAt: string;
  telegramAt?: string;
  startKnown: boolean;
  endKnown: boolean;
  gapSincePreviousOnlineSeconds?: number;
};
export type Bucket = {
  from: string;
  to: string;
  count: number;
  onlineSeconds: number;
  offlineSeconds: number;
  unknownSeconds: number;
  averageSessionSeconds?: number;
  coverageRatio: number;
};
export type Statistics = {
  targetId: string;
  period: string;
  timeZone: string;
  totals: Bucket;
  buckets: Page<Bucket>;
};
export const kinds: Kind[] = [
  "targets",
  "accounts",
  "schedules",
  "destinations",
  "rules",
  "settings",
];
