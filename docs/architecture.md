# TelegramCopilot architecture and acceptance contract

2026-11-07. New repository. Debian deployment; .NET 10 backend and Next.js frontend.

## Boundaries

- Core: domain aggregates, presence state transitions, calendar recurrence, validation. No Telegram, HTTP, or database dependency.
- Application: use cases, DTOs, repository and gateway ports; API invokes these use cases and CLI consumes the corresponding HTTP contract.
- Infrastructure: SQLite WAL, encrypted account/Bark configuration, official TDLib JSON C interface, BarkerSharper, background workers.
- API: ASP.NET Core minimal APIs, unauthenticated as requested. Bind privately; OpenAPI and Problem Details.
- CLI: configuration CRUD, login challenges, telemetry/stats/CSV, health, scheduler run history through the same HTTP contract. Daemon ownership remains with API.
- Web: Next.js server gateway enforces the single passkey. API remains separate and unauthenticated; Compose publishes its port on the host loopback interface. Only the web interface should be exposed through the public HTTPS reverse proxy.

## Presence semantics

UTC timestamps retain seconds. Store observed time separately from Telegram's reported last-online/expiry fields. Push updates are primary; bounded reconciliation refreshes cached user info. Privacy-limited status is unknown, never offline. Seconds are timestamp resolution, not an accuracy guarantee. There is no reconstruction of private or missed presence.

Durable intervals use half-open [start,end). Online, offline and unknown are distinct. Target creation, disabled monitoring, unavailable account, transport outage, restart and stale heartbeat create unknown coverage. Worker heartbeats and last trustworthy checkpoint cap known intervals after a crash. Queries clip at request boundaries and return unknown gaps; statistics count only observed online duration and report coverage separately. Event time earlier than a known boundary cannot rewrite history. Repeated updates do not create extra sessions.

## Scheduling and delivery

Daily/weekly/monthly/yearly wall-clock recurrence in an IANA timezone. Invalid calendar dates are skipped. DST gaps skipped; repeated local clock time runs once. Per-schedule durable occurrence keys prevent normal duplicate dispatch. External Telegram/Bark delivery cannot be atomically committed with SQLite: an interrupted in-flight send becomes uncertain and is not automatically replayed. Telegram enqueue and final delivery acknowledgement are separate events. Never claim exactly-once external delivery.

## Durability and operations

One active API worker per data directory, protected by a process lock. SQLite busy timeout, foreign keys, WAL and transactional state changes. Secrets encrypted using a persistent ASP.NET Data Protection key ring. Preserve database, key ring and TDLib session directories together. Runtime configuration changes apply without restart. Revision checks prevent lost updates. Logical deletion retains telemetry and delivery history.

## Acceptance areas

1. Multi-account login code, password and email challenges; status and account disable.
2. Target identifier resolution by public username or discoverable phone; transitions, disconnect/restart gaps, bounded filtering and pagination.
3. Calendar aggregation day/week/month/year, totals/count/average/unknown time, contribution data, CSV formula escaping.
4. Config CRUD and reference checks; optimistic concurrency; persisted secrets.
5. Per-recipient schedules, durable send ledger and failure history.
6. BarkerSharper destination parameters/encryption and event templates including TIME_OF_ONLINE.
7. One global passkey, atomic first registration, expiring one-use challenges, verified origin/RP, signed server session.
8. Primer research gates before frontend layout/code, bilingual UI, complete config flows and statistics.
9. Debian Docker, explicit native dependency, independent READMEs and incremental Git commits.

## Live verification limits

Real Telegram and Bark operations require user credentials and devices. Automated tests must use gateway doubles and must not send to real recipients. Native TDLib and browser WebAuthn need separate integration checks.
