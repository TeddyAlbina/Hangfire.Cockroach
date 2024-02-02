using System;
using System.Data;
using System.Globalization;
using Dapper;
using Hangfire.Cockroach.Tests.Entities;

namespace Hangfire.Cockroach.Tests.Utils
{
  public static class Helper
  {
 
    public static TestJob GetTestJob(IDbConnection connection, string schemaName, string jobId)
    {
      return connection
        .QuerySingle<TestJob>($@"SELECT ""id"" ""Id"", ""invocationdata"" ""InvocationData"", ""arguments"" ""Arguments"", ""expireat"" ""ExpireAt"", ""statename"" ""StateName"", ""stateid"" ""StateId"", ""createdat"" ""CreatedAt"" FROM ""{schemaName}"".""job"" WHERE ""id"" = @Id OR @Id = @FakeId",
          new { Id = Guid.Parse(jobId), @FakeId = "00000000-0000-0000-0000-000000000000" });
    }

  }
}
