using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenMod.API.Plugins;
using UTowny.Configuration;
using UTowny.Plugin;

namespace UTowny.Persistence.Database;

public interface IDatabaseConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken ct = default);
    string DatabasePath { get; }
}

public sealed class DatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly Lazy<IPluginAccessor<UTownyPlugin>> m_PluginAccessor;
    private readonly IOptions<UTownyOptions> m_Options;
    private string? m_Path;
    public string DatabasePath => m_Path ??= ResolvePath();

    public DatabaseConnectionFactory(Lazy<IPluginAccessor<UTownyPlugin>> pluginAccessor, IOptions<UTownyOptions> options)
    {
        m_PluginAccessor = pluginAccessor;
        m_Options = options;
    }

    private string ResolvePath()
    {
        var plugin = m_PluginAccessor.Value.Instance ?? throw new InvalidOperationException("UTowny plugin instance is not available.");
        Directory.CreateDirectory(plugin.WorkingDirectory);
        return Path.Combine(plugin.WorkingDirectory, m_Options.Value.Database.FileName);
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        var connection = new SqliteConnection(cs);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;";
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return connection;
    }
}
