using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Infrastructure.Telegram;

public sealed partial class TelegramGateway : ITelegramGateway, IAsyncDisposable
{
    private sealed class Session(Resource config, int clientId)
    {
        public Resource Config { get; } = config;
        public int ClientId { get; } = clientId;
        public volatile string Auth = "Starting";
        public volatile string Connection = "Unknown";
        public volatile string? Error;
        public volatile AccountLoginChallenge? Login;
        private long _notBeforeTicks;
        public DateTimeOffset NotBefore
        {
            get => new(Interlocked.Read(ref _notBeforeTicks), TimeSpan.Zero);
            set => Interlocked.Exchange(ref _notBeforeTicks, value.UtcTicks);
        }
        public ConcurrentDictionary<long, JsonObject> Statuses { get; } = new();
    }
    private readonly ITdTransport _transport;
    private readonly Database _db;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly ConcurrentDictionary<Guid, (Guid AccountId, long UserId, string Identifier)> _targets = new();
    private readonly IObservationStream _observations;
    private readonly SemaphoreSlim _reconcile = new(1);
    public TelegramGateway(ITdTransport transport, Database db, TimeProvider clock, IObservationStream observations)
    {
        _transport = transport; _db = db; _clock = clock; _observations = observations;
        transport.Update += OnUpdate;
    }
    public bool NativeAvailable => _transport.Available;
    public string? NativeError => _transport.Error;
    public async Task ReconcileAccountsAsync(IReadOnlyList<Resource> accounts, CancellationToken ct)
    {
        await _reconcile.WaitAsync(ct);
        try
        {
            var enabled = accounts.Where(x => x.Enabled).ToDictionary(x => x.Id);
            foreach (var (id, session) in _sessions)
            {
                if (enabled.TryGetValue(id, out var updated) && updated.Read<AccountSettings>() == session.Config.Read<AccountSettings>()
                    && session.Auth != "authorizationStateClosed") continue;
                _transport.Post(session.ClientId, new() { ["@type"] = "close" });
                _sessions.TryRemove(id, out _);
                foreach (var (target, info) in _targets.Where(x => x.Value.AccountId == id))
                {
                    _targets.TryRemove(target, out _);
                    _observations.Publish(new(target, PresenceState.Unknown, _clock.GetUtcNow(), "AccountReconfigured",
                        SourceAccountId: id, SourceIdentifier: info.Identifier));
                }
            }
            if (!NativeAvailable) return;
            foreach (var account in enabled.Values)
            {
                if (_sessions.ContainsKey(account.Id)) continue;
                var client = _transport.Create();
                var session = new Session(account, client);
                _sessions[account.Id] = session;
                _transport.Post(client, new() { ["@type"] = "getAuthorizationState" });
            }
        }
        finally { _reconcile.Release(); }
    }
    public Task<AccountStatusResponse> StatusAsync(Guid accountId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.TryGetValue(accountId, out var s)
            ? new AccountStatusResponse(accountId, s.Auth, s.Connection, s.Error, s.Login,
                s.NotBefore > _clock.GetUtcNow() ? (int)Math.Ceiling((s.NotBefore - _clock.GetUtcNow()).TotalSeconds) : null)
            : new(accountId, "Inactive", "Unavailable", NativeError));
    }
    public async Task<AccountStatusResponse> LoginAsync(Guid accountId, LoginRequest request, CancellationToken ct)
    {
        var s = Required(accountId);
        var expected = s.Auth switch
        {
            "authorizationStateWaitPhoneNumber" => "phone",
            "authorizationStateWaitCode" => "code",
            "authorizationStateWaitPassword" => "password",
            "authorizationStateWaitEmailAddress" => "email",
            "authorizationStateWaitEmailCode" => "emailCode",
            _ => null
        };
        var restart = request.Type == "phone" && s.Auth is "authorizationStateWaitCode" or "authorizationStateWaitEmailCode"
            or "authorizationStateWaitPassword" or "authorizationStateWaitEmailAddress";
        if (request.Type != expected && !restart) throw new TelegramException(409, "LOGIN_STATE_CHANGED");
        if (string.IsNullOrWhiteSpace(request.Value)) throw new ArgumentException("The login value is required.");
        if (s.NotBefore > _clock.GetUtcNow())
            throw new TelegramException(429, $"retry after {(int)Math.Ceiling((s.NotBefore - _clock.GetUtcNow()).TotalSeconds)}");
        var command = request.Type switch
        {
            "phone" => new JsonObject { ["@type"] = "setAuthenticationPhoneNumber", ["phone_number"] = s.Config.Read<AccountSettings>().Phone },
            "code" => new JsonObject { ["@type"] = "checkAuthenticationCode", ["code"] = request.Value },
            "password" => new() { ["@type"] = "checkAuthenticationPassword", ["password"] = request.Value },
            "email" => new() { ["@type"] = "setAuthenticationEmailAddress", ["email_address"] = request.Value },
            "emailCode" => new() { ["@type"] = "checkAuthenticationEmailCode", ["code"] = new JsonObject
                { ["@type"] = "emailAddressAuthenticationCode", ["code"] = request.Value } },
            _ => throw new ArgumentException("Login type must be phone, code, password, email or emailCode.")
        };
        try
        {
            s.Error = null;
            await _transport.Request(s.ClientId, command, ct);
        }
        catch (TelegramException error)
        {
            s.Error = error.ErrorCode;
            if (error.RetryAfterSeconds is int seconds) s.NotBefore = _clock.GetUtcNow().AddSeconds(seconds);
            throw;
        }
        return await StatusAsync(accountId, ct);
    }
    public async Task<Observation> ObserveAsync(Resource target, CancellationToken ct)
    {
        var config = target.Read<TargetSettings>();
        var now = _clock.GetUtcNow();
        if (!_sessions.TryGetValue(config.AccountId, out var s) || s.Auth != "authorizationStateReady")
            return new(target.Id, PresenceState.Unknown, now, "AccountNotReady");
        if (s.Connection != "connectionStateReady") return new(target.Id, PresenceState.Unknown, now, "ConnectionUnavailable");
        if (s.NotBefore > now) return new(target.Id, PresenceState.Unknown, now, "RateLimited");
        var alreadyResolved = _targets.TryGetValue(target.Id, out var info);
        if (!alreadyResolved || info.AccountId != config.AccountId || info.Identifier != config.Identifier)
        {
            var userId = await ResolveUser(s, config.Identifier, ct);
            info = (config.AccountId, userId, config.Identifier);
            _targets[target.Id] = info;
            if (alreadyResolved) return new(target.Id, PresenceState.Unknown, _clock.GetUtcNow(), "TargetReconfigured");
        }
        var user = await _transport.Request(s.ClientId, new() { ["@type"] = "getUser", ["user_id"] = info.UserId }, ct);
        return ToObservation(target.Id, user["status"]!.AsObject(), _clock.GetUtcNow(), "CachedReconciliation")
            with { SourceAccountId = config.AccountId, SourceIdentifier = config.Identifier };
    }
    private Session Required(Guid id) => _sessions.TryGetValue(id, out var s) ? s
        : throw new InvalidOperationException("Account is disabled, starting or native TDLib is unavailable.");
    public async ValueTask DisposeAsync()
    {
        if (!_transport.Available) { _transport.Update -= OnUpdate; return; }
        foreach (var s in _sessions.Values) _transport.Post(s.ClientId, new() { ["@type"] = "close" });
        var until = _clock.GetUtcNow().AddSeconds(10);
        while (_transport.Available && _sessions.Values.Any(x => x.Auth != "authorizationStateClosed") && _clock.GetUtcNow() < until)
            await Task.Delay(100);
        _transport.Update -= OnUpdate;
    }
}
