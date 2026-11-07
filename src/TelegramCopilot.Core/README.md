# TelegramCopilot Core

纯领域层，仅依赖 .NET 基础库。这里没有数据库、HTTP 客户端、TDLib、BarkerSharper 或 UI 依赖。

- `Configuration/Resource`：不可变、带版本的配置资源；`ConfigurationPolicy` 验证配置与引用类型。
- `Presence/PresenceMonitor`：重复/迟到观测、真实边界、过期空档、完整在线时长及恢复事件的领域决策。
- `Presence/PresenceTimeline`：半开区间裁剪和未知覆盖填充。
- `Presence/TemplateRenderer`：非递归模板替换，完整时长未知时保持明确的 unknown。
- `Scheduling/ScheduleSettings`：日/周/月/年、IANA 时区、月末、闰年及 DST 的下一次日历执行点。

Application 定义用例和端口；Infrastructure 实现持久化、传输和后台任务。区间写入、事件与 outbox 在基础设施事务中提交，领域决策本身可独立验证。完整验证入口为根目录解决方案及 `tests/TelegramCopilot.Tests`。
