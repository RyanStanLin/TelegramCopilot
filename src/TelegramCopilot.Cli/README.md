# TelegramCopilot CLI

CLI 连接正在运行的无鉴权 API；不会单独启动监控守护进程。所有配置走与前端相同的版本校验和验证逻辑。

## 构建与启动

```sh
dotnet publish src/TelegramCopilot.Cli -c Release -o artifacts/cli /bl:artifacts/cli-{}.binlog
dotnet artifacts/cli/telegramcopilot.dll help
```

`TELEGRAMCOPILOT_API_URL` 或 `--url` 指定地址，默认 `http://127.0.0.1:5080`。远程操作先通过 SSH 转发 API 端口。不要把无鉴权端口暴露到公网。

Docker 镜像也包含 CLI：

```sh
docker compose exec api dotnet cli/telegramcopilot.dll health
docker compose exec api dotnet cli/telegramcopilot.dll list targets
```

## 配置 CRUD

资源名是 accounts / targets / schedules / destinations / rules / settings。所有资源使用相同命令：

```sh
telegramcopilot list accounts --page 1 --pageSize 100
telegramcopilot list targets --search Alice
telegramcopilot create accounts --file account.json
telegramcopilot get accounts ACCOUNT_UUID
telegramcopilot update accounts ACCOUNT_UUID --file account-update.json
telegramcopilot delete accounts ACCOUNT_UUID --revision 2
```

这里的 `telegramcopilot` 代表发布后的可执行文件；也可以替换为 `dotnet artifacts/cli/telegramcopilot.dll`。

account.json：

```json
{"name":"Primary","enabled":true,"settings":{"apiId":12345,"apiHash":"YOUR_32_CHARACTER_HASH","phone":"+886912345678"}}
```

更新文件使用完整信封，再加入 GET 返回的 `revision`。GET 返回的 apiHash、设备密钥及加密参数为 `***`；保留该值即可保留现有密钥。创建时必须给真实值。引用其他配置时先创建上游资源；删除时先移除下游引用。

各资源完整字段见 `../TelegramCopilot.Api/README.md`。

## Telegram 登录

```sh
telegramcopilot status ACCOUNT_UUID
telegramcopilot login ACCOUNT_UUID --type code
telegramcopilot login ACCOUNT_UUID --type password
telegramcopilot login ACCOUNT_UUID --type email
telegramcopilot login ACCOUNT_UUID --type emailCode
```

交互输入不回显，不把密码放入 shell 参数或历史记录。自动化可用 `--stdin` 从标准输入提交一个挑战值。每次提交后用 status 查看下一状态。账号创建后后台会初始化 TDLib 并请求验证码，保留会话后重启通常不需重新登录。

## 在线记录与分析

```sh
telegramcopilot timeline TARGET_UUID --from 2026-11-07T00:00:00+08:00 --to 2026-11-08T00:00:00+08:00 --page 1 --pageSize 100
telegramcopilot statistics TARGET_UUID --from 2026-01-01T00:00:00+08:00 --to 2027-01-01T00:00:00+08:00 --period month --timeZone Asia/Taipei
telegramcopilot export TARGET_UUID --from 2026-11-07T00:00:00+08:00 --to 2026-11-08T00:00:00+08:00 --output records.csv
telegramcopilot events --from 2026-11-07T00:00:00+08:00 --to 2026-11-08T00:00:00+08:00 --type Error
telegramcopilot deliveries --channel Bark --page 1
```

timeline/export 可加 `--state Online`、Offline 或 Unknown。统计支持 day/week/month/year。导出所有匹配记录；CSV 是 UTF-8 BOM、引号转义及公式前缀保护。停机时段会返回 Unknown，不能将它解释为被监控对象离线。

## 通用请求和退出状态

`request` 覆盖全部 API 操作和详细参数：

```sh
telegramcopilot request GET /api/health
telegramcopilot request POST /api/accounts/ACCOUNT_UUID/login --file challenge.json
```

成功返回 0；HTTP 错误、参数错误、网络失败返回 1，并把诊断写到标准错误。其他命令将 JSON 写入标准输出，方便脚本处理。不要将含登录挑战或其他密钥的文件提交到 Git。
