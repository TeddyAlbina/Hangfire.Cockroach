﻿using System;
using System.Collections.Generic;

using Dapper;

using Hangfire.Cockroach.Tests.Utils;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using Moq;

using Npgsql;

using Xunit;

namespace Hangfire.Cockroach.Tests
{
    public class PostgreSqlMonitoringApiFacts : IClassFixture<PostgreSqlStorageFixture>
    {
        private readonly PostgreSqlStorageFixture _fixture;

        public PostgreSqlMonitoringApiFacts(PostgreSqlStorageFixture fixture)
        {
            this._fixture = fixture;
        }

        [Fact]
        [CleanDatabase]
        public void GetJobs_MixedCasing_ReturnsJob()
        {
            string arrangeSql = $@"
                INSERT INTO ""{ConnectionUtils.GetSchemaName()}"".""job""(""invocationdata"", ""arguments"", ""createdat"")
                VALUES (@InvocationData, @Arguments, NOW()) RETURNING ""id""";

            Job job = Job.FromExpression(() => SampleMethod("Hello"));
            InvocationData invocationData = InvocationData.SerializeJob(job);

            this.UseConnection(connection =>
            {
                var jobId = connection.QuerySingle<Guid>(arrangeSql,
                  new
                  {
                      InvocationData = new JsonParameter(SerializationHelper.Serialize(invocationData)),
                      Arguments = new JsonParameter(invocationData.Arguments, JsonParameter.ValueType.Array),
                  });

                Mock<IState> state = new();
                state.Setup(x => x.Name).Returns(SucceededState.StateName);
                state.Setup(x => x.SerializeData())
                  .Returns(new Dictionary<string, string> {
            { "SUCCEEDEDAT", "2018-05-03T13:28:18.3939693Z" },
            { "PerformanceDuration", "53" },
            { "latency", "6730" },
                  });

                this.Commit(connection, x => x.SetJobState(jobId.ToString(), state.Object));

                IMonitoringApi monitoringApi = this._fixture.Storage.GetMonitoringApi();
                JobList<SucceededJobDto> jobs = monitoringApi.SucceededJobs(0, 10);

                Assert.NotNull(jobs);
            });
        }

        private void UseConnection(Action<NpgsqlConnection> action)
        {
            CockroachStorage storage = _fixture.SafeInit();
            action(storage.CreateAndOpenConnection());
        }

        private void Commit(
          NpgsqlConnection connection,
          Action<CockroachWriteOnlyTransaction> action)
        {
            CockroachStorage storage = _fixture.SafeInit();
            using CockroachWriteOnlyTransaction transaction = new(storage, () => connection);
            action(transaction);
            transaction.Commit();
        }

#pragma warning disable xUnit1013 // Public method should be marked as test
        public static void SampleMethod(string arg)
#pragma warning restore xUnit1013 // Public method should be marked as test
        { }
    }
}
