﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

using Dapper;

using Hangfire.Cockroach.Tests.Utils;

using Npgsql;

using Xunit;

namespace Hangfire.Cockroach.Tests
{
    public class ExpirationManagerFacts : IClassFixture<PostgreSqlStorageFixture>
    {
        private readonly Guid Id1 = new Guid("70F8F27E-F0EC-4EDA-A242-8835E14D7D31");
        private readonly Guid Id2 = new Guid("66D8F927-D5DD-4C42-A6B0-E683CA585069");
        private readonly Guid Id3 = new Guid("57354D89-8A41-42ED-9F78-DC3FD9F6DD32");

        private readonly PostgreSqlStorageFixture _fixture;
        private readonly CancellationToken _token;

        public ExpirationManagerFacts(PostgreSqlStorageFixture fixture)
        {
            CancellationTokenSource cts = new();
            this._token = cts.Token;
            this._fixture = fixture;
            this._fixture.SetupOptions(o => o.DeleteExpiredBatchSize = 2);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenStorageIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ExpirationManager(null));
        }

        [Fact]
        [CleanDatabase]
        public void Execute_RemovesOutdatedRecords()
        {
            this.UseConnection((connection, manager) =>
            {

                Guid CreateEntry(string key)
                {
                    return CreateExpirationEntry(connection, DateTime.UtcNow.AddMonths(-1), key);
                }

                List<Guid> entryIds = Enumerable.Range(1, 3).Select(i => CreateEntry($"key{i}")).ToList();

                manager.Execute(this._token);

                entryIds.ForEach(entryId => Assert.True(IsEntryExpired(connection, entryId)));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_DoesNotRemoveEntries_WithNoExpirationTimeSet()
        {
            this.UseConnection((connection, manager) =>
            {
                Guid entryId = CreateExpirationEntry(connection, null);

                manager.Execute(this._token);

                Assert.False(IsEntryExpired(connection, entryId));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_DoesNotRemoveEntries_WithFreshExpirationTime()
        {
            this.UseConnection((connection, manager) =>
            {
                Guid entryId = CreateExpirationEntry(connection, DateTime.Now.AddMonths(1));

                manager.Execute(this._token);

                Assert.False(IsEntryExpired(connection, entryId));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_Processes_CounterTable()
        {
            this.UseConnection((connection, manager) =>
            {
                // Arrange
                string createSql = $@"
          INSERT INTO ""{GetSchemaName()}"".""counter"" (""key"", ""value"", ""expireat"") 
          VALUES ('key', 1, @ExpireAt)
        ";
                connection.Execute(createSql, new { ExpireAt = DateTime.UtcNow.AddMonths(-1) });

                // Act
                manager.Execute(this._token);

                // Assert
                Assert.Equal(0, connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""counter"""));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_Aggregates_CounterTable()
        {
            this.UseConnection((connection, manager) =>
            {
                // Arrange
                string createSql = $@"
          INSERT INTO ""{GetSchemaName()}"".""counter"" (""key"", ""value"") 
          VALUES ('stats:succeeded', 1)
        ";
                for (int i = 0; i < 5; i++)
                {
                    connection.Execute(createSql);
                }

                // Act
                manager.Execute(this._token);

                // Assert
                Assert.Equal(1, connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""counter"""));
                Assert.Equal(5, connection.QuerySingle<long>($@"SELECT SUM(""value"") FROM ""{GetSchemaName()}"".""counter"""));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_Processes_JobTable()
        {
            this.UseConnection((connection, manager) =>
            {
                // Arrange
                string createSql = $@"
          INSERT INTO ""{GetSchemaName()}"".""job"" (""invocationdata"", ""arguments"", ""createdat"", ""expireat"") 
          VALUES ('{{}}', '[]', NOW(), @ExpireAt)
        ";
                connection.Execute(createSql, new { ExpireAt = DateTime.UtcNow.AddMonths(-1) });

                // Act
                manager.Execute(this._token);

                // Assert
                Assert.Equal(0, connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""job"""));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_Processes_ListTable()
        {
            this.UseConnection((connection, manager) =>
            {
                // Arrange
                string createSql = $@"INSERT INTO ""{GetSchemaName()}"".""list"" (""key"", ""expireat"") VALUES ('key', @ExpireAt)";
                connection.Execute(createSql, new { ExpireAt = DateTime.UtcNow.AddMonths(-1) });

                // Act
                manager.Execute(this._token);

                // Assert
                Assert.Equal(0, connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""list"""));
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_Processes_SetTable()
        {
            this.UseConnection((connection, manager) =>
            {
                // Arrange
                string createSql = $@"INSERT INTO ""{GetSchemaName()}"".""set"" (""key"", ""score"", ""value"", ""expireat"") VALUES ('key', 0, '', @ExpireAt)";
                connection.Execute(createSql, new { ExpireAt = DateTime.UtcNow.AddMonths(-1) });

                // Act
                manager.Execute(this._token);

                // Assert
                Assert.Equal(0, connection.Query<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""set""").Single());
            });
        }

        [Fact]
        [CleanDatabase]
        public void Execute_Processes_HashTable()
        {
            this.UseConnection((connection, manager) =>
            {
                // Arrange
                string createSql = $@"
          INSERT INTO ""{GetSchemaName()}"".""hash"" (""key"", ""field"", ""value"", ""expireat"") 
          VALUES ('key', 'field', '', @ExpireAt)";
                connection.Execute(createSql, new { ExpireAt = DateTime.UtcNow.AddMonths(-1) });

                // Act
                manager.Execute(this._token);

                // Assert
                Assert.Equal(0, connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""hash"""));
            });
        }

        private static Guid CreateExpirationEntry(NpgsqlConnection connection, DateTime? expireAt, string key = "key")
        {
            string insertSqlNull = $@"
        INSERT INTO ""{GetSchemaName()}"".""counter""(""key"", ""value"", ""expireat"")
        VALUES (@Key, 1, null) RETURNING ""id""
      ";

            string insertSqlValue = $@"
        INSERT INTO ""{GetSchemaName()}"".""counter""(""key"", ""value"", ""expireat"")
        VALUES (@Key, 1, NOW() - interval '{{0}} seconds') RETURNING ""id""
      ";

            string insertSql = expireAt == null
              ? insertSqlNull
              : string.Format(CultureInfo.InvariantCulture, insertSqlValue,
                ((long)(DateTime.UtcNow - expireAt.Value).TotalSeconds).ToString(CultureInfo.InvariantCulture));

            return connection.QuerySingle<Guid>(insertSql, new { Key = key });
        }

        private static bool IsEntryExpired(NpgsqlConnection connection, Guid entryId)
        {
            return connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""counter"" WHERE ""id"" = @Id", new { Id = entryId }) == 0;
        }

        private void UseConnection(Action<NpgsqlConnection, ExpirationManager> action)
        {
            CockroachStorage storage = this._fixture.SafeInit();
            ExpirationManager manager = new ExpirationManager(storage, TimeSpan.Zero);
            action(storage.CreateAndOpenConnection(), manager);
        }

        private static string GetSchemaName()
        {
            return ConnectionUtils.GetSchemaName();
        }
    }
}
