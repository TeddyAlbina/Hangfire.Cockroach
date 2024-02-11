using System;
using System.Globalization;

using Hangfire.Annotations;
using Hangfire.Cockroach.Factories;

using Npgsql;

namespace Hangfire.Cockroach.Tests.Utils
{
    public static class ConnectionUtils
    {
        private const string DatabaseVariable = "Hangfire_Cockroach_DatabaseName";
        private const string SchemaVariable = "Hangfire_Cockroach_SchemaName";

        private const string ConnectionStringTemplateVariable = "Hangfire_Cockroach_ConnectionStringTemplate";

        private const string MasterDatabaseName = "public";
        private const string DefaultDatabaseName = @"hangfire_tests";
        private const string DefaultSchemaName = @"hangfire";

        private const string DefaultConnectionStringTemplate =
          @"Host=127.0.0.1; Port=26257; Database=dev; Username=teddy; Include Error Detail=true";

        public static string GetDatabaseName() => GetEnvVariable(DatabaseVariable) ?? DefaultDatabaseName;

        public static string GetSchemaName() => GetEnvVariable(SchemaVariable) ?? DefaultSchemaName;

        public static string GetMasterConnectionString()
        {
            return string.Format(CultureInfo.InvariantCulture, GetConnectionStringTemplate(), MasterDatabaseName);
        }

        public static string GetConnectionString()
        {
            return string.Format(CultureInfo.InvariantCulture, GetConnectionStringTemplate(), GetDatabaseName());
        }

        public static NpgsqlConnectionFactory GetDefaultConnectionFactory([CanBeNull] CockroachStorageOptions options = null)
        {
            return new NpgsqlConnectionFactory(GetConnectionString(), options ?? new CockroachStorageOptions());
        }

        private static string GetConnectionStringTemplate()
        {
            return GetEnvVariable(ConnectionStringTemplateVariable)
              ?? DefaultConnectionStringTemplate;
        }

        public static NpgsqlConnection CreateConnection()
        {
            NpgsqlConnectionStringBuilder csb = new(GetConnectionString());

            NpgsqlConnection connection = new()
            {
                ConnectionString = csb.ToString(),
            };
            connection.Open();

            return connection;
        }

        public static NpgsqlConnection CreateMasterConnection()
        {
            NpgsqlConnectionStringBuilder csb = new(GetMasterConnectionString());

            NpgsqlConnection connection = new()
            {
                ConnectionString = csb.ToString(),
            };
            connection.Open();

            return connection;
        }

        private static string? GetEnvVariable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);

            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            }

            return value;
        }
    }
}
