using Microsoft.Data.Sqlite;

namespace SuperRender.Browser.Storage;

/// <summary>
/// SQLite-backed storage database shared by all origins.
/// Table schema: storage(origin TEXT, key TEXT, value TEXT, PRIMARY KEY(origin, key))
/// </summary>
public sealed class StorageDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public StorageDatabase(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS storage (
                origin TEXT NOT NULL,
                key TEXT NOT NULL,
                value TEXT NOT NULL,
                PRIMARY KEY (origin, key)
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public string? GetItem(string origin, string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM storage WHERE origin = @origin AND key = @key";
        cmd.Parameters.AddWithValue("@origin", origin);
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result as string;
    }

    public void SetItem(string origin, string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO storage (origin, key, value) VALUES (@origin, @key, @value)
            ON CONFLICT(origin, key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("@origin", origin);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    public void RemoveItem(string origin, string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM storage WHERE origin = @origin AND key = @key";
        cmd.Parameters.AddWithValue("@origin", origin);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.ExecuteNonQuery();
    }

    public void Clear(string origin)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM storage WHERE origin = @origin";
        cmd.Parameters.AddWithValue("@origin", origin);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetKeys(string origin)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT key FROM storage WHERE origin = @origin ORDER BY rowid";
        cmd.Parameters.AddWithValue("@origin", origin);
        var keys = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            keys.Add(reader.GetString(0));
        return keys;
    }

    public int GetLength(string origin)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM storage WHERE origin = @origin";
        cmd.Parameters.AddWithValue("@origin", origin);
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
