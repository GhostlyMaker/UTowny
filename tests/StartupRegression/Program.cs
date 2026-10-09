using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UTowny.Configuration;
using UTowny.Persistence.Database;

// Exercise the real factory before any OpenMod plugin instance exists.
// This project intentionally has no OpenMod dependency or plugin accessor.
var directory = Path.Combine(Path.GetTempPath(), "utowny-startup-" + Guid.NewGuid());
try
{
    var options = Options.Create(new UTownyOptions
    {
        Database = new DatabaseOptions { FileName = "startup.db" }
    });
    var factory = new DatabaseConnectionFactory(directory, options);
    if (factory.DatabasePath != Path.Combine(directory, "startup.db"))
        throw new Exception("Configured plugin database path was not used.");
    await using (var connection = await factory.OpenAsync())
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE startup_probe(value INTEGER); INSERT INTO startup_probe VALUES(42);";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "PRAGMA foreign_keys";
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 1)
            throw new Exception("Foreign key enforcement is disabled.");
    }
    // A new factory models a reload and must preserve the existing database.
    await using (var connection = await new DatabaseConnectionFactory(directory, options).OpenAsync())
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM startup_probe";
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 42)
            throw new Exception("Database contents were not preserved on reload.");
    }
    Console.WriteLine("PASS: database opens before plugin activation and persists across reload.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}

await DispatchRegression.RunAsync();

PermissionRegression.Run();

await CreationRegression.RunAsync();

CombatRegression.Run();


GridRegression.Run();
