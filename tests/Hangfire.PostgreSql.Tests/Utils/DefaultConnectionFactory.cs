using Npgsql;

namespace Hangfire.Cockroach.Tests.Utils
{
  public class DefaultConnectionFactory : IConnectionFactory
  {
    /// <summary>
    /// Get or create NpgsqlConnection
    /// </summary>
    public NpgsqlConnection GetOrCreateConnection()
    {
      return ConnectionUtils.CreateConnection();
    }
  }
}