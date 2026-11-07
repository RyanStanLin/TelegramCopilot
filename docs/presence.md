# Presence and statistics semantics

TelegramCopilot records what the configured Telegram account can observe. Official TDLib handles networking, update ordering and local Telegram state. The application never bypasses status privacy. Username resolution and discoverable phone lookup are read operations; contacts are not imported.

## Three distinct states

- Online: TDLib returned an unexpired userStatusOnline.
- Offline: TDLib returned userStatusOffline, with Telegram's was_online retained separately.
- Unknown: hidden/recent/week/month/empty status, missing account, connection interruption, expired cached online status, disabled target, error or process outage.

The transition timestamp is when the application observes the update. Telegram's reported timestamp is metadata, not used to move an event backwards through previously recorded history. Initial observations and resumed observations have censored boundaries. An online session that started before observation is not counted as an observed login.

The application processes pushes in received order and periodically inspects the TDLib user cache. getUser is explicitly an offline/cached method for user accounts. RefreshSeconds is not a promise of fresh server presence. Network delay and privacy can prevent exact real-world seconds from being known.

Pushes, cache reconciliation and local availability observations share one queue and one persistence consumer. A later cached state cannot bypass already queued transitions. An unexpired online status also carries its expiry: reads cap known coverage at that expiry, renewals update it, and a delayed follow-up preserves an Unknown gap. Expiry never becomes an invented offline event.

Intervals continue the latest observable state while recording is available; they do not prove that every real-world change between observations was delivered. Coverage measures the recorded known-state share, not an independent server verification every second. Counts are observed starts and may undercount unavailable or hidden transitions.

## Crash and downtime

Each worker persists a heartbeat. Queries cap an open known interval at the last durable heartbeat, even if the process has crashed and has not restarted. At startup, open intervals are closed at that checkpoint. Missing coverage is filled with Unknown intervals. Restart never fills an outage with the last known status. Graceful shutdown appends an unknown transition immediately.

Example: observed online at 10:00:10, last persisted heartbeat at 10:02:00, restart at 10:05:00. A query returns observed online through 10:02:00, an unknown interval from 10:02:00 through the next observation, and then the newly observed state. It cannot say when the person actually went offline inside that gap.

Known coverage is capped conservatively at the committed checkpoint; the most recent fraction of a second may temporarily appear unknown until the next heartbeat. The service requires one active API worker per data directory. Do not scale API replicas against the same SQLite/TDLib files.

Recovery distinguishes an expired online status before the last checkpoint from process downtime after it. Database schema version 2 adds the persisted expiry column and upgrades version 1 without deleting prior intervals. Deleting a target caps its retained interval at the deletion time; queries after that time return unknown coverage.

## Time ranges

Store UTC milliseconds, retaining second-level Telegram metadata. Public dates use DateTimeOffset / ISO 8601. All intervals and queries are half-open `[start,end)`. The requested range is clipped to now. Missing intervals, including dates before monitoring began, remain unknown. Configuration changes do not erase historical events.

Counts include only known online starts inside the range. Durations include clipped overlap. Average session duration includes complete observed online sessions entirely contained by the range; incomplete sessions are excluded. Gap duration is supplied only when both relevant boundaries are visible in the queried window and no unknown interval lies between them. No gap is invented across an outage or across a missing preceding session.

Calendar day/week/month/year buckets use the requested IANA timezone; weeks start Monday. Daily duration may differ from 86400 seconds on DST days. Coverage ratio is `(online + offline) / requested elapsed seconds`, excluding unknown. The contribution view encodes observed login counts; zero with unknown coverage is not evidence that the user stayed offline.

## External delivery

Schedule occurrence keys and notification delivery keys are durably claimed before sending. Normal retries/restarts cannot claim the same occurrence twice. SQLite and Telegram/Bark do not share a transaction; an interrupted request can have reached the external service even when its acknowledgement was not persisted. Such attempts are Uncertain and are not automatically replayed. Review the destination before manually creating a replacement plan.

The scheduler processes wall-clock calendar occurrences, skips invalid dates and DST gaps, and uses the earlier UTC occurrence of a repeated local time. Updates are live. Paused/disabled configurations retain history. Missed schedules during process downtime are not backfilled as if delivered on time.
