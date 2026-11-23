using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.TestSupport;

// Raw SQL against a test ledger, for what the store doesn't expose. Each call opens its own connection.
public static class LedgerSql
{
    public static async Task<SqliteConnection> OpenAsync(string databasePath)
    {
        SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        return connection;
    }

    public static async Task<int> ExecuteAsync(string databasePath, string sql, params object?[] values)
    {
        await using SqliteConnection connection = await OpenAsync(databasePath);
        await using SqliteCommand command = Command(connection, sql, values);
        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<object?> ScalarAsync(string databasePath, string sql, params object?[] values)
    {
        await using SqliteConnection connection = await OpenAsync(databasePath);
        await using SqliteCommand command = Command(connection, sql, values);
        return await command.ExecuteScalarAsync();
    }

    // Each row as its values joined with "|", NULL as "null" and numbers in invariant culture.
    public static async Task<IReadOnlyList<string>> RowsAsync(string databasePath, string sql, params object?[] values)
    {
        await using SqliteConnection connection = await OpenAsync(databasePath);
        await using SqliteCommand command = Command(connection, sql, values);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            string[] fields = new string[reader.FieldCount];
            for (int index = 0; index < reader.FieldCount; index++)
            {
                fields[index] = reader.IsDBNull(index)
                    ? "null"
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)!;
            }

            rows.Add(string.Join('|', fields));
        }

        return rows;
    }

    // values bind to @p0, @p1, … in order.
    private static SqliteCommand Command(SqliteConnection connection, string sql, object?[] values)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        for (int index = 0; index < values.Length; index++)
        {
            command.Parameters.AddWithValue($"@p{index}", values[index] ?? DBNull.Value);
        }

        return command;
    }
}
