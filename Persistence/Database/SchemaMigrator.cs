using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
namespace UTowny.Persistence.Database;

public interface ISchemaMigrator { Task MigrateAsync(CancellationToken ct = default); }

public sealed class SchemaMigrator : ISchemaMigrator
{
    private readonly IDatabaseConnectionFactory m_Factory;
    private readonly ILogger<SchemaMigrator> m_Logger;
    public SchemaMigrator(IDatabaseConnectionFactory factory, ILogger<SchemaMigrator> logger) { m_Factory=factory; m_Logger=logger; }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using var db = await m_Factory.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync(ct).ConfigureAwait(false);
        var sql = @"
CREATE TABLE IF NOT EXISTS schema_version(version INTEGER NOT NULL);
INSERT INTO schema_version(version) SELECT 0 WHERE NOT EXISTS(SELECT 1 FROM schema_version);
CREATE TABLE IF NOT EXISTS players(
 steam64 INTEGER PRIMARY KEY,
 balance INTEGER NOT NULL DEFAULT 0 CHECK(balance >= 0),
 playtime_seconds INTEGER NOT NULL DEFAULT 0 CHECK(playtime_seconds >= 0),
 last_seen_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS towns(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 name TEXT NOT NULL COLLATE NOCASE UNIQUE,
 mayor_steam64 INTEGER NOT NULL,
 bank_balance INTEGER NOT NULL DEFAULT 0 CHECK(bank_balance >= 0),
 pvp_enabled INTEGER NOT NULL DEFAULT 0,
 created_utc TEXT NOT NULL,
 next_upkeep_utc TEXT NOT NULL,
 tax_enabled INTEGER NOT NULL DEFAULT 0,
 tax_amount INTEGER NOT NULL DEFAULT 0 CHECK(tax_amount >= 0),
 next_tax_utc TEXT NOT NULL,
 spawn_x REAL NULL, spawn_y REAL NULL, spawn_z REAL NULL, spawn_yaw REAL NULL,
 spawn_public INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(mayor_steam64) REFERENCES players(steam64) ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS town_members(
 town_id INTEGER NOT NULL,
 player_steam64 INTEGER NOT NULL UNIQUE,
 role INTEGER NOT NULL CHECK(role BETWEEN 0 AND 2),
 joined_utc TEXT NOT NULL,
 missed_tax_cycles INTEGER NOT NULL DEFAULT 0 CHECK(missed_tax_cycles >= 0),
 PRIMARY KEY(town_id, player_steam64),
 FOREIGN KEY(town_id) REFERENCES towns(id) ON DELETE CASCADE,
 FOREIGN KEY(player_steam64) REFERENCES players(steam64) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_town_members_town ON town_members(town_id);
CREATE TABLE IF NOT EXISTS claims(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 town_id INTEGER NOT NULL,
 map_id TEXT NOT NULL,
 grid_x INTEGER NOT NULL,
 grid_z INTEGER NOT NULL,
 plot_owner_steam64 INTEGER NULL,
 for_sale INTEGER NOT NULL DEFAULT 0,
 price INTEGER NOT NULL DEFAULT 0 CHECK(price >= 0),
 protection_flags INTEGER NOT NULL DEFAULT 0,
 UNIQUE(map_id, grid_x, grid_z),
 FOREIGN KEY(town_id) REFERENCES towns(id) ON DELETE CASCADE,
 FOREIGN KEY(plot_owner_steam64) REFERENCES players(steam64) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS ix_claims_town ON claims(town_id);
CREATE INDEX IF NOT EXISTS ix_claims_owner ON claims(plot_owner_steam64);
CREATE TABLE IF NOT EXISTS town_invites(
 town_id INTEGER NOT NULL,
 player_steam64 INTEGER NOT NULL,
 invited_by_steam64 INTEGER NOT NULL,
 expires_utc TEXT NOT NULL,
 PRIMARY KEY(town_id, player_steam64),
 FOREIGN KEY(town_id) REFERENCES towns(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS scheduled_receipts(
 kind TEXT NOT NULL,
 subject_id TEXT NOT NULL,
 period_utc TEXT NOT NULL,
 completed_utc TEXT NOT NULL,
 PRIMARY KEY(kind, subject_id, period_utc)
);
";
        await using var cmd = db.CreateCommand(); cmd.Transaction=(SqliteTransaction)tx; cmd.CommandText=sql;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await using var ver = db.CreateCommand(); ver.Transaction=(SqliteTransaction)tx; ver.CommandText="UPDATE schema_version SET version=1 WHERE version < 1;";
        await ver.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        m_Logger.LogInformation("UTowny schema ready at {DatabasePath}, schema version 1", m_Factory.DatabasePath);
    }
}
