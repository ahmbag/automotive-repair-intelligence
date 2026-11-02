using System.Data;
using System.Data.Common;

namespace ARI.Infrastructure.Persistence.Hana;

internal static class HanaCommand
{
    public static DbCommand Create(
        DbConnection connection,
        string sql,
        int timeoutSeconds,
        DbTransaction? transaction = null,
        params (DbType Type, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;
        command.Transaction = transaction;

        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"p{index}";
            parameter.DbType = parameters[index].Type;
            parameter.Value = parameters[index].Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
