# Debian 部署

目标系统为 Debian 13。以下命令在服务器执行；以有 sudo 权限的账户为例。已有 Docker 的机器从第 2 步开始。这里不会要求在宿主机安装 .NET、Node.js 或 TDLib。

## 1. 安装 Docker Engine 和 Compose

使用 [Docker 官方 Debian 软件源](https://docs.docker.com/engine/install/debian/)。已有其他容器环境时，先按官方文档确认包兼容性。

```sh
sudo apt update
sudo apt install -y ca-certificates curl git
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
sudo tee /etc/apt/sources.list.d/docker.sources <<'EOF'
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: trixie
Components: stable
Signed-By: /etc/apt/keyrings/docker.asc
EOF
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo docker version
sudo docker compose version
```

本次运行验收使用 arm64 的 Debian 13 后端容器及 Debian Bookworm 前端容器。Dockerfile 未限制 CPU 平台；amd64 构建没有在本次环境实跑。

## 2. 准备代码

如果已将本项目放入 Git 仓库，在服务器克隆该仓库。也可以在开发机器导出包含全部提交历史的 Git bundle，再传到服务器：

```sh
# 开发机器，项目根目录
mkdir -p artifacts
git bundle create artifacts/TelegramCopilot.bundle --all
scp artifacts/TelegramCopilot.bundle your-user@your-server:~/
```

```sh
# 服务器
git clone ~/TelegramCopilot.bundle TelegramCopilot
cd TelegramCopilot
git submodule update --init --recursive
cp .env.example .env
chmod 600 .env
```

## 3. 选择访问方式

只通过 SSH 隧道访问时，保留默认 `.env`，在自己的电脑执行：

```sh
ssh -L 3000:127.0.0.1:3000 -L 5080:127.0.0.1:5080 your-user@your-server
```

然后使用 `http://localhost:3000`。`localhost` 是本机安全上下文，不能替换成普通 HTTP 服务器 IP。

通过域名访问时，先将域名 DNS 指向服务器，并在**第一次注册 Passkey 前**设置：

```dotenv
WEBAUTHN_ORIGIN=https://telegram.example.com
WEBAUTHN_RP_ID=telegram.example.com
WEB_BIND=127.0.0.1
WEB_PORT=3000
BUILD_JOBS=2
```

使用现有 HTTPS 反向代理，或按 [Caddy 官方安装说明](https://caddyserver.com/docs/install#debian-ubuntu-raspbian) 安装 Caddy。将下面站点加入它的配置，替换域名：

```caddyfile
telegram.example.com {
    reverse_proxy 127.0.0.1:3000
}
```

```sh
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

首次注册前只允许自己访问站点。按照需求，第一位完成注册的人拥有这个实例唯一的 Passkey。独立 API 无鉴权，保留其 `127.0.0.1:5080` 绑定；HTTPS 代理只转发 web。

## 4. 构建并启动

```sh
sudo docker compose up -d --build
sudo docker compose ps
sudo docker compose logs --tail 100 api
curl -fsS http://127.0.0.1:5080/api/health
```

首次会编译固定提交的官方 TDLib，在低功耗机器上可能超过一小时。之后复用 Docker 构建缓存。可在 `.env` 将 `BUILD_JOBS` 降为 1，减少编译时的内存需求。

健康结果应包含 `nativeAvailable: true`；正常运行后 `status` 为 `Healthy`。`/health/live` 只证明进程存活。镜像已配置原生库搜索路径 `/usr/local/lib`。如果 TDLib 不可用，编辑配置仍可进行，监控数据会明确保持未知。

## 5. 首次注册和 Telegram 配置

1. 从选定的最终地址打开前端，注册 Passkey。
2. 在 [my.telegram.org](https://my.telegram.org) 获取自己的 API ID/hash。
3. 创建 Telegram 账号，填写自己的国际格式手机号；在账号编辑页提交验证码、2FA 密码或邮箱挑战。
4. 刷新登录状态，确认已登录且连接就绪。
5. 添加监控对象，选择观测账号，填写用户名或可发现的手机号。
6. 配置 Bark 目标与事件规则。需要定时消息时，每个接收人分别建立计划，填写各自正文、时区和时间。

完整字段、模板变量、统计定义见 [API 手册](../src/TelegramCopilot.Api/README.md)、[CLI 手册](../src/TelegramCopilot.Cli/README.md) 和 [前端手册](../web/README.md)。

## 6. 日常运行与更新

```sh
sudo docker compose logs -f api
sudo docker compose exec api dotnet cli/telegramcopilot.dll health
sudo docker compose exec api dotnet cli/telegramcopilot.dll list targets
git pull --ff-only
sudo docker compose up -d --build
```

从 bundle 克隆的项目，需要先设置自己的远程仓库，或用新的 bundle 获取更新。修改 `.env` 后同样执行 `up -d` 以重新创建容器。配置 CRUD 通过页面、CLI 或 API 即时生效。保持服务器时钟同步；计划使用 IANA 时区和系统 tzdata。

同一数据卷只运行一个 API worker。不要通过增加 API 副本来扩容。已经提交的发送操作可能在配置停用或删除后才返回结果，删除计划不会撤回已提交消息。

## 7. 备份、恢复与密钥重置

后端卷包含数据库、Data Protection 密钥环和 Telegram 会话；前端卷包含 Passkey 公钥与会话密钥。停止两项服务后，备份两个完整命名卷，再启动服务：

```sh
sudo docker compose stop
sudo docker volume ls --filter label=com.docker.compose.project
# 使用自己的卷备份工具保存 backend-data、web-data 两个完整卷
sudo docker compose start
```

恢复时保留目录权限、完整密钥环及原 RP ID/域名。后端首次启动自动执行当前数据库结构升级。不要仅复制 SQLite 主文件而遗漏 WAL、密钥环或会话目录；停止服务后备份更容易得到一致快照。

遗失 Passkey 时，先限制外部访问，再在服务器执行：

```sh
sudo docker compose exec web node tools/reset-passkey.mjs --confirm
```

下一位访问者可以重新注册，旧会话会失效。`docker compose down` 保留卷；`down -v` 会删除数据，不用于日常更新。历史日志默认保留，不自动清理。
