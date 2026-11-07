# TelegramCopilot

C# / .NET 10 的 Telegram 多账号在线状态记录、定时消息和 Bark 通知服务，配套独立 Next.js + React + Primer Web 管理界面。目标部署环境是 Debian 13；前后端分别构建和运行。

生产接入见 [DEPLOYMENT.md](DEPLOYMENT.md)。前端现在通过独立 Git 仓库的 `web` submodule 引用；克隆本仓库时使用 `git clone --recurse-submodules`，或先执行 `git submodule update --init --recursive`。

## 当前功能

- Core → Application → Infrastructure → API/CLI 的领域分层，官方 TDLib JSON 接口，不使用 Bot API 冒充用户登录。
- 账号、监控对象、定时消息、Bark 目标、通知规则和系统配置的增删查改。配置持久化、版本冲突检测、引用保护和密钥脱敏。
- Telegram 手机验证码、两步验证密码、邮箱和邮箱验证码流程，多账号会话目录隔离。
- 在线/离线/未知三态区间、事件、心跳及重启恢复。所有 API 时间为带偏移的 ISO 8601；区间为 `[from,to)`。
- 分页明细、按日/周/月/年和 IANA 时区统计、频率日历、UTF-8 CSV 导出。
- 每个接收对象独立的日历计划和正文，支持每天、每周某天、每月某日、每年某月某日，精确到秒。
- BarkerSharper 通知，标题/正文/副标题/分组模板、额外 Bark 参数、多个设备、AES CBC/ECB。
- 首次访问注册唯一 Passkey，后续必须用它登录；服务端代理保护全部页面和浏览器 API 请求。中英文切换不会清空表单。

秒是时间戳和调度的分辨率，并非 Telegram 可见性或网络延迟的保证。用户隐私设置可能只允许取得“最近上线”等模糊状态；断线和停机期间不会补造上下线记录。详见 [监控语义](docs/presence.md)。

## Docker 快速开始

需要 Docker Engine 与 Compose 插件。Debian 13 安装 Docker 的具体系统步骤以 [Docker 官方 Debian 文档](https://docs.docker.com/engine/install/debian/) 为准。

```sh
cp .env.example .env
docker compose up -d --build
docker compose ps
docker compose logs -f api
```

首次构建会从固定提交编译 Telegram 官方 TDLib，耗时较长，建议至少 4 GB 可用内存。内存较少时在 `.env` 设置 `BUILD_JOBS=1`。默认 `BUILD_JOBS=2`，支持 amd64/arm64 原生构建。

从安装 Docker、传输 Git 历史到 HTTPS、备份的完整步骤见 [Debian 部署手册](docs/deployment-debian.md)。本次实际运行验证的平台是 arm64。

在运行 Docker 的电脑打开 `http://localhost:3000`，注册你的 Passkey。远程 Debian 服务器可以先建立 SSH 隧道：

```sh
ssh -L 3000:127.0.0.1:3000 your-user@your-server
```

然后在本机访问 `http://localhost:3000`。此方式的 RP ID 是 `localhost`。要长期通过域名使用，请在**第一次注册前**设置最终域名：

```dotenv
WEBAUTHN_ORIGIN=https://telegram.example.com
WEBAUTHN_RP_ID=telegram.example.com
WEB_BIND=127.0.0.1
WEB_PORT=3000
```

在宿主机使用 HTTPS 反向代理，参考 [Caddy 配置](docs/Caddyfile.example)。首次注册前限制域名访问，完成注册后再开放。Passkey 绑定 RP ID；切换域名时需要按前端手册本地重置，再重新注册。

API 根据需求**无鉴权**。默认只映射到宿主机 `127.0.0.1:5080`；前端在 Compose 内部访问它。Passkey 不会给独立 C# API 增加鉴权。需要在远程机器使用 CLI 时可以转发 API 端口：

```sh
ssh -L 5080:127.0.0.1:5080 your-user@your-server
```

## 首次配置顺序

1. 在 [my.telegram.org](https://my.telegram.org) 创建自己的 Telegram 应用，取得 `api_id` / `api_hash`。
2. 前端“Telegram 账号”中新建账号，填国际格式手机号；进入该账号编辑页查看登录状态。
3. 按状态提交手机验证码、两步验证密码或邮箱挑战，刷新直到已登录。不会代替你注册新的 Telegram 用户。
4. 新建监控对象，选择账号，输入 `@username` 或国际格式 `+手机号`。手机号码必须可被当前账号发现；不会导入或修改联系人。
5. 在“在线记录”选择对象和时间范围，查看频率日历、覆盖率、统计和时间线，或导出 CSV。
6. 如需推送，先建 Bark 目标，再建事件规则；如需自动发消息，给每个接收人分别建计划。

## 本地开发

.NET 10 SDK，Node.js 24（Node 22.12 亦可，脚本启用 experimental SQLite）。后端可在没有本机 TDLib 的情况下启动以编辑配置；此时健康状态为 Degraded，监控区间为 Unknown。真实登录和监控需要原生库或使用 Docker 后端。

```sh
dotnet restore TelegramCopilot.slnx /bl:artifacts/restore-{}.binlog
dotnet build TelegramCopilot.slnx /bl:artifacts/build-{}.binlog
install -d -m 700 data
DataPath=./data dotnet src/TelegramCopilot.Api/bin/Debug/net10.0/TelegramCopilot.Api.dll
```

另开终端：

```sh
cd web
npm ci
cp .env.example .env.local
npm run dev
```

访问 `http://localhost:3000`；OpenAPI 为 `http://127.0.0.1:5080/openapi/v1.json`。

## CLI

```sh
dotnet src/TelegramCopilot.Cli/bin/Debug/net10.0/telegramcopilot.dll help
dotnet src/TelegramCopilot.Cli/bin/Debug/net10.0/telegramcopilot.dll health
dotnet src/TelegramCopilot.Cli/bin/Debug/net10.0/telegramcopilot.dll list targets
```

完整操作见 [CLI 手册](src/TelegramCopilot.Cli/README.md)。CLI 使用同一份 API 契约，覆盖全部配置和查询；API 进程负责后台监控，不要启动第二个进程共享同一数据目录。

## 数据和备份

Compose 使用 `backend-data` 与 `web-data` 两个命名卷。后端包含 SQLite、Data Protection 密钥环和 TDLib 会话；前端卷包含唯一 Passkey、公钥计数器及会话签名密钥。配置 JSON 加密存储，密钥环及 TDLib 会话目录仍属于敏感数据。备份必须包含两个完整数据卷。

一致性备份应先停止服务，再备份卷，恢复后以原来的域名启动。`docker compose down` 保留卷；`down -v` 会永久删除卷和登录凭据。

## 验证与文档

```sh
dotnet test TelegramCopilot.slnx /bl:artifacts/test-{}.binlog
npm ci
npm run lint:css
npm --prefix web run typecheck
npm --prefix web run build
```

- [后端/API 手册](src/TelegramCopilot.Api/README.md)
- [Debian 完整部署](docs/deployment-debian.md)
- [前端部署和 Passkey 手册](web/README.md)
- [在线状态、统计和断线语义](docs/presence.md)
- [架构和外部发送一致性](docs/architecture.md)
- [Primer 研究与设计记录](work/primer-docs-ledger.md)
- [验证记录](docs/verification.md)

真实 Telegram 登录、隐私可见性和 Bark 设备接收效果需要使用你自己的账号验证；本仓库的自动测试不会向真实联系人发送消息。
