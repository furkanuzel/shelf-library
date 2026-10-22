using System.Data;
using System.Data.Common;

namespace LibraryManagement;

public enum DbProvider
{
    Sqlite,
    SqlServer,
}

/// <summary>
/// Opens connections for the configured provider and creates the schema on first run.
/// Configure with environment variables:
///   LMS_PROVIDER    sqlite (default) | sqlserver
///   LMS_CONNECTION  provider connection string
///                   (default for sqlite: "Data Source=library.db")
/// </summary>
public sealed class Database
{
    public DbProvider Provider { get; }
    private readonly string _connectionString;

    public Database(DbProvider provider, string connectionString)
    {
        Provider = provider;
        _connectionString = connectionString;
    }

    public static Database FromEnvironment()
    {
        var providerName = (Environment.GetEnvironmentVariable("LMS_PROVIDER") ?? "sqlite")
            .Trim()
            .ToLowerInvariant();

        var provider = providerName switch
        {
            "sqlite" => DbProvider.Sqlite,
            "sqlserver" or "mssql" => DbProvider.SqlServer,
            _ => throw new InvalidOperationException(
                $"Unknown LMS_PROVIDER '{providerName}'. Use 'sqlite' or 'sqlserver'."),
        };

        var connectionString = Environment.GetEnvironmentVariable("LMS_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (provider == DbProvider.SqlServer)
                throw new InvalidOperationException(
                    "LMS_CONNECTION is required when LMS_PROVIDER=sqlserver.");
            connectionString = "Data Source=library.db";
        }

        return new Database(provider, connectionString);
    }

    public DbConnection Open()
    {
        DbConnection connection = Provider switch
        {
#if OFFLINE
            DbProvider.Sqlite => new LibraryManagement.Offline.OfflineSqliteConnection(_connectionString),
            _ => throw new NotSupportedException("Offline builds support SQLite only."),
#else
            DbProvider.Sqlite => new Microsoft.Data.Sqlite.SqliteConnection(_connectionString),
            DbProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(_connectionString),
            _ => throw new ArgumentOutOfRangeException(),
#endif
        };
        connection.Open();

        if (Provider == DbProvider.Sqlite)
            Execute(connection, null, "PRAGMA foreign_keys = ON;");

        return connection;
    }

    public void EnsureSchema()
    {
        using var connection = Open();
        var statements = Provider == DbProvider.Sqlite ? Schema.Sqlite : Schema.SqlServer;
        foreach (var statement in statements)
            Execute(connection, null, statement);
    }

    // ---- Small ADO.NET helpers ------------------------------------------------

    public static DbCommand Command(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    public static int Execute(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string, object?)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static long Scalar(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string, object?)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    public static List<T> Query<T>(
        DbConnection connection,
        string sql,
        Func<IDataRecord, T> map,
        params (string, object?)[] parameters)
    {
        using var command = Command(connection, null, sql, parameters);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
            rows.Add(map(reader));
        return rows;
    }
}

public static class RecordExtensions
{
    public static string Text(this IDataRecord record, string column)
    {
        var value = record[column];
        return value is DBNull ? "" : Convert.ToString(value) ?? "";
    }

    public static int Int(this IDataRecord record, string column)
    {
        var value = record[column];
        return value is DBNull ? 0 : Convert.ToInt32(value);
    }
}
