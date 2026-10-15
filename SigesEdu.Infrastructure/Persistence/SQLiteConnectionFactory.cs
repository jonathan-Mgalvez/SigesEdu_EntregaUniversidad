using System.Data;
using Microsoft.Data.Sqlite;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Infrastructure.Persistence;

public class SQLiteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SQLiteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
        AsegurarDirectorioBaseDatos();
    }

    /// <summary>
    /// Crea y abre una conexión a SQLite aplicando las directivas arquitectónicas obligatorias (WAL, foreign_keys, busy_timeout).
    /// </summary>
    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
            ";
            cmd.ExecuteNonQuery();
        }

        return connection;
    }

    private void AsegurarDirectorioBaseDatos()
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(_connectionString);
            if (!string.IsNullOrWhiteSpace(builder.DataSource) && builder.DataSource != ":memory:")
            {
                var fullPath = Path.GetFullPath(builder.DataSource);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
        catch
        {
            // Ignorar si la cadena usa fuentes especiales o en memoria
        }
    }
}