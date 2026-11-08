using System.Text;
using BarkerSharper;
using BarkerSharper.Data;
using BarkerSharper.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Notifications;

public sealed class BarkGateway(NotificationAvatar avatars) : IBarkGateway
{
    private sealed class ExtendedNotification : BarkNotificationExtendedModel
    {
        [JsonProperty("subtitle", NullValueHandling = NullValueHandling.Ignore)]
        public string? Subtitle { get; set; }
        [JsonExtensionData] public IDictionary<string, JToken>? Extra { get; set; }
    }
    public async Task SendAsync(DestinationSettings destination, RuleSettings rule, MonitorEvent value, CancellationToken ct)
    {
        var avatar = await avatars.ForEventAsync(value, ct);
        var encryption = destination.Encryption is { } e ? new EncryptionConfiguration
        {
            Key = Encoding.UTF8.GetBytes(e.Key), IV = Encoding.UTF8.GetBytes(e.IV),
            KeySize = (AesKeySize)(Encoding.UTF8.GetByteCount(e.Key) * 8),
            CipherMode = e.Mode == "ECB" ? AesCipherMode.ECB : AesCipherMode.CBC
        } : null;
        foreach (var device in destination.DeviceKeys)
        {
            ct.ThrowIfCancellationRequested();
            var parameters = rule.Parameters is { } p ? JObject.Parse(p.GetRawText()) : new JObject();
            foreach (var property in parameters.Properties().ToArray())
            {
                if (property.Value.Type == JTokenType.String) property.Value = TemplateRenderer.Render(property.Value.ToString(), value, rule.Language);
            }
            foreach (var key in new[] { "device_key", "device_keys", "ciphertext", "iv" }) parameters.Remove(key);
            if (value.TargetId is not null) parameters.Remove("icon");
            if (avatar is not null) parameters["icon"] = avatar;
            parameters["title"] = TemplateRenderer.Render(rule.Title, value, rule.Language);
            parameters["body"] = TemplateRenderer.Render(rule.Body, value, rule.Language);
            if (rule.Subtitle is { } subtitle) parameters["subtitle"] = TemplateRenderer.Render(subtitle, value, rule.Language);
            if (rule.Group is { } group) parameters["group"] = TemplateRenderer.Render(group, value, rule.Language);
            var notification = parameters.ToObject<ExtendedNotification>()!;
            var barker = new Barker(new BarkConfiguration(new Uri(destination.ServerUrl), device, encryption));
            var result = await barker.BarkAsync(notification).WaitAsync(TimeSpan.FromSeconds(30), ct);
            if (!result.IsSucceed) throw new InvalidOperationException("Bark server rejected the notification.");
        }
    }
}
