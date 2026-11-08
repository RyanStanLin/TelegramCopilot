using System.Text.Json;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed class MonitoringRepository(Database db) : IMonitoringRepository
{
    public async Task StartPollAsync(Resource target, DateTimeOffset at, DateTimeOffset next, CancellationToken ct)
    {
        var settings = target.Read<TargetSettings>();
        await using var c = await db.Open(ct);
        await Database.Exec(c, """
            INSERT INTO target_monitoring(target_id,account_id,identifier,revision,poll_count,started,next_poll)
            VALUES($id,$account,$identifier,$revision,1,$at,$next)
            ON CONFLICT(target_id) DO UPDATE SET
                profile=CASE WHEN account_id=$account AND identifier=$identifier THEN profile ELSE NULL END,
                telegram_status=CASE WHEN account_id=$account AND identifier=$identifier THEN telegram_status ELSE NULL END,
                last_seen=CASE WHEN account_id=$account AND identifier=$identifier THEN last_seen ELSE NULL END,
                account_id=$account,identifier=$identifier,revision=$revision,
                poll_count=poll_count+1,started=$at,next_poll=$next,error=NULL
            """, ct, ("$id", target.Id.ToString()), ("$account", settings.AccountId.ToString()),
            ("$identifier", settings.Identifier), ("$revision", target.Revision), ("$at", Database.Ms(at)), ("$next", Database.Ms(next)));
        db.Changes.Publish();
    }
    public async Task CompletePollAsync(Resource target, Observation result, string? error, CancellationToken ct)
    {
        var succeeded = error is null && result.Reason is not ("AccountNotReady" or "ConnectionUnavailable" or "RateLimited" or "TargetReconfigured");
        await using var c = await db.Open(ct);
        await Database.Exec(c, """
            UPDATE target_monitoring SET completed=$at,succeeded=succeeded+$success,failed=failed+$failure,
                last_success=CASE WHEN $success=1 THEN $at ELSE last_success END,reason=$reason,error=$error
            WHERE target_id=$id AND revision=$revision
                AND EXISTS(SELECT 1 FROM resources WHERE id=$id AND deleted=0 AND revision=$revision)
            """, ct, ("$id", target.Id.ToString()), ("$revision", target.Revision), ("$at", Database.Ms(result.ObservedAt)),
            ("$success", succeeded ? 1 : 0), ("$failure", succeeded ? 0 : 1), ("$reason", result.Reason),
            ("$error", error ?? (succeeded ? null : result.Reason)));
        db.Changes.Publish();
    }
    public async Task ObserveAsync(Resource target, Observation o, CancellationToken ct)
    {
        var settings = target.Read<TargetSettings>();
        await using var c = await db.Open(ct);
        await Database.Exec(c, """
            INSERT INTO target_monitoring(target_id,account_id,identifier,revision,state,reason,profile,telegram_status,last_seen,observed,telegram_updated)
            VALUES($id,$account,$identifier,$revision,$state,$reason,$profile,$telegram,$seen,$at,$telegram_at)
            ON CONFLICT(target_id) DO UPDATE SET state=$state,reason=$reason,observed=$at,
                profile=COALESCE($profile,CASE WHEN account_id=$account AND identifier=$identifier THEN profile ELSE NULL END),
                telegram_status=COALESCE($telegram,CASE WHEN account_id=$account AND identifier=$identifier THEN telegram_status ELSE NULL END),
                last_seen=CASE WHEN $telegram IS NOT NULL THEN $seen WHEN account_id=$account AND identifier=$identifier THEN last_seen ELSE NULL END,
                telegram_updated=COALESCE($telegram_at,CASE WHEN account_id=$account AND identifier=$identifier THEN telegram_updated ELSE NULL END),
                account_id=$account,identifier=$identifier,revision=$revision
            WHERE observed IS NULL OR observed<=$at
            """, ct, ("$id", target.Id.ToString()), ("$account", settings.AccountId.ToString()), ("$identifier", settings.Identifier),
            ("$revision", target.Revision), ("$state", o.State.ToString()), ("$reason", o.Reason),
            ("$profile", o.Profile is null ? null : JsonSerializer.Serialize(o.Profile)), ("$telegram", o.TelegramStatus),
            ("$seen", o.TelegramStatus == "userStatusOffline" && o.TelegramAt is { } seen ? Database.Ms(seen) : null), ("$at", Database.Ms(o.ObservedAt)),
            ("$telegram_at", o.TelegramStatus is not null || o.Profile is not null ? Database.Ms(o.ObservedAt) : null));
        db.Changes.Publish();
    }
    public async Task<TargetMonitoringResponse> GetAsync(Resource target, DateTimeOffset now, CancellationToken ct)
    {
        var settings = target.Read<TargetSettings>();
        await using var c = await db.Open(ct);
        using var command = Database.Command(c, "SELECT * FROM target_monitoring WHERE target_id=$id", ("$id", target.Id.ToString()));
        await using var r = await command.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return new(target.Id, target.Enabled, 0, 0, 0, null, null, null, null, "Unknown", "NoObservation", null, false, now, null);
        DateTimeOffset? At(int index) => r.IsDBNull(index) ? null : Database.At(r.GetInt64(index));
        var current = r.GetString(1) == settings.AccountId.ToString() && r.GetString(2) == settings.Identifier;
        var profile = current && !r.IsDBNull(14) ? JsonSerializer.Deserialize<TelegramProfile>(r.GetString(14)) : null;
        var response = profile is null ? null : new TelegramProfileResponse(profile.UserId,
            string.Join(" ", new[] { profile.FirstName, profile.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
            profile.Username, profile.PhotoFileId is > 0, profile.PhotoId,
            r.IsDBNull(15) ? null : r.GetString(15), At(16), At(18));
        return new(target.Id, target.Enabled, r.GetInt64(4), r.GetInt64(5), r.GetInt64(6), At(7), At(8), At(9),
            target.Enabled && current ? At(10) : null, current ? r.GetString(11) : "Unknown", current ? r.GetString(12) : "TargetReconfigured",
            current && !r.IsDBNull(13) ? r.GetString(13) : null,
            target.Enabled && current && At(7) is { } start && (At(8) is not { } end || start > end), now, response);
    }
    public async Task<(long Count, DateTimeOffset? LastPoll, long Successful, long Failed)> TotalsAsync(CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        using var command = Database.Command(c, "SELECT COALESCE(SUM(poll_count),0),MAX(completed),COALESCE(SUM(succeeded),0),COALESCE(SUM(failed),0) FROM target_monitoring");
        await using var r = await command.ExecuteReaderAsync(ct); await r.ReadAsync(ct);
        return (r.GetInt64(0), r.IsDBNull(1) ? null : Database.At(r.GetInt64(1)), r.GetInt64(2), r.GetInt64(3));
    }
    public async Task RecoverAsync(DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        await Database.Exec(c, "UPDATE target_monitoring SET completed=$at,failed=failed+1,error='WorkerInterrupted',reason='WorkerInterrupted' WHERE started IS NOT NULL AND (completed IS NULL OR started>completed)", ct,
            ("$at", Database.Ms(at)));
    }
}
