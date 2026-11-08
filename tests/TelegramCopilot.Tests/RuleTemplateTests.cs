using TelegramCopilot.Application.Services;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Tests;

public sealed class RuleTemplateTests
{
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en")]
    public async Task BuiltInTemplates_CoverEveryEventAndPassDraftValidationWithLocalizedDurations(string language)
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var target = await s.Target(account.Id);
        var destination = await s.Configuration.SaveAsync(ResourceKind.Destinations, null,
            TestStore.Request("Bark", new DestinationSettings("https://api.day.app", ["test-device"])), default);
        var templates = RuleTemplates.ForLanguage(language);
        Assert.Equal(Enum.GetValues<EventType>().Select(x => x.ToString()), templates.Select(x => x.Event));
        var validation = new RuleValidationService(s.Configs, s.Clock);
        foreach (var template in templates)
        {
            var rule = template.Settings with { DestinationId = destination.Id, TargetId = target.Id };
            var preview = await validation.ValidateAsync(TestStore.Request(template.Name, rule), default);
            Assert.True(preview.Valid, template.Event); Assert.Empty(preview.Errors);
            Assert.Equal(language, rule.Language); Assert.Equal(1, rule.Parameters!.Value.GetProperty("isArchive").GetInt32());
            if (language == "en") Assert.DoesNotMatch("[\\u4e00-\\u9fff]", template.Name + template.Description + preview.Preview!.Body);
            if (rule.Event == EventType.Offline)
            {
                Assert.Equal("active", rule.Parameters.Value.GetProperty("level").GetString());
                Assert.Contains(language == "en" ? "2h 32m 1s" : "2小时32分1秒", preview.Preview!.Body);
            }
            if (rule.Event == EventType.Online) Assert.Equal("passive", rule.Parameters.Value.GetProperty("level").GetString());
        }
        Assert.Throws<ArgumentException>(() => RuleTemplates.ForLanguage("unknown"));
    }
}
