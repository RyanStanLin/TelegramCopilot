# 验证记录

2026-11-07。验证对象是本仓库实现；没有使用真实 Telegram 登录凭据或 Bark 设备密钥。

## 已完成的检查

| 检查 | 结果 |
|---|---|
| .NET Release 解决方案构建 | 0 warnings、0 errors |
| xUnit / VSTest | Passed: 62, Failed: 0, Skipped: 0；TRX 位于本地 artifacts/tests/backend-final.trx |
| 测试发现 | Release `--list-tests --no-build` 成功，包含新增 worker、TDLib 协议和过期恢复用例 |
| TypeScript / Next.js | 类型检查及生产构建成功 |
| CSS | 3 个 CSS Modules 通过官方 MCP lint_css 和 @primer/stylelint-config |
| 前端约束 | zh-CN/en 字典键一致；手写前端文件少于 200 行 |
| Docker 构建 | API/CLI 的 Debian 13 镜像和 web 的 Debian Bookworm/Node 24 镜像成功构建 |
| Docker 实际运行 | 两项服务 healthy；`/api/health` 返回 Healthy、nativeAvailable=true、nativeError=null |
| API / CLI 联调 | 两个 smoke 脚本通过；镜像内 CLI 的 health 命令也返回 Healthy |
| Chromium / 虚拟 WebAuthn | 2 passed；针对 Docker 容器执行，含全部配置流程 |
| 实际容器重启 | 配置与 Passkey 保留；查询得到 5.071 秒的 WorkerUnavailable 未知区间 |

本次平台是 macOS arm64 开发机及 arm64 Linux Docker VM。API 实际运行镜像的 `/etc/os-release` 为 Debian GNU/Linux 13，所有 `libtdjson` 依赖可解析。原生库搜索路径问题在容器验收中被发现并修复为镜像环境 `LD_LIBRARY_PATH=/usr/local/lib`。

## 逐项需求与证据

| Requirement | Evidence |
|---|---|
| C# Core、独立 API、CLI，DDD 解耦 | TelegramCopilot.slnx；Core 的领域决策、Application 端口、Infrastructure 适配器；[架构](architecture.md) |
| 多账号和独立会话 | `AccountLifecycle_IsolatesSessionDirectoriesAndClosesDisabledAccount`；`SemanticAccountUpdate_DoesNotReconnectAnActiveSession` |
| 验证码、2FA、邮箱挑战 | `LoginChallenges_AreSubmittedToTheSelectedAccount` 的 code/password/email/emailCode 四种案例；真实账号登录待部署者验证 |
| 用户名/手机号、官方 TDLib、隐私状态 | `PresenceUpdates_RetainIdentityAndIgnoreThePreviousResolvedUser`；`PhoneResolution_UsesReadOnlyLookupAndDoesNotImportContacts`；`PrivateOrCoarseStatus_IsUnknownRatherThanOffline`；容器原生库加载成功 |
| 秒级观测、持续记录、去重与间隔 | `DuplicateUpdates_DoNotCreateSessionsAndOfflineCapturesDuration`；`LateObservation_CannotRewriteAConfirmedNewerState`；`QueuedPushes_ArePreservedBeforeCacheReconciliationAndDisable`；秒是记录分辨率 |
| 程序停机时段、持久化、保留历史 | `CrashRecovery_CapsKnownCoverageAtDurableCheckpoint`；`DeletedOnlineTarget_DoesNotExtendKnownPresenceAfterDeletion`；实际容器停止/恢复后返回 WorkerUnavailable |
| 状态有效期与数据库升级 | `ExpiredOnlineCoverage_IsCappedAndDelayedOfflineHasAnUnknownGap`；`TimelyOnlineRenewal_ExtendsExpiryWithoutAnotherSession`；`Recovery_DistinguishesExpiredCoverageFromProcessDowntime`；`VersionOneDatabase_IsUpgradedWithoutLosingIntervals` |
| 日/周/月/年统计、次数/总时长/平均值、分页 | `Statistics_CountDurationAverageAndUnknownCoverage_AreIndependent`；`CalendarDay_UsesRequestedTimezoneAndPaginates`；`InvalidQueryRangeAndPaging_AreRejected`；API smoke 查询四种 period |
| 日/周/月/年定时发送 | `Daily_ExactBoundary_GoesToNextDay`；`Weekly_MondayAtSecondPrecision_UsesLocalCalendar`；`Monthly_Day31_SkipsShortMonth`；`Yearly_JanuaryFirst_PreservesMidnight`；闰年及 DST 用例 |
| 不同接收人、正文、时间和发送日志 | `IndependentRecipients_PreserveMessagesAndCalendarOccurrenceTimes`；`LateOccurrence_IsRecordedAsSkippedWithoutSending`；`SendMessage_RequiresFinalAcknowledgementEvenWhenItArrivesEarly`；`SendFailure_IsPropagatedRatherThanReportedAsSent` |
| 外部发送持久化与中断 | `DeliveryClaim_IsDurableAndInterruptedSendIsUncertain`；结果不确定时不自动重放，不承诺外部 exactly-once |
| BarkerSharper、事件模板、参数与多设备 | `BarkerSharper_TransmitsTemplatesParametersAndEachDeviceKey` 使用真实库向本机 HTTP 服务验证标题、正文、时长变量、副标题、分组、额外参数和密钥；`EventRules_FilterTargetAndTypeAndPersistEachDeviceOutcome` 验证匹配及部分失败 |
| 全部配置 CRUD、CLI/API 无鉴权 | tests/api_smoke.py 覆盖六类资源 CRUD、脱敏保留、版本/引用错误、Problem Details；tests/cli_smoke.py 覆盖 CLI CRUD、错误退出、日志、统计与导出 |
| CSV 导出 | `Csv_QuotesAndNeutralizesFormulaPrefixes`；API 和 CLI smoke 验证 BOM 与 Unknown 记录；浏览器下载成功 |
| 全局一个 Passkey、首次注册 | `single_passkey_guards_console_and_configuration_workflows` 验证首次注册、已有注册后的拒绝、挑战重放拒绝、错误 Origin、未登录代理 401、退出和再次认证；SQL singleton 防止保存第二把密钥 |
| 完整 Next.js/React/Primer 配置与统计页面 | 同一浏览器用例操作六类配置、加密目标、通知模板、每年计划、频率日历、分页表格与 CSV；[设计记录](../work/primer-docs-ledger.md) 保留模式与官方来源 |
| 语言、可访问性和响应式 | 同一用例验证语言持久化、切换不丢输入、名称 Online 保持用户原文、必填焦点、确认/Escape/焦点归还、删除后搜索框焦点；1280/768/375/320px、深色和 200% 文本缩放 |
| 错误恢复 | `login_status_failure_can_be_retried` 验证真实页面的失败提示和重试 |
| Debian、Docker、说明与 Git | Dockerfile、web/Dockerfile、compose.yaml、各 README、[部署步骤](deployment-debian.md)；开发过程保留在 codex/telegram-copilot 分支的增量提交 |

## 复现命令

从仓库根目录执行；.NET 项目使用 xUnit 2 和 VSTest 命令模式。

```sh
dotnet build TelegramCopilot.slnx -c Release /bl:artifacts/build-{}.binlog
dotnet test tests/TelegramCopilot.Tests/TelegramCopilot.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=backend-final.trx' --results-directory artifacts/tests /bl:artifacts/test-{}.binlog
npm ci
npm run lint:css
npm --prefix web ci
npm --prefix web run typecheck
npm --prefix web run build
```

集成测试只对**新建的临时实例和测试卷**运行。它们创建禁用的账号/监控/发送配置及虚拟 Passkey，不应用于自己的生产数据。确保本机 5080、3300 端口可用，并已安装 Chrome：

```sh
WEBAUTHN_ORIGIN=http://localhost:3300 WEB_PORT=3300 docker compose --env-file .env.example -p telegramcopilot-qa up -d --build
python3 tests/api_smoke.py
python3 tests/cli_smoke.py
TEST_WEB_URL=http://localhost:3300 NODE_OPTIONS=--experimental-sqlite npm --prefix web exec -- playwright test --config web/playwright.config.ts
docker compose --env-file .env.example -p telegramcopilot-qa down -v
```

每次完整浏览器测试需要空白测试实例或本地重置测试 Passkey；其私钥由临时虚拟认证器持有，不能用于正式登录。Playwright 截图与 HTML 报告保存在 web/test-results 和 web/playwright-report，均不提交 Git。

## 验证边界

- 没有向真实 Telegram 联系人、机器人或 Bark 手机发送测试消息。TDLib 协议测试使用传输替身，实际服务器登录、隐私可见性、验证码投递、2FA 和设备接收需要部署者使用自己的账号验收。
- 没有执行数天的持续运行、规模/负载测试、amd64 实机测试、物理 Passkey 设备同步测试或完整读屏审计。HTTPS/DNS/Caddy 配置已提供，没有部署到一个真实公网域名。
- 截图检查了实际结构、表格列、日历尺寸、主题和窄屏功能。没有有效的同版本 GitHub 截图/DPR 基线，未声明像素级 1:1。
- 没有生成代码覆盖率或运行自动 mutation 工具。代码与断言进行过人工差异审查；这些结果不等同于覆盖率或安全审计报告。
