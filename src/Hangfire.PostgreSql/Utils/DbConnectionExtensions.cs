using System.Data;
using Npgsql;

namespace Hangfire.Cockroach.Utils;

internal static class DbConnectionExtensions
{
    private static bool? supportsNotifications;

    internal static bool SupportsNotifications(this IDbConnection connection)
    {
        if (supportsNotifications.HasValue)
        {
            return supportsNotifications.Value;
        }

        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            supportsNotifications = false;
            return false;
        }

        if (npgsqlConnection.State != ConnectionState.Open)
        {
            npgsqlConnection.Open();
        }

        supportsNotifications = npgsqlConnection.PostgreSqlVersion.Major >= 11;
        return supportsNotifications.Value;
    }
}
