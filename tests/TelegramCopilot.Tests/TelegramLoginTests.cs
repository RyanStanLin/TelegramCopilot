using System.Text.Json.Nodes;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Infrastructure.Telegram;

namespace TelegramCopilot.Tests;

public sealed class TelegramLoginTests
{
    [Fact]
    public async Task PhoneRetry_UsesSavedNumberAndAdvancesToCode()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(ready: false);
        h.Transport.Auth(h.Client, "authorizationStateWaitPhoneNumber");
        h.Transport.Handle = (client, _) =>
        {
            h.Transport.Auth(client, "authorizationStateWaitCode");
            return new() { ["@type"] = "ok" };
        };
        var result = await h.Gateway.LoginAsync(h.Account.Id, new("phone", "+10000000000"), default);
        var sent = Assert.Single(h.Transport.Requests);
        Assert.Equal("setAuthenticationPhoneNumber", sent.Body["@type"]!.ToString());
        Assert.Equal("+886912345678", sent.Body["phone_number"]!.ToString());
        Assert.Equal("authorizationStateWaitCode", result.AuthorizationState);
    }

    [Fact]
    public async Task WrongStep_IsRejectedBeforeCallingTelegram()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(ready: false);
        h.Transport.Auth(h.Client, "authorizationStateWaitPhoneNumber");
        var error = await Assert.ThrowsAsync<TelegramException>(() =>
            h.Gateway.LoginAsync(h.Account.Id, new("code", "12345"), default));
        Assert.Equal(409, error.Code);
        Assert.Equal("LOGIN_STATE_CHANGED", error.ErrorCode);
        Assert.Empty(h.Transport.Requests);
    }

    [Fact]
    public async Task ChallengeMetadata_RefreshesWithinTheSameStateAndClearsAfterLogin()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(ready: false);
        void Code(string delivery, int length) => h.Transport.Emit(h.Client, new()
        {
            ["@type"] = "updateAuthorizationState",
            ["authorization_state"] = new JsonObject
            {
                ["@type"] = "authorizationStateWaitCode",
                ["code_info"] = new JsonObject
                {
                    ["phone_number"] = "+886912345678",
                    ["type"] = new JsonObject { ["@type"] = delivery, ["length"] = length }
                }
            }
        });
        Code("authenticationCodeTypeTelegramMessage", 5);
        Assert.Equal(new("code", "+886912345678", "authenticationCodeTypeTelegramMessage", 5),
            (await h.Gateway.StatusAsync(h.Account.Id, default)).Login);
        Code("authenticationCodeTypeSms", 6);
        Assert.Equal("authenticationCodeTypeSms", (await h.Gateway.StatusAsync(h.Account.Id, default)).Login?.Delivery);
        Assert.Equal(6, (await h.Gateway.StatusAsync(h.Account.Id, default)).Login?.CodeLength);
        h.Transport.Emit(h.Client, new()
        {
            ["@type"] = "updateAuthorizationState", ["authorization_state"] = new JsonObject
            { ["@type"] = "authorizationStateWaitPassword", ["password_hint"] = "fixture hint" }
        });
        Assert.Equal("fixture hint", (await h.Gateway.StatusAsync(h.Account.Id, default)).Login?.PasswordHint);
        h.Transport.Ready(h.Client);
        Assert.Null((await h.Gateway.StatusAsync(h.Account.Id, default)).Login);
    }

    [Fact]
    public async Task InvalidCode_RetainsActionableErrorAndSuccessfulRetryClearsIt()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(ready: false);
        h.Transport.Auth(h.Client, "authorizationStateWaitCode");
        h.Transport.Handle = (_, _) => throw new TelegramException(400, "PHONE_CODE_INVALID");
        var error = await Assert.ThrowsAsync<TelegramException>(() => h.Gateway.LoginAsync(h.Account.Id, new("code", "12345"), default));
        Assert.Equal("PHONE_CODE_INVALID", error.ErrorCode);
        var failed = await h.Gateway.StatusAsync(h.Account.Id, default);
        Assert.Equal("PHONE_CODE_INVALID", failed.Error);
        Assert.Equal("authorizationStateWaitCode", failed.AuthorizationState);
        h.Transport.Handle = (client, _) => { h.Transport.Auth(client, "authorizationStateWaitPassword"); return new() { ["@type"] = "ok" }; };
        var result = await h.Gateway.LoginAsync(h.Account.Id, new("code", "54321"), default);
        Assert.Equal("authorizationStateWaitPassword", result.AuthorizationState);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(400, "API_ID_INVALID", "API_ID_INVALID")]
    [InlineData(400, "PASSWORD_HASH_INVALID", "PASSWORD_HASH_INVALID")]
    [InlineData(400, "unexpected text containing a private value", "TELEGRAM_ERROR_400")]
    [InlineData(406, "PHONE_CODE_INVALID", "TELEGRAM_ERROR_406")]
    public void Errors_ExposeOnlyRecognizedSafeIdentifiers(int code, string message, string expected)
    {
        var error = new TelegramException(code, message);
        Assert.Equal(expected, error.ErrorCode);
        Assert.Equal($"Telegram request failed: {expected} (code {code}).", error.Message);
    }
}
