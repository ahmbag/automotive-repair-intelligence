using System.Data;
using ARI.Infrastructure.Persistence.Hana;

const string migrationTable = "SCHEMA_MIGRATIONS";
const string migrationSchema = "ARI";

var options = new HanaOptions
{
    Host = Environment.GetEnvironmentVariable("Hana__Host") ?? string.Empty,
    Port = int.TryParse(Environment.GetEnvironmentVariable("Hana__Port"), out var port) ? port : 443,
    User = Environment.GetEnvironmentVariable("Hana__User") ?? string.Empty,
    Password = Environment.GetEnvironmentVariable("Hana__Password") ?? string.Empty,
    ConnectionTimeoutSeconds = 30
};

var directory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "database", "hana"));

if (!Directory.Exists(directory))
{
    throw new DirectoryNotFoundException($"Migration directory not found: {directory}");
}

var factory = new HanaConnectionFactory(options);
await using var connection = await factory.OpenConnectionAsync();

await EnsureMigrationTableAsync();

foreach (var file in Directory.EnumerateFiles(directory, "*.sql").Order(StringComparer.Ordinal))
{
    var migrationId = Path.GetFileNameWithoutExtension(file);
    if (await IsAppliedAsync(migrationId))
    {
        Console.WriteLine($"Already applied: {migrationId}");
        continue;
    }

    var statements = (await File.ReadAllTextAsync(file))
        .Split("--#STATEMENT", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    foreach (var statement in statements)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        command.CommandTimeout = options.ConnectionTimeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    await using (var record = connection.CreateCommand())
    {
        record.CommandText = $"INSERT INTO {migrationSchema}.{migrationTable} (MIGRATION_ID, APPLIED_AT_UTC) VALUES (?, CURRENT_UTCTIMESTAMP)";
        var parameter = record.CreateParameter();
        parameter.ParameterName = "p0";
        parameter.DbType = DbType.String;
        parameter.Value = migrationId;
        record.Parameters.Add(parameter);
        await record.ExecuteNonQueryAsync();
    }

    Console.WriteLine($"Applied: {migrationId}");
}

async Task EnsureMigrationTableAsync()
{
    await using var exists = connection.CreateCommand();
    exists.CommandText = "SELECT COUNT(*) FROM SYS.TABLES WHERE SCHEMA_NAME = ? AND TABLE_NAME = ?";
    foreach (var value in new[] { migrationSchema, migrationTable })
    {
        var parameter = exists.CreateParameter();
        parameter.ParameterName = $"p{exists.Parameters.Count}";
        parameter.DbType = DbType.String;
        parameter.Value = value;
        exists.Parameters.Add(parameter);
    }

    if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 0)
    {
        return;
    }

    await using var create = connection.CreateCommand();
    create.CommandText = $"""
        CREATE COLUMN TABLE {migrationSchema}.{migrationTable} (
            MIGRATION_ID NVARCHAR(200) NOT NULL PRIMARY KEY,
            APPLIED_AT_UTC TIMESTAMP NOT NULL
        )
        """;
    await create.ExecuteNonQueryAsync();
}

async Task<bool> IsAppliedAsync(string migrationId)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM {migrationSchema}.{migrationTable} WHERE MIGRATION_ID = ?";
    var parameter = command.CreateParameter();
    parameter.ParameterName = "p0";
    parameter.DbType = DbType.String;
    parameter.Value = migrationId;
    command.Parameters.Add(parameter);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) != 0;
}
