using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Channels;
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
        public string Auth = "Starting";
        public string Connection = "Unknown";
        public string? Error;
        public DateTimeOffset NotBefore;
        public ConcurrentDictionary<long, JsonObject> Statuses { get; } = new();
    }
    private readonly TdTransport _transport;
    private readonly Database _db;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly ConcurrentDictionary<Guid, (Guid AccountId, long UserId, string Identifier)> _targets = new();
    private readonly Channel<Observation> _updates = Channel.CreateUnbounded<Observation>(new() { SingleReader = true });
    private readonly SemaphoreSlim _reconcile = new(1);
    public TelegramGateway(TdTransport transport, Database db, TimeProvider clock)
    {
        _transport = transport; _db = db; _clock = clock;
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
                if (enabled.TryGetValue(id, out var updated) && updated.Settings.GetRawText() == session.Config.Settings.GetRawText()
                    && session.Auth != "authorizationStateClosed") continue;
                _transport.Post(session.ClientId, new() { ["@type"] = "close" });
                _sessions.TryRemove(id, out _);
                foreach (var (target, info) in _targets.Where(x => x.Value.AccountId == id))
                {
                    _targets.TryRemove(target, out _);
                    _updates.Writer.TryWrite(new(target, PresenceState.Unknown, _clock.GetUtcNow(), "AccountReconfigured"));
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
            ? new AccountStatusResponse(accountId, s.Auth, s.Connection, s.Error)
            : new(accountId, "Inactive", "Unavailable", NativeError));
    }
    public async Task<AccountStatusResponse> LoginAsync(Guid accountId, LoginRequest request, CancellationToken ct)
    {
        var s = Required(accountId);
        var command = request.Type switch
        {
            "code" => new JsonObject { ["@type"] = "checkAuthenticationCode", ["code"] = request.Value },
            "password" => new() { ["@type"] = "checkAuthenticationPassword", ["password"] = request.Value },
            "email" => new() { ["@type"] = "setAuthenticationEmailAddress", ["email_address"] = request.Value },
            "emailCode" => new() { ["@type"] = "checkAuthenticationEmailCode", ["code"] = new JsonObject
                { ["@type"] = "emailAddressAuthenticationCode", ["code"] = request.Value } },
            _ => throw new ArgumentException("Login type must be code, password, email or emailCode.")
        };
        await _transport.Request(s.ClientId, command, ct);
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
    public async IAsyncEnumerable<Observation> UpdatesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var update in _updates.Reader.ReadAllAsync(ct)) yield return update;
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
        _updates.Writer.TryComplete();
    }
}
