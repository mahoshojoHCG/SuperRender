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
            );
            CREATE TABLE IF NOT EXISTS cookies (
                name TEXT NOT NULL,
                value TEXT NOT NULL,
                domain TEXT NOT NULL,
                path TEXT NOT NULL,
                expires TEXT,
                secure INTEGER NOT NULL DEFAULT 0,
                http_only INTEGER NOT NULL DEFAULT 0,
                same_site TEXT NOT NULL DEFAULT 'Lax',
                PRIMARY KEY (domain, path, name)
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

    // --- Cookie persistence ---

    public void SaveCookie(string name, string value, string domain, string path,
        DateTimeOffset? expires, bool secure, bool httpOnly, string sameSite)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO cookies (name, value, domain, path, expires, secure, http_only, same_site)
            VALUES (@name, @value, @domain, @path, @expires, @secure, @httpOnly, @sameSite)
            ON CONFLICT(domain, path, name) DO UPDATE SET
                value = excluded.value,
                expires = excluded.expires,
                secure = excluded.secure,
                http_only = excluded.http_only,
                same_site = excluded.same_site
            """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@domain", domain);
        cmd.Parameters.AddWithValue("@path", path);
        cmd.Parameters.AddWithValue("@expires", expires.HasValue ? expires.Value.ToString("O") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@secure", secure ? 1 : 0);
        cmd.Parameters.AddWithValue("@httpOnly", httpOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@sameSite", sameSite);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCookie(string name, string domain, string path)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM cookies WHERE name = @name AND domain = @domain AND path = @path";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@domain", domain);
        cmd.Parameters.AddWithValue("@path", path);
        cmd.ExecuteNonQuery();
    }

    public List<(string Name, string Value, string Domain, string Path,
        DateTimeOffset? Expires, bool Secure, bool HttpOnly, string SameSite)> LoadAllCookies()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name, value, domain, path, expires, secure, http_only, same_site FROM cookies";
        var result = new List<(string, string, string, string, DateTimeOffset?, bool, bool, string)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            DateTimeOffset? expires = reader.IsDBNull(4) ? null
                : DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture);
            result.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                expires,
                reader.GetInt32(5) != 0,
                reader.GetInt32(6) != 0,
                reader.GetString(7)
            ));
        }
        return result;
    }

    public void PurgeExpiredCookies()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM cookies WHERE expires IS NOT NULL AND expires <= @now";
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
