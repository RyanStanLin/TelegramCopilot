using System.Text.Json;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Application.Services;

public static class RuleTemplates
{
    public static IReadOnlyList<RuleTemplateResponse> ForLanguage(string language)
    {
        if (language is not ("zh-CN" or "en")) throw new ArgumentException("Template language must be zh-CN or en.");
        return Enum.GetValues<EventType>().Select(type => Create(type, language)).ToArray();
    }

    private static RuleTemplateResponse Create(EventType type, string language)
    {
        var en = language == "en";
        var (name, title, body, description) = type switch
        {
            EventType.Online => en
                ? ("Online", "{NAME} is online", "An online status was observed.", "Quiet notification when an online status is observed.")
                : ("在线提醒", "{NAME} 在线", "已检测到在线状态。", "静默提醒，检测到在线状态时发送。"),
            EventType.Offline => en
                ? ("Offline", "{NAME} went offline", "Online duration: {TIME_OF_ONLINE}", "Only a confirmed online-to-offline transition triggers this notification. Duration is unknown if the start was not observed.")
                : ("离线提醒", "{NAME} 已下线", "本次在线时长：{TIME_OF_ONLINE}", "仅确认从在线变为离线时发送。未观测到完整起点时，时长显示未知。"),
            EventType.Unknown => en
                ? ("Status unavailable", "{NAME}: status unavailable", "The online status cannot currently be confirmed. Check the monitoring details.", "Quiet notification for interrupted or privacy-limited observations; this does not mean offline.")
                : ("状态未知提醒", "暂时无法确认 {NAME} 的状态", "请查看对象详情中的连接状态和 Telegram 隐私状态。", "静默提醒，连接中断或隐私限制不代表已经离线。"),
            EventType.MonitorRestored => en
                ? ("Monitoring resumed", "{NAME}: monitoring resumed", "An observable status is available again.", "Quiet recovery notification after an observation gap.")
                : ("监控恢复提醒", "{NAME} 的监控已恢复", "已重新获取到可确认的状态。", "静默提醒，观测中断后恢复时发送。"),
            EventType.AccountReady => en
                ? ("Account ready", "{NAME} is ready", "The Telegram account is ready for monitoring and messages.", "Quiet confirmation that the account is available.")
                : ("账号就绪提醒", "{NAME} 已就绪", "Telegram 账号已可用于监控和发送消息。", "静默提醒，账号可以正常工作时发送。"),
            EventType.AccountUnavailable => en
                ? ("Account unavailable", "{NAME} is unavailable", "Open the account page to check the connection or sign in again.", "Standard alert when an account requires attention.")
                : ("账号异常提醒", "{NAME} 暂不可用", "请在账号页面检查连接，或按提示重新登录。", "普通提醒，账号需要处理时发送。"),
            EventType.WorkerStarted => en
                ? ("Service started", "Monitoring service started", "Background monitoring and scheduled messages have started.", "Quiet service startup notification.")
                : ("服务启动提醒", "监控服务已启动", "后台监控和定时消息任务已开始运行。", "静默提醒，服务启动时发送。"),
            EventType.WorkerStopped => en
                ? ("Service stopped", "Monitoring service stopped", "Background monitoring is paused until the service starts again.", "Standard notification for an orderly service shutdown.")
                : ("服务停止提醒", "监控服务已停止", "后台监控暂时停止，服务启动后恢复。", "普通提醒，服务正常关闭时发送。"),
            EventType.MessageSent => en
                ? ("Message sent", "{NAME}: message sent", "The scheduled message was sent successfully.", "Quiet confirmation of a successful message delivery.")
                : ("消息发送成功", "{NAME} 的消息已发送", "定时消息已成功发送。", "静默提醒，消息发送成功时发送。"),
            EventType.MessageFailed => en
                ? ("Message failed", "{NAME}: message failed", "Check delivery history before sending again to avoid duplicates.", "Standard alert; inspect the delivery result before retrying.")
                : ("消息发送失败", "{NAME} 的消息未能发送", "请先查看发送记录，再决定是否重试，避免重复发送。", "普通提醒，检查发送结果后再重试。"),
            _ => en
                ? ("System error", "{NAME}: attention required", "Open event history for the error and recovery steps.", "Standard alert for a system error; does not bypass silent mode.")
                : ("系统错误提醒", "{NAME} 需要处理", "请在事件记录中查看原因和处理建议。", "普通提醒，遵守设备的静音与专注模式。")
        };
        var quiet = type is EventType.Online or EventType.Unknown or EventType.MonitorRestored
            or EventType.AccountReady or EventType.WorkerStarted or EventType.MessageSent;
        var group = type is EventType.Online or EventType.Offline or EventType.Unknown or EventType.MonitorRestored
            ? "presence-{TARGET_ID}" : "TelegramCopilot";
        var parameters = JsonSerializer.SerializeToElement(new { level = quiet ? "passive" : "active", isArchive = 1 });
        return new(type.ToString(), name, description, new(Guid.Empty, type, Title: title, Body: body, Group: group, Parameters: parameters, Language: language));
    }
}
