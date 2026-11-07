using System.Text.Json.Nodes;
using TelegramCopilot.Infrastructure.Telegram;

namespace TelegramCopilot.Tests;

public sealed class TelegramSendingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendMessage_RequiresFinalAcknowledgementEvenWhenItArrivesEarly(bool early)
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        h.Transport.Handle = (client, request) =>
        {
            if (request["@type"]!.ToString() != "sendMessage") return h.DefaultResponse(client, request);
            if (early) h.Transport.Emit(client, Acknowledgement(success: true));
            return Pending();
        };
        var sending = h.Gateway.SendAsync(h.Account.Id, "@test_bot", "hello", default);
        if (!early)
        {
            Assert.False(sending.IsCompleted);
            h.Transport.Emit(h.Client, Acknowledgement(success: true));
        }
        var receipt = await sending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Sent", receipt.Status);
        Assert.Equal("99", receipt.MessageId);
        var submission = Assert.Single(h.Transport.Requests, value => value.Body["@type"]!.ToString() == "sendMessage");
        Assert.Equal("inputMessageText", submission.Body["input_message_content"]!["@type"]!.ToString());
        Assert.Equal("formattedText", submission.Body["input_message_content"]!["text"]!["@type"]!.ToString());
        Assert.Equal("hello", submission.Body["input_message_content"]!["text"]!["text"]!.ToString());
    }

    [Fact]
    public async Task SendFailure_IsPropagatedRatherThanReportedAsSent()
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        h.Transport.Handle = (client, request) => request["@type"]!.ToString() == "sendMessage"
            ? Pending() : h.DefaultResponse(client, request);
        var sending = h.Gateway.SendAsync(h.Account.Id, "@test_bot", "hello", default);
        h.Transport.Emit(h.Client, Acknowledgement(success: false));
        var error = await Assert.ThrowsAsync<TelegramException>(() => sending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(403, error.Code);
    }

    private static JsonObject Pending() => new()
    {
        ["@type"] = "message", ["id"] = -1, ["chat_id"] = 314,
        ["sending_state"] = new JsonObject { ["@type"] = "messageSendingStatePending" }
    };
    private static JsonObject Acknowledgement(bool success) => new()
    {
        ["@type"] = success ? "updateMessageSendSucceeded" : "updateMessageSendFailed",
        ["old_message_id"] = -1,
        ["message"] = new JsonObject { ["id"] = 99, ["chat_id"] = 314 },
        ["error"] = new JsonObject { ["code"] = 403 }
    };
}
