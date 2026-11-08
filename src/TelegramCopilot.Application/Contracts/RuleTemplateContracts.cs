using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Application.Contracts;

/// <summary>Localized built-in notification defaults; a destination must be chosen before saving.</summary>
public sealed record RuleTemplateResponse(string Event, string Name, string Description, RuleSettings Settings);
