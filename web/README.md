# TelegramCopilot Web

独立 Next.js App Router + TypeScript + React + Primer Product UI 前端。页面、浏览器 API 代理及 Passkey 验证运行在 Node.js 服务端；C# API 单独运行，按需求不加鉴权。

## 本地快速开始

Node 24 推荐。Node 22.12 需要 experimental SQLite 标志，npm 脚本已设置。

```sh
cd web
npm ci
cp .env.example .env.local
npm run dev
```

浏览器打开 `http://localhost:3000`。后端应运行于 `http://127.0.0.1:5080`，或在 `.env.local` 设置 `TELEGRAMCOPILOT_API_URL`。这个变量只在服务器读取，不使用 NEXT_PUBLIC，不会将私网后端地址和浏览器会话直接交给客户端。

## 环境变量

| 变量 | 示例 | 用途 |
|---|---|---|
| TELEGRAMCOPILOT_API_URL | http://api:5080 | 后端地址，仅服务器使用 |
| WEBAUTHN_ORIGIN | https://telegram.example.com | 精确允许的浏览器 origin，包括非默认端口 |
| WEBAUTHN_RP_ID | telegram.example.com | Passkey 绑定域名，不含协议和端口 |
| WEB_DATA_PATH | /data | web.db 和会话密钥的目录 |
| PORT | 3000 | Next.js 监听端口 |

本地开发默认 origin=http://localhost:3000、RP ID=localhost。生产域名必须有有效 HTTPS；普通内网 HTTP IP 不具备 WebAuthn 的安全上下文条件。

## 唯一 Passkey

没有密钥时显示注册入口。注册完成后，SQLite singleton 约束阻止第二把密钥，即使两个请求同时通过密码学验证也只能保存一把。之后登录需要已有 Passkey。挑战有效期两分钟、绑定 HttpOnly/SameSite cookie、验证时原子消费；校验 origin/RP ID 与用户验证标志。会话为 HttpOnly、SameSite=Strict 的签名 cookie，有效期八小时；HTTPS 下设置 Secure。

所有控制台页面在服务端检查会话；`/api/backend/*` 同样检查会话，写请求还检查 Origin。独立 C# API 仍无鉴权，因此生产 Compose 只将它绑定到宿主机回环地址。不要用公网可直达的 API 绕过此边界。

首次注册没有额外管理员密码，这是产品要求。请在注册前通过 SSH 隧道、反向代理访问限制或防火墙只让自己进入。先决定长期域名，再注册。

遗失 Passkey 的恢复操作只在服务器本地执行，不提供无鉴权 HTTP 重置入口：

```sh
# 在 web 目录运行；先备份 web.db
WEB_DATA_PATH=/path/to/web-data npm run reset-passkey -- --confirm

# Compose 容器
# 重置后，下一位访问者可注册，先限制外部访问
docker compose exec web node tools/reset-passkey.mjs --confirm
```

重置同时删除未用挑战和会话签名密钥，使旧会话失效。不会删除 Telegram 配置和历史日志。Passkey 私钥始终由浏览器/认证器保管，服务器保存公钥。

## 页面

- 监控对象：对象配置、启停、进入时间线，用户名/手机号及核对间隔。
- 在线记录：对象、时间范围、时区、周期和状态过滤；次数/时长/平均值/覆盖率；每日频率日历、周期表、分页秒级明细、CSV。
- Telegram 账号：应用凭据、手机号、授权状态、验证码/2FA/邮箱挑战。
- 定时消息：发送账号、独立接收人、正文、频率、当地时间、星期/日期/月份及 IANA 时区。
- Bark 目标：服务地址、多个设备、加密配置。
- 通知规则：事件/目标过滤、标题/副标题/正文/分组模板及任意额外 Bark 参数 JSON。
- 事件日志和发送记录：筛选、分页和发送结果。ID 可从资源编辑页 URL 或 API 获取。
- 系统设置：心跳、调度宽限和默认时区；服务健康与密钥状态。

所有配置通过独立编辑页显式保存，失败保留输入，使用 revision 防止静默覆盖；删除先确认并保留历史。中英文切换入口位于页头，持久化语言偏好，使用 Intl 格式化日期/数量，不刷新或重建当前编辑表单。

## 构建与 Docker

```sh
npm run typecheck
npm run build
npm start
```

生产支持 standalone 输出。根目录 Compose 同时编排 web 和 api，镜像分别构建。`web/Dockerfile` 使用 Debian Bookworm 上的 Node 24，后端镜像使用 Debian Trixie，两者都可以运行在 Debian 宿主机。

```sh
# 仓库根目录
cp .env.example .env
docker compose up -d --build
```

WEB_DATA_PATH 必须持久化。不要提交 `.env.local`、web.db 或生产日志。恢复时保留 RP ID/HTTPS 域名。

## 设计与验证

设计依据是 Primer Product UI，非 Primer Brand。依赖固定于 package-lock，CSS Modules 与官方语义 tokens，使用 Stack、PageLayout、PageHeader、NavList、表单控件和 Table，禁用自造原生控件。研究和官方文档回退见根目录 `work/primer-docs-ledger.md`。

CSS 校验在仓库根目录执行 `npm ci && npm run lint:css`。浏览器集成验证使用临时数据目录和虚拟 WebAuthn 认证器；请参考根目录 `docs/verification.md`。真实设备 Passkey 同步、Telegram 账号可见性和 Bark 手机端效果需在你的部署中确认。
