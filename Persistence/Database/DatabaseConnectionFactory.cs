using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UTowny.Configuration;

namespace UTowny.Persistence.Database;

public interface IDatabaseConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken ct = default);
    string DatabasePath { get; }
}

public sealed class DatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly string m_WorkingDirectory;
    private readonly IOptions<UTownyOptions> m_Options;
    private string? m_Path;
    public string DatabasePath => m_Path ??= ResolvePath();

    public DatabaseConnectionFactory(string workingDirectory, IOptions<UTownyOptions> options)
    {
        m_WorkingDirectory = workingDirectory;
        m_Options = options;
    }

    private string ResolvePath()
    {
        Directory.CreateDirectory(m_WorkingDirectory);
        return Path.Combine(m_WorkingDirectory, m_Options.Value.Database.FileName);
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
