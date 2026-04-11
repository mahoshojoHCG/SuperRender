using Microsoft.Data.Sqlite;

namespace SuperRender.Browser.Networking;

/// <summary>
/// SQLite-backed HTTP cache database.
/// </summary>
public sealed class CacheDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public CacheDatabase(string dbPath)
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
            CREATE TABLE IF NOT EXISTS http_cache (
                url TEXT PRIMARY KEY,
                body BLOB NOT NULL,
                content_type TEXT,
                etag TEXT,
                last_modified TEXT,
                expires INTEGER,
                max_age INTEGER,
                cached_at INTEGER NOT NULL,
                no_store INTEGER NOT NULL DEFAULT 0
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public CacheEntry? Get(string url)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT body, content_type, etag, last_modified, expires, max_age, cached_at, no_store FROM http_cache WHERE url = @url";
        cmd.Parameters.AddWithValue("@url", url);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new CacheEntry
        {
            Url = url,
            Body = (byte[])reader["body"],
            ContentType = reader["content_type"] as string,
            ETag = reader["etag"] as string,
            LastModified = reader["last_modified"] as string,
            Expires = reader["expires"] is long exp ? exp : null,
            MaxAge = reader["max_age"] is long ma ? (int)ma : null,
            CachedAt = (long)reader["cached_at"],
            NoStore = (long)reader["no_store"] != 0,
        };
    }

    public void Put(CacheEntry entry)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO http_cache (url, body, content_type, etag, last_modified, expires, max_age, cached_at, no_store)
            VALUES (@url, @body, @content_type, @etag, @last_modified, @expires, @max_age, @cached_at, @no_store)
            ON CONFLICT(url) DO UPDATE SET
                body = excluded.body,
                content_type = excluded.content_type,
                etag = excluded.etag,
                last_modified = excluded.last_modified,
                expires = excluded.expires,
                max_age = excluded.max_age,
                cached_at = excluded.cached_at,
                no_store = excluded.no_store
            """;
        cmd.Parameters.AddWithValue("@url", entry.Url);
        cmd.Parameters.AddWithValue("@body", entry.Body);
        cmd.Parameters.AddWithValue("@content_type", (object?)entry.ContentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@etag", (object?)entry.ETag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@last_modified", (object?)entry.LastModified ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@expires", entry.Expires.HasValue ? (object)entry.Expires.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@max_age", entry.MaxAge.HasValue ? (object)(long)entry.MaxAge.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@cached_at", entry.CachedAt);
        cmd.Parameters.AddWithValue("@no_store", entry.NoStore ? 1L : 0L);
        cmd.ExecuteNonQuery();
    }

    public void Remove(string url)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM http_cache WHERE url = @url";
        cmd.Parameters.AddWithValue("@url", url);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

/// <summary>
/// A single cached HTTP response entry.
/// </summary>
public sealed class CacheEntry
{
    public required string Url { get; init; }
    public required byte[] Body { get; init; }
    public string? ContentType { get; init; }
    public string? ETag { get; init; }
    public string? LastModified { get; init; }
    public long? Expires { get; init; }
    public int? MaxAge { get; init; }
    public long CachedAt { get; init; }
    public bool NoStore { get; init; }
}
