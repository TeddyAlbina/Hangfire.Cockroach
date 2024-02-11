using System;
using System.Globalization;
using System.Linq;

using Dapper;

using Hangfire.Cockroach.Tests.Utils;

using Xunit;

namespace Hangfire.Cockroach.Tests
{
    public class PostgreSqlFetchedJobFacts
    {
        private const string JobId = "id";
        private const string Queue = "queue";
        private readonly Guid Id1 = new Guid("70F8F27E-F0EC-4EDA-A242-8835E14D7D31");
        private readonly Guid Id2 = new Guid("66D8F927-D5DD-4C42-A6B0-E683CA585069");
        private readonly Guid Id3 = new Guid("57354D89-8A41-42ED-9F78-DC3FD9F6DD32");

        private readonly CockroachStorage _storage;

        public PostgreSqlFetchedJobFacts()
        {
            _storage = new CockroachStorage(ConnectionUtils.GetDefaultConnectionFactory());
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenStorageIsNull()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CockroachFetchedJob(null, Id1, JobId, Queue));

            Assert.Equal("storage", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenJobIdIsNull()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CockroachFetchedJob(_storage, Id1, null, Queue));

            Assert.Equal("jobId", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenQueueIsNull()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CockroachFetchedJob(_storage, Id1, JobId, null));

            Assert.Equal("queue", exception.ParamName);
        }

        [Fact]
        public void Ctor_CorrectlySets_AllInstanceProperties()
        {
            CockroachFetchedJob fetchedJob = new(_storage, Id1, JobId, Queue);

            Assert.Equal(Id1, fetchedJob.Id);
            Assert.Equal(JobId, fetchedJob.JobId);
            Assert.Equal(Queue, fetchedJob.Queue);
        }

        [Fact]
        [CleanDatabase]
        public void RemoveFromQueue_ReallyDeletesTheJobFromTheQueue()
        {
            // Arrange
            var id = CreateJobQueueRecord(_storage, Id1.ToString(), "default");
            CockroachFetchedJob processingJob = new(_storage, id, "1", "default");

            // Act
            processingJob.RemoveFromQueue();

            // Assert
            long count = _storage.UseConnection(null, connection =>
              connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""jobqueue"""));
            Assert.Equal(0, count);
        }

        [Fact]
        [CleanDatabase]
        public void RemoveFromQueue_DoesNotDelete_UnrelatedJobs()
        {
            // Arrange
            CreateJobQueueRecord(_storage, Id1.ToString(), "default");
            CreateJobQueueRecord(_storage, Id2.ToString(), "critical");
            CreateJobQueueRecord(_storage, Id3.ToString(), "default");

            CockroachFetchedJob fetchedJob = new CockroachFetchedJob(_storage, Guid.NewGuid(), Id1.ToString(), "default");

            // Act
            fetchedJob.RemoveFromQueue();

            // Assert
            long count = _storage.UseConnection(null, connection =>
              connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""jobqueue"""));
            Assert.Equal(3, count);
        }

        [Fact]
        [CleanDatabase]
        public void Requeue_SetsFetchedAtValueToNull()
        {
            // Arrange
            var id = CreateJobQueueRecord(_storage, Id1.ToString(), "default");
            CockroachFetchedJob processingJob = new(_storage, id, Id1.ToString(), "default");

            // Act
            processingJob.Requeue();

            // Assert
            dynamic record = _storage.UseConnection(null, connection =>
              connection.Query($@"SELECT * FROM ""{GetSchemaName()}"".""jobqueue""").Single());
            Assert.Null(record.FetchedAt);
        }

        [Fact]
        [CleanDatabase]
        public void Dispose_SetsFetchedAtValueToNull_IfThereWereNoCallsToComplete()
        {
            // Arrange
            var id = CreateJobQueueRecord(_storage, Id3.ToString(), "default");
            CockroachFetchedJob processingJob = new(_storage, id, Id3.ToString(), "default");

            // Act
            processingJob.Dispose();

            // Assert
            dynamic record = _storage.UseConnection(null, connection =>
              connection.Query($@"SELECT * FROM ""{GetSchemaName()}"".""jobqueue""").Single());
            Assert.Null(record.fetchedat);
        }

        private static Guid CreateJobQueueRecord(CockroachStorage storage, string jobId, string queue)
        {
            string arrangeSql = $@"
        INSERT INTO ""{GetSchemaName()}"".""jobqueue"" (""jobid"", ""queue"", ""fetchedat"")
        VALUES (@Id, @Queue, NOW()) RETURNING ""id""
      ";

            return
              storage.UseConnection(null, connection =>
                connection.QuerySingle<Guid>(arrangeSql, new { Id = Guid.Parse(jobId), Queue = queue }));
        }

        private static string GetSchemaName()
        {
            return ConnectionUtils.GetSchemaName();
        }
    }
}
