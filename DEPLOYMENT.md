# Production deployment

- Backend repository: https://github.com/RyanStanLin/TelegramCopilot
- Frontend repository: https://github.com/RyanStanLin/TelegramCopilot-Web (`web` submodule)
- Deployment branch in each repository: `main`
- Console: https://telegramcopilot.ryanl.in
- API: https://telegramcopilot-api.ryanl.in
- Approved platform workflow: `472a450fa4a7f8caca1b8b7c2b451eb7758dc442`

## Services and updates

| Service | Type and port | Readiness | Memory | Persistent files |
|---|---|---|---|---|
| monitor | worker, internal HTTP 5080 | Process plus downstream readiness | 256 MiB | backend-data at /data |
| api | web, nginx 8080 | /health/ready, proxied to monitor | 32 MiB | none |
| frontend web | independent repository, Node 3000 | /api/auth/status | 192 MiB | web-data at /data |

The API and its background monitoring run together in the worker, keeping one process responsible for TDLib sessions, heartbeat, notifications and dispatch. The platform stops the previous worker before starting its replacement. A stateless API gateway supplies the public web entry and resolves the worker's current address through container DNS. This preserves the application process lock; it never starts two session writers against the same volume. Updates briefly interrupt monitoring, and the persisted timeline exposes the gap as Unknown. The gateway health requires native TDLib and a recent worker heartbeat, rather than accepting an arbitrary 200 page.

Steady declared memory is 480 MiB. Updating the frontend peaks at 672 MiB; the backend update peaks at 512 MiB because worker replacement stops the old worker first. Admission still checks all other platform applications and host headroom. These are configured limits, not a multi-account load-test guarantee.

GitHub is the source of code and declarations. Push to main runs the original .NET tests; an independent route-cache invalidation job and successful tests gate the pinned reusable release. It builds linux/amd64 on GitHub, publishes a repository-ID-specific GHCR digest, and deploys with GitHub OIDC. PRs validate only. No VPS compilation, SSH credentials or global platform secrets are used by CI.

```sh
git clone --recurse-submodules https://github.com/RyanStanLin/TelegramCopilot.git
cd TelegramCopilot
# Make backend changes, then:
git add <changed-files>
git commit -m 'Describe the change'
git push origin main
```

For frontend updates, run `git -C web switch main` before editing, then commit/push inside web to its main branch. Record its new submodule pointer in the backend when desired; each push deploys the corresponding repository. See [frontend deployment](web/DEPLOYMENT.md).

## Native runtime

Root Dockerfile retains the complete source build of pinned official TDLib. The separate **Build pinned TDLib runtime** workflow publishes only its shared library and license for linux/amd64. The production deploy/Dockerfile consumes its reviewed immutable digest from paas.json, keeping normal C# updates small and repeatable.

To upgrade TDLib, explicitly change the source pin, run that workflow, review the source and resulting digest, update TDLIB_IMAGE in paas.json and run compatibility checks before publishing. Never replace the digest with latest. Root Compose remains a standalone deployment alternative.

## Variables, volumes and credentials

monitor uses ASPNETCORE_URLS, DATA_PATH and TZ. Its Docker runtime sets bounded GC defaults. The frontend variables are documented in web/DEPLOYMENT.md. This initial deployment requires no external secretRefs or PAAS_SECRETS; Telegram and Bark credentials are entered later through the console or API and stored encrypted by the application. Do not put them into GitHub source or the manifest.

`PUBLICAPIORIGIN` maps to the .NET configuration key `PublicApiOrigin`, the public API origin used to build a monitored target's avatar URL for Bark. It is declared on monitor in `paas.json`; it must remain reachable by the receiving device. Target events use the matching Telegram profile avatar and remove unrelated custom icon overrides. Check the profile/avatar endpoint and monitoring counters after an update; a real Bark receipt is separate from an outgoing payload test.

Both persistent volumes use UID 10001. backend-data contains SQLite/WAL, the Data Protection key ring and TDLib session files. web-data contains the unique Passkey public credential and session-signing state. Preserve each complete volume and its logical name across releases. Do not delete or rename volumes during updates. No old production data is being migrated into this new instance.

The independent API is unauthenticated as requested. The console requires its global Passkey and protects its own browser proxy. Direct API clients use the API domain. The domains remain separate and no browser CORS bypass is required.

## Operations, backups and rollback

Actions → **PaaS operations** → Run workflow provides status, routes, history, logs and fixed maintenance operations. Select monitor for backend logs and api for gateway logs. Use the frontend repository's workflow for its web service. A code rollback selects a recorded release-id and does not undo data writes or schema changes.

Use backup/backups/download-backup for encrypted backups. File-volume backups may briefly stop the writer; monitor then records unavailable coverage. Preserve off-host encrypted backups and test recovery in a separate approved environment. Data restore is a distinct operation and requires RESTORE plus the numeric repository ID; never use delete/restore to test production deployment.

The application performs versioned compatible SQLite upgrades at startup under its single-process lock. Readiness failures must be diagnosed before switching traffic. Check current commit, digest, native library loading, volume permissions, memory events and application logs. For OIDC/branch rejection, inspect the numeric identities and pinned workflow; do not weaken trust or rerun stale deployments. Keep the previous successful release when a candidate fails.

## Release evidence

Initial production release verified on 2026-11-08:

- Backend commit: `a86c1485d893481297c4dd0fabc515788e342082`.
- Successful complete run: https://github.com/RyanStanLin/TelegramCopilot/actions/runs/42390696672
- Release ID: `4d1fcb3fcb5eea0bd17749a58d71dd7a81f7e09c5f490f261726a3e3a8b31a47`.
- Native artifact run: https://github.com/RyanStanLin/TelegramCopilot/actions/runs/42388812498
- HTTPS readiness, native availability, OpenAPI, Problem Details, frontend SSR/CSS and unauthenticated proxy rejection passed. The owner registered their own Passkey and entered the console.

The temporary disabled persistence fixture was logically deleted. Further follow-up push and cross-update persistence acceptance were skipped at the owner's request. This documentation commit still triggers the configured automatic pipeline; use the latest successful main run and PaaS history for current online identity.

Read-only checks are available with `python3 tools/check-production.py`. Optional proof flags write only an explicitly disabled fixture and are for a separately approved update-acceptance window.

Initial api image: `ghcr.io/ryanstanlin/paas-1449502662-api@sha256:cb1409e8a72cd064fcd6b82dd81a2672e1276ce1cbcafd32546ca9ed5cebea41`.

Initial monitor image: `ghcr.io/ryanstanlin/paas-1449502662-monitor@sha256:ba9dda99fd88ad5cb6577454acf3f647be736e856cbe52eb97a8c1d91afa1946`.
