using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using DataMigrator;
using MySqlConnector;
using Npgsql;

var options = MigratorOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return;
}

if (string.IsNullOrWhiteSpace(options.MySqlConnectionString) ||
    string.IsNullOrWhiteSpace(options.PostgresConnectionString))
{
    Console.Error.WriteLine("Missing required connection strings.");
    PrintHelp();
    return;
}

var schemaPath = ResolveSchemaPath(options.SchemaFilePath);
if (!File.Exists(schemaPath))
{
    Console.Error.WriteLine($"Schema file not found: {schemaPath}");
    return;
}

var dropOrderTables = LoadDropOrderTables(schemaPath);
if (dropOrderTables.Count == 0)
{
    Console.Error.WriteLine($"Could not extract table order from: {schemaPath}");
    return;
}

var insertOrderTables = dropOrderTables
    .AsEnumerable()
    .Reverse()
    .ToList();

await using var mySql = new MySqlConnection(options.MySqlConnectionString);
await using var postgres = new NpgsqlConnection(options.PostgresConnectionString);

await mySql.OpenAsync();
await postgres.OpenAsync();

var sourceTables = await GetMySqlTablesAsync(mySql);
var targetTables = await GetPostgresTablesAsync(postgres, options.PostgresSchema);

var configuredTables = options.Tables.Count > 0
    ? options.Tables
    : insertOrderTables;

var tablesToMigrate = configuredTables
    .Where(table => sourceTables.Contains(table) && targetTables.Contains(table))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

if (tablesToMigrate.Count == 0)
{
    Console.Error.WriteLine("No common tables found between MySQL and PostgreSQL.");
    return;
}

Console.WriteLine($"Schema file: {schemaPath}");
Console.WriteLine($"Tables to migrate: {tablesToMigrate.Count}");

if (options.TruncateTarget)
{
    var truncateOrderTables = dropOrderTables
        .Where(table => tablesToMigrate.Contains(table, StringComparer.OrdinalIgnoreCase))
        .ToList();

    await TruncateTargetTablesAsync(postgres, options.PostgresSchema, truncateOrderTables);
}

foreach (var table in tablesToMigrate)
{
    await MigrateTableAsync(mySql, postgres, options, table);
}

Console.WriteLine("Migration finished.");

static async Task MigrateTableAsync(
    MySqlConnection mySql,
    NpgsqlConnection postgres,
    MigratorOptions options,
    string tableName)
{
    var targetColumns = await GetPostgresColumnsAsync(postgres, options.PostgresSchema, tableName);
    var sourceColumns = await GetMySqlColumnsAsync(mySql, tableName);

    var sourceColumnSet = sourceColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var columnsToCopy = targetColumns
        .Where(column => sourceColumnSet.Contains(column.Name))
        .ToList();

    if (columnsToCopy.Count == 0)
    {
        Console.WriteLine($"Skipping {tableName}: no matching columns.");
        return;
    }

    var rowCount = await GetMySqlRowCountAsync(mySql, tableName);
    Console.WriteLine($"Migrating {tableName} ({rowCount} rows)...");

    await using var transaction = await postgres.BeginTransactionAsync();

    var selectSql =
        $"SELECT {string.Join(", ", columnsToCopy.Select(column => QuoteMySqlIdentifier(column.Name)))} " +
        $"FROM {QuoteMySqlIdentifier(tableName)}";

    await using var selectCommand = new MySqlCommand(selectSql, mySql)
    {
        CommandTimeout = 0
    };

    await using var reader = await selectCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

    var batchRows = new List<object?[]>(options.BatchSize);
    var migratedRows = 0;

    while (await reader.ReadAsync())
    {
        var values = new object?[columnsToCopy.Count];
        for (var i = 0; i < columnsToCopy.Count; i++)
        {
            values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        batchRows.Add(values);

        if (batchRows.Count >= options.BatchSize)
        {
            await InsertBatchAsync(postgres, transaction, options.PostgresSchema, tableName, columnsToCopy, batchRows);
            migratedRows += batchRows.Count;
            Console.WriteLine($"  {tableName}: {migratedRows}/{rowCount}");
            batchRows.Clear();
        }
    }

    if (batchRows.Count > 0)
    {
        await InsertBatchAsync(postgres, transaction, options.PostgresSchema, tableName, columnsToCopy, batchRows);
        migratedRows += batchRows.Count;
        batchRows.Clear();
    }

    await transaction.CommitAsync();
    Console.WriteLine($"Done {tableName}: {migratedRows} rows.");
}

static async Task InsertBatchAsync(
    NpgsqlConnection postgres,
    NpgsqlTransaction transaction,
    string schema,
    string tableName,
    IReadOnlyList<ColumnInfo> columns,
    IReadOnlyList<object?[]> rows)
{
    var sql = $"""
               INSERT INTO {QuotePostgresIdentifier(schema)}.{QuotePostgresIdentifier(tableName)}
               ({string.Join(", ", columns.Select(column => QuotePostgresIdentifier(column.Name)))})
               VALUES
               {BuildValuesClause(columns.Count, rows.Count)};
               """;

    await using var command = new NpgsqlCommand(sql, postgres, transaction)
    {
        CommandTimeout = 0
    };

    for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
    {
        var row = rows[rowIndex];
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var parameterName = $"p_{rowIndex}_{columnIndex}";
            var value = ConvertValue(columns[columnIndex], row[columnIndex]);
            command.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
        }
    }

    await command.ExecuteNonQueryAsync();
}

static string BuildValuesClause(int columnCount, int rowCount)
{
    var rows = new List<string>(rowCount);
    for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
    {
        var parameters = new string[columnCount];
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            parameters[columnIndex] = $"@p_{rowIndex}_{columnIndex}";
        }

        rows.Add($"({string.Join(", ", parameters)})");
    }

    return string.Join(",\n", rows);
}

static object? ConvertValue(ColumnInfo column, object? value)
{
    if (value is null or DBNull)
    {
        return null;
    }

    return column.PostgresType switch
    {
        "uuid" => ConvertToGuid(value),
        "boolean" => ConvertToBoolean(value),
        "date" => ConvertToDateOnly(value),
        "integer" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        "bigint" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        "numeric" or "decimal" => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        "real" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
        "double precision" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => value
    };
}

static Guid ConvertToGuid(object value)
{
    return value switch
    {
        Guid guid => guid,
        string text => Guid.Parse(text),
        byte[] bytes when bytes.Length == 16 => new Guid(bytes),
        _ => Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!)
    };
}

static bool ConvertToBoolean(object value)
{
    return value switch
    {
        bool boolean => boolean,
        sbyte int8 => int8 != 0,
        byte uint8 => uint8 != 0,
        short int16 => int16 != 0,
        ushort uint16 => uint16 != 0,
        int int32 => int32 != 0,
        long int64 => int64 != 0,
        decimal number => number != 0,
        string text when bool.TryParse(text, out var parsedBool) => parsedBool,
        string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong) => parsedLong != 0,
        _ => throw new InvalidCastException($"Cannot convert value '{value}' to boolean.")
    };
}

static object ConvertToDateOnly(object value)
{
    return value switch
    {
        DateOnly dateOnly => dateOnly,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
        _ => DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture))
    };
}

static async Task<long> GetMySqlRowCountAsync(MySqlConnection connection, string tableName)
{
    var sql = $"SELECT COUNT(*) FROM {QuoteMySqlIdentifier(tableName)}";
    await using var command = new MySqlCommand(sql, connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static async Task<HashSet<string>> GetMySqlTablesAsync(MySqlConnection connection)
{
    const string sql = """
                       SELECT table_name
                       FROM information_schema.tables
                       WHERE table_schema = DATABASE()
                         AND table_type = 'BASE TABLE';
                       """;

    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while (await reader.ReadAsync())
    {
        tables.Add(reader.GetString(0));
    }

    return tables;
}

static async Task<HashSet<string>> GetPostgresTablesAsync(NpgsqlConnection connection, string schema)
{
    const string sql = """
                       SELECT table_name
                       FROM information_schema.tables
                       WHERE table_schema = @schema
                         AND table_type = 'BASE TABLE';
                       """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("schema", schema);

    await using var reader = await command.ExecuteReaderAsync();

    var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while (await reader.ReadAsync())
    {
        tables.Add(reader.GetString(0));
    }

    return tables;
}

static async Task<List<string>> GetMySqlColumnsAsync(MySqlConnection connection, string tableName)
{
    const string sql = """
                       SELECT column_name
                       FROM information_schema.columns
                       WHERE table_schema = DATABASE()
                         AND table_name = @tableName
                       ORDER BY ordinal_position;
                       """;

    await using var command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@tableName", tableName);

    await using var reader = await command.ExecuteReaderAsync();

    var columns = new List<string>();
    while (await reader.ReadAsync())
    {
        columns.Add(reader.GetString(0));
    }

    return columns;
}

static async Task<List<ColumnInfo>> GetPostgresColumnsAsync(NpgsqlConnection connection, string schema, string tableName)
{
    const string sql = """
                       SELECT column_name, data_type
                       FROM information_schema.columns
                       WHERE table_schema = @schema
                         AND table_name = @tableName
                       ORDER BY ordinal_position;
                       """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("schema", schema);
    command.Parameters.AddWithValue("tableName", tableName);

    await using var reader = await command.ExecuteReaderAsync();

    var columns = new List<ColumnInfo>();
    while (await reader.ReadAsync())
    {
        columns.Add(new ColumnInfo(
            reader.GetString(0),
            reader.GetString(1)));
    }

    return columns;
}

static async Task TruncateTargetTablesAsync(NpgsqlConnection connection, string schema, IReadOnlyList<string> tables)
{
    if (tables.Count == 0)
    {
        return;
    }

    var targets = string.Join(", ", tables.Select(table => $"{QuotePostgresIdentifier(schema)}.{QuotePostgresIdentifier(table)}"));
    var sql = $"TRUNCATE TABLE {targets} RESTART IDENTITY CASCADE;";
    await using var command = new NpgsqlCommand(sql, connection)
    {
        CommandTimeout = 0
    };

    Console.WriteLine("Truncating target tables...");
    await command.ExecuteNonQueryAsync();
}

static string ResolveSchemaPath(string? configuredPath)
{
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        return Path.GetFullPath(configuredPath);
    }

    var current = new DirectoryInfo(Environment.CurrentDirectory);
    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, "db", "create.sql");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "db", "create.sql"));
}

static List<string> LoadDropOrderTables(string schemaPath)
{
    var sql = File.ReadAllText(schemaPath);
    var match = Regex.Match(
        sql,
        @"DROP\s+TABLE\s+IF\s+EXISTS\s*(?<tables>.*?)\s*CASCADE\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    if (!match.Success)
    {
        return [];
    }

    return match.Groups["tables"].Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(table => table
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim()
            .Trim('"'))
        .Where(table => !string.IsNullOrWhiteSpace(table))
        .ToList();
}

static string QuoteMySqlIdentifier(string identifier) => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";

static string QuotePostgresIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

static void PrintHelp()
{
    Console.WriteLine(
        """
        MySQL -> PostgreSQL data migrator

        Required:
          --mysql "<connection string>"
          --pgsql "<connection string>"

        Optional:
          --schema-file "<path to db/create.sql>"
          --schema "<postgres schema>"              default: public
          --tables "users,regions,players"         default: inferred from db/create.sql
          --batch-size 500                          default: 500
          --truncate                                truncates target tables before insert
          --help

        Example:
          dotnet run --project .\tools\DataMigrator\DataMigrator.csproj -- \
            --mysql "Server=localhost;Database=map;User ID=root;Password=root;" \
            --pgsql "Host=localhost;Database=map;Username=postgres;Password=root;" \
            --truncate
        """);
}

namespace DataMigrator
{
    internal sealed record MigratorOptions(
        string? MySqlConnectionString,
        string? PostgresConnectionString,
        string? SchemaFilePath,
        string PostgresSchema,
        int BatchSize,
        bool TruncateTarget,
        bool ShowHelp,
        IReadOnlyList<string> Tables)
    {
        public static MigratorOptions Parse(string[] args)
        {
            string? mysql = null;
            string? pgsql = null;
            string? schemaFilePath = null;
            var postgresSchema = "public";
            var batchSize = 500;
            var truncateTarget = false;
            var showHelp = false;
            var tables = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--mysql":
                        mysql = RequireValue(args, ref i);
                        break;
                    case "--pgsql":
                    case "--postgres":
                        pgsql = RequireValue(args, ref i);
                        break;
                    case "--schema-file":
                        schemaFilePath = RequireValue(args, ref i);
                        break;
                    case "--schema":
                        postgresSchema = RequireValue(args, ref i);
                        break;
                    case "--batch-size":
                        batchSize = int.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                        break;
                    case "--tables":
                        tables = RequireValue(args, ref i)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();
                        break;
                    case "--truncate":
                        truncateTarget = true;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        showHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }

            if (batchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(args), "Batch size must be greater than zero.");
            }

            return new MigratorOptions(
                mysql,
                pgsql,
                schemaFilePath,
                postgresSchema,
                batchSize,
                truncateTarget,
                showHelp,
                tables);
        }

        private static string RequireValue(string[] args, ref int index)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for argument: {args[index]}");
            }

            index++;
            return args[index];
        }
    }

    internal sealed record ColumnInfo(string Name, string PostgresType);
}