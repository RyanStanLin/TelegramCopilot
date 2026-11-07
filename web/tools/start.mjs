import { cpSync, existsSync } from "node:fs";
import { resolve, join } from "node:path";
import { spawn } from "node:child_process";
import nextEnv from "@next/env";
nextEnv.loadEnvConfig(process.cwd());
const standalone = resolve(".next/standalone");
const directory = existsSync(join(standalone, "server.js"))
  ? standalone
  : join(standalone, "web");
cpSync(resolve(".next/static"), join(directory, ".next/static"), {
  recursive: true,
});
if (existsSync("public"))
  cpSync(resolve("public"), join(directory, "public"), { recursive: true });
const child = spawn(process.execPath, [join(directory, "server.js")], {
  stdio: "inherit",
  env: { ...process.env, HOSTNAME: process.env.HOSTNAME ?? "127.0.0.1" },
});
for (const signal of ["SIGINT", "SIGTERM"])
  process.on(signal, () => child.kill(signal));
child.on("exit", (code) => process.exit(code ?? 1));
