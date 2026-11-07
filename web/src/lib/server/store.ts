import "server-only";
import { DatabaseSync } from "node:sqlite";
import { mkdirSync } from "node:fs";
import { resolve, join } from "node:path";
import { randomBytes } from "node:crypto";
import type { WebAuthnCredential } from "@simplewebauthn/server";

const globals = globalThis as typeof globalThis & { copilotDb?: DatabaseSync };
export function database() {
  if (globals.copilotDb) return globals.copilotDb;
  const directory = resolve(process.env.WEB_DATA_PATH ?? "./data");
  mkdirSync(directory, { recursive: true, mode: 0o700 });
  const db = new DatabaseSync(join(directory, "web.db"));
  db.exec(`PRAGMA journal_mode=WAL; PRAGMA busy_timeout=15000;
    CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS passkey (singleton INTEGER PRIMARY KEY CHECK(singleton=1), body TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS challenges (id TEXT PRIMARY KEY, mode TEXT NOT NULL, challenge TEXT NOT NULL, expires INTEGER NOT NULL);`);
  globals.copilotDb = db;
  return db;
}
export function secret(key: string) {
  const db = database();
  db.prepare("INSERT OR IGNORE INTO meta VALUES (?,?)").run(
    key,
    randomBytes(32).toString("base64url"),
  );
  return (
    db.prepare("SELECT value FROM meta WHERE key=?").get(key) as {
      value: string;
    }
  ).value;
}
export type StoredCredential = Omit<WebAuthnCredential, "publicKey"> & {
  publicKey: string;
  registeredAt: string;
};
export function credential(): StoredCredential | null {
  const row = database()
    .prepare("SELECT body FROM passkey WHERE singleton=1")
    .get() as { body: string } | undefined;
  return row ? JSON.parse(row.body) : null;
}
export function saveCredential(value: StoredCredential) {
  database()
    .prepare("INSERT INTO passkey(singleton,body) VALUES(1,?)")
    .run(JSON.stringify(value));
}
export function updateCounter(
  id: string,
  oldCounter: number,
  newCounter: number,
) {
  const current = credential();
  if (!current || current.id !== id || current.counter !== oldCounter)
    throw new Error("AuthenticationConflict");
  const changed = database()
    .prepare("UPDATE passkey SET body=? WHERE singleton=1 AND body=?")
    .run(
      JSON.stringify({ ...current, counter: newCounter }),
      JSON.stringify(current),
    );
  if (changed.changes !== 1) throw new Error("AuthenticationConflict");
}
export function consumeChallenge(id: string) {
  return database()
    .prepare("DELETE FROM challenges WHERE id=? RETURNING *")
    .get(id) as
    | {
        id: string;
        mode: "register" | "authenticate";
        challenge: string;
        expires: number;
      }
    | undefined;
}
