using Microsoft.Data.Sqlite;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed class Database
{
    private readonly string _connection;
    public string DirectoryPath { get; }
    public Database(string dataPath)
    {
        DirectoryPath = Path.GetFullPath(dataPath);
        Directory.CreateDirectory(DirectoryPath);
        _connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(DirectoryPath, "telegramcopilot.db"),
            Mode = SqliteOpenMode.ReadWriteCreate, DefaultTimeout = 15, ForeignKeys = true }.ToString();
    }
    public async Task<SqliteConnection> Open(CancellationToken ct)
    {
        var c = new SqliteConnection(_connection);
        await c.OpenAsync(ct);
        return c;
    }
    public async Task Initialize(CancellationToken ct)
    {
        await using var c = await Open(ct);
        await Exec(c, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY);
            INSERT OR IGNORE INTO schema_version VALUES (1);
            CREATE TABLE IF NOT EXISTS resources (
                id TEXT PRIMARY KEY, kind TEXT NOT NULL, body TEXT NOT NULL, revision INTEGER NOT NULL, deleted INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_resources_kind ON resources(kind,deleted);
            CREATE TABLE IF NOT EXISTS intervals (
                id INTEGER PRIMARY KEY AUTOINCREMENT, target_id TEXT NOT NULL, state TEXT NOT NULL,
                start INTEGER NOT NULL, end INTEGER, reason TEXT NOT NULL, observed INTEGER NOT NULL,
                telegram_at INTEGER, start_known INTEGER NOT NULL, end_known INTEGER NOT NULL DEFAULT 0);
            CREATE UNIQUE INDEX IF NOT EXISTS ix_intervals_open ON intervals(target_id) WHERE end IS NULL;
            CREATE INDEX IF NOT EXISTS ix_intervals_range ON intervals(target_id,start,end);
            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT, target_id TEXT, account_id TEXT, name TEXT NOT NULL,
                type TEXT NOT NULL, at INTEGER NOT NULL, online_seconds REAL, detail TEXT NOT NULL,
                notified INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_events_range ON events(at,target_id,type);
            CREATE INDEX IF NOT EXISTS ix_events_outbox ON events(notified,id);
            CREATE TABLE IF NOT EXISTS worker (id INTEGER PRIMARY KEY CHECK(id=1), heartbeat INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS deliveries (
                id TEXT PRIMARY KEY, channel TEXT NOT NULL, resource_id TEXT NOT NULL, due_at INTEGER NOT NULL,
                status TEXT NOT NULL, external_id TEXT, error TEXT, updated_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_deliveries_history ON deliveries(updated_at,resource_id);
            """, ct);
    }
    internal static async Task<int> Exec(SqliteConnection c, string sql, CancellationToken ct,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(c, sql, parameters);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
    internal static SqliteCommand Command(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        var cmd = c.CreateCommand(); cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    internal static long Ms(DateTimeOffset t) => t.ToUnixTimeMilliseconds();
    internal static DateTimeOffset At(long t) => DateTimeOffset.FromUnixTimeMilliseconds(t);
}
