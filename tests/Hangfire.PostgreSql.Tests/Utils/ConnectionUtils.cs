using System;
using System.Globalization;
using Hangfire.Annotations;
using Hangfire.Cockroach.Factories;
using Npgsql;

namespace Hangfire.Cockroach.Tests.Utils
{
  public static class ConnectionUtils
  {
    private const string DatabaseVariable = "Hangfire_PostgreSql_DatabaseName";
    private const string SchemaVariable = "Hangfire_PostgreSql_SchemaName";

    private const string ConnectionStringTemplateVariable = "Hangfire_PostgreSql_ConnectionStringTemplate";

    private const string MasterDatabaseName = "postgres";
    private const string DefaultDatabaseName = @"hangfire_tests";
    private const string DefaultSchemaName = @"hangfire";

    private const string DefaultConnectionStringTemplate = @"Host=plucky-jaguar-483.jxf.cockroachlabs.cloud; Port=26257; Database=bluecurve-jobs-dev; Username=teddy; Password=YIc-CH22W_lJLl7xZ72CQQ; SSL Mode=VerifyFull;Include Error Detail=true";

    public static string GetDatabaseName()
    {
      return Environment.GetEnvironmentVariable(DatabaseVariable) ?? DefaultDatabaseName;
    }

    public static string GetSchemaName()
    {
      return Environment.GetEnvironmentVariable(SchemaVariable) ?? DefaultSchemaName;
    }

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
      return Environment.GetEnvironmentVariable(ConnectionStringTemplateVariable)
        ?? DefaultConnectionStringTemplate;
    }

    public static NpgsqlConnection CreateConnection()
    {
      NpgsqlConnectionStringBuilder csb = new(GetConnectionString());

      NpgsqlConnection connection = new() {
        ConnectionString = csb.ToString(),
      };
      connection.Open();

      return connection;
    }

    public static NpgsqlConnection CreateMasterConnection()
    {
      NpgsqlConnectionStringBuilder csb = new(GetMasterConnectionString());

      NpgsqlConnection connection = new() {
        ConnectionString = csb.ToString(),
      };
      connection.Open();

      return connection;
    }
  }
}
