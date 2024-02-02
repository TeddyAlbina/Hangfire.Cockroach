using System;

namespace Hangfire.Cockroach.Tests.Entities
{
  public record TestJob(Guid Id, string InvocationData, string Arguments, DateTime? ExpireAt, string StateName, Guid? StateId, DateTime CreatedAt);

  public class TestJobs
  {
    public void Run(string logMessage)
    {
      Console.WriteLine("Running test job: {0}", logMessage);
    }
  }
}
