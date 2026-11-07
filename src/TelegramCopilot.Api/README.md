# TelegramCopilot API

.NET 10 Minimal API，无鉴权。默认 `http://127.0.0.1:5080`，OpenAPI 始终提供于 `/openapi/v1.json`。配置 `DATA_PATH`（兼容旧名 `DataPath`）决定数据库、密钥环和 TDLib 会话存放位置。`ASPNETCORE_URLS` 可覆盖监听地址。

## 运行

从仓库根目录：

```sh
dotnet build TelegramCopilot.slnx /bl:artifacts/build-{}.binlog
install -d -m 700 data
DataPath=./data dotnet src/TelegramCopilot.Api/bin/Debug/net10.0/TelegramCopilot.Api.dll
```

Debian 生产环境建议使用根目录的 Dockerfile，完整步骤见 `../../docs/deployment-debian.md`。镜像自带从官方源码固定提交构建的 `libtdjson.so`。不使用 Docker 时，将同版本库放入 `/usr/local/lib`，执行 `ldconfig`，并在启动环境设置 `LD_LIBRARY_PATH=/usr/local/lib`；同时安装 OpenSSL、zlib 和 .NET 10 ASP.NET Core runtime。没有 TDLib 时 API 仍可编辑配置，后台会记录不可观测时段。

## 配置 CRUD

资源名：`accounts`, `targets`, `schedules`, `destinations`, `rules`, `settings`。

| 方法 | 路径 | 参数 / 结果 |
|---|---|---|
| GET | `/api/{kind}` | `search`, `page=1`, `pageSize=100` → items/total/page/pageSize |
| GET | `/api/{kind}/{id}` | 返回单个脱敏配置 |
| POST | `/api/{kind}` | 创建，201 + Location |
| PUT | `/api/{kind}/{id}` | 完整替换，必须提供当前 revision |
| DELETE | `/api/{kind}/{id}?revision=1` | 204；有被引用资源时 409 |

所有配置体共用信封：

```json
{"name":"主账号","enabled":true,"settings":{"apiId":12345,"apiHash":"0123456789abcdef0123456789abcdef","phone":"+886912345678"}}
```

PUT 在信封中另加 `revision`。Secret 字段返回 `***`，更新时原样传回会保留现有值，创建时必须提供真实密钥。未知 settings 字段会返回 400，避免拼写错误静默失效。

### accounts.settings

`apiId` 正整数，`apiHash` 32 位十六进制，`phone` 国际格式。修改手机号会使用新的独立会话目录。禁用停止该账号后续的监控和发送；再次启用可以恢复已保存的会话。正在发送的请求可能继续完成。删除配置不会清理磁盘会话文件或历史日志。TDLib 被设置为在线运行以接收更新，观测账号自身可能显示在线。

### targets.settings

```json
{"accountId":"ACCOUNT_UUID","identifier":"@alice","refreshSeconds":30}
```

`identifier` 也可为 `+886912345678`。`refreshSeconds` 取 1–3600，是核对 TDLib **缓存状态**的间隔，默认 30 秒；在线变更主要来自推送。调小它不会绕过 Telegram 隐私限制，也不承诺每秒进行一次服务器查询。

### schedules.settings

```json
{"accountId":"ACCOUNT_UUID","recipient":"@some_bot","message":"hello","frequency":"Weekly","timeZone":"Asia/Taipei","localTime":"07:59:00","dayOfWeek":1,"dayOfMonth":1,"month":1}
```

`frequency`: Daily/Weekly/Monthly/Yearly。星期日为 0、星期一为 1；日期 1–31、月份 1–12。每个接收人独立建立一条配置，可以使用不同账号、时间和正文。不存在的月末日期、非闰年的 2 月 29 日、DST 空档会跳过；DST 重复时间只运行一次。使用系统 tzdata 的 IANA 时区。

计划在程序运行时按秒检查；调度宽限内尽快发送，过期的待执行点记录为 Skipped。重启不会追发停机期间所有消息。发送中断记录为 Uncertain，不自动重试。Sent 表示 TDLib 的最终发送成功更新，通常包含最终消息编号，不是单纯入队。

### destinations.settings

```json
{"serverUrl":"https://api.day.app","deviceKeys":["DEVICE_KEY"],"encryption":null}
```

多个设备使用多个 key。可选加密：`{"key":"16/24/32 UTF-8 bytes","iv":"16 UTF-8 bytes","mode":"CBC"}`，mode 也支持 ECB。对应 BarkerSharper 的 AES128/192/256 配置。HTTPS 或 HTTP Bark 服务均可；device keys / 加密 key / IV 加密存储并在 API 中脱敏。

### rules.settings

```json
{"destinationId":"DESTINATION_UUID","event":"Offline","targetId":"TARGET_UUID","title":"{NAME} 下线了","body":"在线了 {TIME_OF_ONLINE}","subtitle":"状态更新","group":"{NAME}","parameters":{"level":"timeSensitive","sound":"bell","badge":1,"isArchive":1,"url":"https://example.com"}}
```

`targetId=null` 匹配该事件的全部对象。事件为 Online, Offline, Unknown, MonitorRestored, AccountReady, AccountUnavailable, WorkerStarted, WorkerStopped, MessageSent, MessageFailed, Error。

模板变量：`NAME`, `EVENT`, `TIME_OF_ONLINE`（自适应时长，如 `1s`、`1分钟`、`2小时32分1秒`），`ONLINE_SECONDS`，`AT`，`DETAIL`，`TARGET_ID`，`ACCOUNT_ID`，使用 `{VARIABLE}`。中断或首次观测的完整在线时长无法确定时为 `unknown`，不会把部分时长冒充完整时长。

`parameters` 支持 BarkerSharper ExtendedModel 的 level / badge / autoCopy / copy / sound / call / icon / group / volume / isArchive / url 等参数；还可透传库尚未建模的 Bark 字段，例如 subtitle。rule 顶层 title/body/subtitle/group 优先。设备密钥、密文及 IV 不能通过 parameters 覆盖。参数字符串支持同样的模板变量。BarkerSharper 没有取消参数，超时结果可能为 Uncertain，实际网络请求可能已经送达。

### settings.settings

全局只允许一份系统配置，不创建时使用以下默认值：

```json
{"timeZone":"Asia/Taipei","heartbeatSeconds":1,"staleAfterSeconds":5,"schedulerGraceSeconds":60}
```

heartbeatSeconds 1–10；staleAfterSeconds 至少是心跳的两倍、最多 120；schedulerGraceSeconds 1–3600。禁用或删除此配置会使用默认值。系统时区用于未传 timeZone 的 API 统计查询；前端筛选器和每条计划使用各自显式时区，不重写现有计划。

## 登录、健康和历史

- `GET /health/live`：进程存活。
- `GET /health/ready`：TDLib 和持久化心跳就绪时 200，否则 503 Problem Details。
- `GET /api/health`：Healthy/Degraded、持久化心跳及 TDLib 是否可用。
- `GET /api/accounts/{id}/status`：当前授权状态、连接状态和脱敏错误。
- `POST /api/accounts/{id}/login`：`{"type":"code","value":"12345"}`。type 也可为 password/email/emailCode，挑战值不持久化。
- `GET /api/events?from=...&to=...&targetId=...&type=Offline&page=1&pageSize=100`。
- `GET /api/deliveries?channel=Telegram&resourceId=...&page=1&pageSize=100`：Bark/Telegram 的 Sending/Sent/Failed/Uncertain/Skipped 记录。

Telegram 可能要求设备确认、重新注册或购买 Premium，这些状态会明确返回，当前界面不代办这些账号操作。

## 在线日志与统计

```text
GET /api/targets/{id}/timeline?from=2026-11-07T00:00:00%2B08:00&to=2026-11-08T00:00:00%2B08:00&page=1&pageSize=100&state=Online
GET /api/targets/{id}/statistics?from=...&to=...&period=week&timeZone=Asia%2FTaipei&page=1&pageSize=100
GET /api/targets/{id}/export.csv?from=...&to=...&state=Online
```

`from` 必须早于 `to`，最长 3660 天；page 1–1000000，pageSize 1–500。未来的结束时间裁到当前时刻。state 可省略，或 Online/Offline/Unknown。CSV 导出全部匹配记录，不仅当前页；为避免内存压力，超过约 100 MB 时请缩短时间范围。

统计 period 为 day/week/month/year，周从星期一开始。桶按指定时区划分，可跨 DST。返回 Totals 及分页 buckets，包含上线次数、在线/离线/未知秒数、完整会话平均时长和覆盖率。只统计观测到的上线边界；跨查询边界的持续会话贡献时长，但不会凭空增加次数。平均值只用完整落在范围内且两端可观测的会话。故次数和用于平均值的会话数可能不同。

日志包含 startKnown/endKnown、observedAt、telegramAt，避免把观测时间、Telegram 服务器时间和查询裁剪边界混为一谈。删除监控配置后，仍可用原 ID 查询历史。详细语义见根目录 docs/presence.md。

## 错误

400 参数错误；404 不存在；409 配置版本冲突、引用仍存在或账号当前不能操作；429 Telegram 频率限制；502 Telegram 请求失败；504 超时。响应使用 Problem Details。OpenAPI 和 `TelegramCopilot.http` 包含所有操作。

## Monitoring and manual tests

- `GET /api/monitoring` returns worker health, total persisted poll attempts and enabled target count. GET requests never perform a poll or increment counters.
- `GET /api/targets/{id}/monitoring` includes successful/unsuccessful checks, last/next poll, nickname, username, avatar availability and Telegram-provided last-seen. Privacy-limited statuses retain their original category and do not invent an exact timestamp.
- `GET /api/targets/{id}/avatar` reads the current small Telegram photo through the configured account. Missing photos return 404.
- `POST /api/rules/validate` accepts the ordinary configuration draft, returns field issues and a simulated notification preview, and performs no delivery or save.
- `POST /api/schedules/{id}/test` and `POST /api/rules/{id}/test` accept `{ "requestId": "a-new-UUID" }`. They send the saved configuration immediately and audit every outcome. Reusing the same requestId does not send again. A schedule test does not consume or alter its recurring occurrence. A rule test invokes only that rule and does not publish a synthetic global event. Disabled rules/destinations can be tested manually; automatic delivery still honors enablement.
- Delivery IDs beginning with `test:telegram:` or `test:bark:` identify manual tests. Failed, partial and uncertain results are preserved.

API/CSV numeric duration fields remain seconds for compatibility. The console accepts Chinese/English time units and formats displayed durations automatically.
