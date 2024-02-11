namespace Hangfire.Cockroach.Tests.Utils
{
    internal static class PostgreSqlStorageExtensions
    {
        public static CockroachConnection GetStorageConnection(this CockroachStorage storage)
        {
            return storage.GetConnection() as CockroachConnection;
        }
    }
}
