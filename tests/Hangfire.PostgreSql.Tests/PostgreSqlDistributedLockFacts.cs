﻿using System;
using System.Data;
using System.Linq;
using System.Threading;

using Dapper;

using Hangfire.Cockroach.Tests.Utils;

using Moq;

using Npgsql;

using Xunit;

namespace Hangfire.Cockroach.Tests
{
    public class PostgreSqlDistributedLockFacts : IDisposable
    {
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private NpgsqlConnection _connection;

        public void Dispose()
        {
            _connection?.Dispose();
        }

        [Fact]
        public void Acquire_ThrowsAnException_WhenResourceIsNullOrEmpty()
        {
            CockroachStorageOptions options = new();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
              () => CockroachDistributedLock.Acquire(new Mock<IDbConnection>().Object, "", _timeout, options));

            Assert.Equal("resource", exception.ParamName);
        }

        [Fact]
        public void Acquire_ThrowsAnException_WhenConnectionIsNull()
        {
            CockroachStorageOptions options = new();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => CockroachDistributedLock.Acquire(null, "hello", _timeout, options));

            Assert.Equal("connection", exception.ParamName);
        }

        [Fact]
        public void Acquire_ThrowsAnException_WhenOptionsIsNull()
        {
            Mock<IDbConnection> connection = new Mock<IDbConnection>();
            connection.SetupGet(c => c.State).Returns(ConnectionState.Open);
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
              () => CockroachDistributedLock.Acquire(new Mock<IDbConnection>().Object, "hi", _timeout, null));

            Assert.Equal("options", exception.ParamName);
        }


        [Fact]
        [CleanDatabase]
        public void Acquire_AcquiresExclusiveApplicationLock_WithUseNativeDatabaseTransactions_OnSession()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = true,
            };

            UseConnection(connection =>
            {
                // ReSharper disable once UnusedVariable
                CockroachDistributedLock.Acquire(connection, "hello", _timeout, options);

                long lockCount = connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""lock"" WHERE ""resource"" = @Resource",
                  new { Resource = "hello" });

                Assert.Equal(1, lockCount);
                //Assert.Equal("Exclusive", lockMode);
            });
        }

        [Fact]
        [CleanDatabase]
        public void Acquire_AcquiresExclusiveApplicationLock_WithUseNativeDatabaseTransactions_OnSession_WhenDeadlockOccurs()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = true,
                DistributedLockTimeout = TimeSpan.FromSeconds(10),
            };

            UseConnection(connection =>
            {
                // Arrange
                TimeSpan timeout = TimeSpan.FromSeconds(15);
                string resourceName = "hello";
                connection.Execute($@"INSERT INTO ""{GetSchemaName()}"".""lock"" VALUES (@ResourceName, 0, @Now)", new { ResourceName = resourceName, Now = DateTime.UtcNow });

                // Act && Assert (not throwing means it worked)
                CockroachDistributedLock.Acquire(connection, resourceName, timeout, options);
            });
        }

        [Fact]
        [CleanDatabase]
        public void Acquire_AcquiresExclusiveApplicationLock_WithoutUseNativeDatabaseTransactions_OnSession()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = false,
            };

            UseConnection(connection =>
            {
                // Acquire locks on two different resources to make sure they don't conflict.
                CockroachDistributedLock.Acquire(connection, "hello", _timeout, options);
                CockroachDistributedLock.Acquire(connection, "hello2", _timeout, options);

                long lockCount = connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""lock"" WHERE ""resource"" = @Resource",
                  new { Resource = "hello" });

                Assert.Equal(1, lockCount);
            });
        }


        [Fact]
        [CleanDatabase]
        public void Acquire_ThrowsAnException_IfLockCanNotBeGranted_WithUseNativeDatabaseTransactions()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = true,
            };

            ManualResetEventSlim releaseLock = new(false);
            ManualResetEventSlim lockAcquired = new(false);

            Thread thread = new(() => UseConnection(connection1 =>
            {
                CockroachDistributedLock.Acquire(connection1, "exclusive", _timeout, options);
                lockAcquired.Set();
                releaseLock.Wait();
                CockroachDistributedLock.Release(connection1, "exclusive", options);
            }));
            thread.Start();

            lockAcquired.Wait();

            UseConnection(connection2 =>
              Assert.Throws<CockroachDistributedLockException>(() => CockroachDistributedLock.Acquire(connection2, "exclusive", _timeout, options)));

            releaseLock.Set();
            thread.Join();
        }

        [Fact]
        [CleanDatabase]
        public void Acquire_ThrowsAnException_IfLockCanNotBeGranted_WithoutUseNativeDatabaseTransactions()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = false,
            };

            ManualResetEventSlim releaseLock = new(false);
            ManualResetEventSlim lockAcquired = new(false);

            Thread thread = new(() => UseConnection(connection1 =>
            {
                CockroachDistributedLock.Acquire(connection1, "exclusive", _timeout, options);
                lockAcquired.Set();
                releaseLock.Wait();
                CockroachDistributedLock.Release(connection1, "exclusive", options);
            }));
            thread.Start();

            lockAcquired.Wait();

            UseConnection(connection2 =>
              Assert.Throws<CockroachDistributedLockException>(() => CockroachDistributedLock.Acquire(connection2, "exclusive", _timeout, options)));

            releaseLock.Set();
            thread.Join();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [CleanDatabase]
        public void Acquire_ExpiredLockExists_LocksAnyway(bool useNativeDatabaseTransactions)
        {
            const string resource = "hello";

            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = useNativeDatabaseTransactions,
            };

            UseConnection(connection =>
            {
                DateTime acquired = DateTime.UtcNow - options.DistributedLockTimeout - TimeSpan.FromMinutes(1);
                connection.Execute($@"INSERT INTO ""{GetSchemaName()}"".""lock"" (""resource"", ""acquired"") VALUES (@Resource, @Acquired)", new { Resource = resource, Acquired = acquired });

                CockroachDistributedLock.Acquire(connection, resource, _timeout, options);

                long lockCount = connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""lock"" WHERE ""resource"" = @Resource",
                  new { Resource = resource });

                Assert.Equal(1, lockCount);
            });
        }

        [Fact]
        [CleanDatabase]
        public void Dispose_ReleasesExclusiveApplicationLock_WithUseNativeDatabaseTransactions()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = true,
            };

            UseConnection(connection =>
            {
                CockroachDistributedLock.Acquire(connection, "hello", _timeout, options);
                CockroachDistributedLock.Release(connection, "hello", options);

                long lockCount = connection.QuerySingle<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""lock"" WHERE ""resource"" = @Resource",
                  new { Resource = "hello" });

                Assert.Equal(0, lockCount);
            });
        }

        [Fact]
        [CleanDatabase]
        public void Dispose_ReleasesExclusiveApplicationLock_WithoutUseNativeDatabaseTransactions()
        {
            CockroachStorageOptions options = new()
            {
                SchemaName = GetSchemaName(),
                UseNativeDatabaseTransactions = false,
            };

            UseConnection(connection =>
            {
                CockroachDistributedLock.Acquire(connection, "hello", _timeout, options);
                CockroachDistributedLock.Release(connection, "hello", options);

                long lockCount = connection.Query<long>($@"SELECT COUNT(*) FROM ""{GetSchemaName()}"".""lock"" WHERE ""resource"" = @Resource",
                  new { Resource = "hello" }).Single();

                Assert.Equal(0, lockCount);
            });
        }

        private void UseConnection(Action<NpgsqlConnection> action)
        {
            _connection ??= ConnectionUtils.CreateConnection();
            action(_connection);
        }

        private static string GetSchemaName()
        {
            return ConnectionUtils.GetSchemaName();
        }
    }
}
