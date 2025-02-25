﻿// This file is part of Hangfire.PostgreSql.
// Copyright © 2014 Frank Hommers <http://hmm.rs/Hangfire.PostgreSql>.
// 
// Hangfire.PostgreSql is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as 
// published by the Free Software Foundation, either version 3 
// of the License, or any later version.
// 
// Hangfire.PostgreSql  is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public 
// License along with Hangfire.PostgreSql. If not, see <http://www.gnu.org/licenses/>.
//
// This work is based on the work of Sergey Odinokov, author of 
// Hangfire. <http://hangfire.io/>
//   
//    Special thanks goes to him.

using System;
using System.Data;
using System.Globalization;
using System.Threading;

using Dapper;

using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;

namespace Hangfire.Cockroach;

#pragma warning disable CS0618
internal sealed class ExpirationManager : IBackgroundProcess, IServerComponent
#pragma warning restore CS0618
{
    private const string DistributedLockKey = "locks:expirationmanager";

    private static readonly TimeSpan defaultLockTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan delayBetweenPasses = TimeSpan.FromSeconds(1);
    private static readonly ILog Logger = LogProvider.GetLogger(typeof(ExpirationManager));

    private static readonly string[] processedCounters = [
        "stats:succeeded",
        "stats:deleted",
    ];
    private static readonly string[] processedTables = [
        "aggregatedcounter",
        "counter",
        "job",
        "list",
        "set",
        "hash",
    ];

    private readonly TimeSpan _checkInterval;
    private readonly CockroachStorage _storage;

    public ExpirationManager(CockroachStorage storage)
      : this(storage ?? throw new ArgumentNullException(nameof(storage)), storage.Options.JobExpirationCheckInterval) { }

    public ExpirationManager(CockroachStorage storage, TimeSpan checkInterval)
    {
        this._storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this._checkInterval = checkInterval;
    }

    public void Execute(BackgroundProcessContext context) => this.Execute(context.StoppingToken);

    public void Execute(CancellationToken cancellationToken)
    {
        foreach (var table in processedTables)
        {
            Logger.DebugFormat("Removing outdated records from table '{0}'...", table);

            this.UseConnectionDistributedLock(this._storage, connection =>
            {
                using var transaction = connection.BeginTransaction();
                
                int removedCount;
                do
                {
                    removedCount = connection.Execute($@"
                        DELETE FROM ""{this._storage.Options.SchemaName}"".""{table}"" 
                        WHERE ""id"" IN (
                            SELECT ""id"" 
                            FROM ""{this._storage.Options.SchemaName}"".""{table}"" 
                            WHERE ""expireat"" < NOW() 
                            LIMIT {this._storage.Options.DeleteExpiredBatchSize.ToString(CultureInfo.InvariantCulture)}
                        )", transaction: transaction);

                    if (removedCount <= 0)
                    {
                        continue;
                    }

                    Logger.InfoFormat("Removed {0} outdated record(s) from '{1}' table.", removedCount, table);

                    cancellationToken.WaitHandle.WaitOne(delayBetweenPasses);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                while (removedCount != 0);

                transaction.Commit();
            });
        }

        this.AggregateCounters(cancellationToken);
        
        cancellationToken.WaitHandle.WaitOne(this._checkInterval);
    }

    public override string ToString()
    {
        return "SQL Records Expiration Manager";
    }

    private void AggregateCounters(CancellationToken cancellationToken)
    {
        foreach (var processedCounter in processedCounters)
        {
            this.AggregateCounter(processedCounter);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void AggregateCounter(string counterName)
    {
        this.UseConnectionDistributedLock(this._storage, connection =>
        {
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            var aggregateQuery = $@"
            WITH ""counters"" AS (
              DELETE FROM ""{this._storage.Options.SchemaName}"".""counter""
              WHERE ""key"" = @Key
              AND ""expireat"" IS NULL
              RETURNING *
            )

            SELECT SUM(""value"") FROM ""counters"";
          ";

            var aggregatedValue = connection.ExecuteScalar<long>(aggregateQuery, new { Key = counterName }, transaction);
            
            transaction.Commit();

            if (aggregatedValue > 0)
            {
                var insertQuery = $@"INSERT INTO ""{this._storage.Options.SchemaName}"".""counter""(""key"", ""value"") VALUES (@Key, @Value);";
                
                connection.Execute(insertQuery, new { Key = counterName, Value = aggregatedValue });
            }
        });
    }

    private void UseConnectionDistributedLock(CockroachStorage storage, Action<IDbConnection> action)
    {
        try
        {
            storage.UseConnection(null, connection =>
            {
                CockroachDistributedLock.Acquire(connection, DistributedLockKey, defaultLockTimeout, this._storage.Options);

                try
                {
                    action(connection);
                }
                finally
                {
                    CockroachDistributedLock.Release(connection, DistributedLockKey, this._storage.Options);
                }
            });
        }
        catch (DistributedLockTimeoutException e) when (e.Resource == DistributedLockKey)
        {
            // DistributedLockTimeoutException here doesn't mean that outdated records weren't removed.
            // It just means another Hangfire server did this work.
            Logger.Log(LogLevel.Debug,
              () =>
                $@"An exception was thrown during acquiring distributed lock on the {DistributedLockKey} resource within {defaultLockTimeout.TotalSeconds} seconds. Outdated records were not removed. It will be retried in {this._checkInterval.TotalSeconds} seconds.",
              e);
        }
    }
}
