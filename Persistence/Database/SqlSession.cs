using Microsoft.Data.Sqlite;
namespace UTowny.Persistence.Database;

// A short-lived transaction session, used only by services/repositories.
public sealed class SqlSession : IDisposable
{
    public SqliteConnection Connection { get; }
    public SqliteTransaction Transaction { get; }
    public SqlSession(SqliteConnection connection) { Connection = connection; Transaction = connection.BeginTransaction(); }
    public SqliteCommand Command(string sql, params object?[] values)
    {
        var command = Connection.CreateCommand(); command.Transaction = Transaction; command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$" + i, values[i] ?? DBNull.Value);
        return command;
    }
    public int Execute(string sql, params object?[] values) { using var c = Command(sql, values); return c.ExecuteNonQuery(); }
    public object? Scalar(string sql, params object?[] values) { using var c = Command(sql, values); var v = c.ExecuteScalar(); return v is DBNull ? null : v; }
    public long Number(string sql, params object?[] values) => Convert.ToInt64(Scalar(sql, values));
    public List<object?[]> Rows(string sql, params object?[] values)
    {
        using var c = Command(sql, values); using var r = c.ExecuteReader(); var rows = new List<object?[]>();
        while (r.Read()) { var row = new object?[r.FieldCount]; for (int i = 0; i < row.Length; i++) row[i] = r.IsDBNull(i) ? null : r.GetValue(i); rows.Add(row); }
        return rows;
    }
    public void Commit() => Transaction.Commit();
    public void Dispose() => Transaction.Dispose();
}
