// This file is part of Hangfire.PostgreSql.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dapper;

using Hangfire.Cockroach.Properties;
using Hangfire.Cockroach.Utils;
using Hangfire.Storage;

using Npgsql;

namespace Hangfire.Cockroach;

public sealed class CockroachJobQueue : IPersistentJobQueue
{
    private const string JobNotificationChannel = "new_job";

    internal static readonly AutoResetEventRegistry queueEventRegistry = new();
    private readonly CockroachStorage storage;

    public CockroachJobQueue(CockroachStorage storage)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.SignalDequeue = new AutoResetEvent(false);
        this.JobQueueNotification = new AutoResetEvent(false);
    }

    private AutoResetEvent SignalDequeue { get; }
    private AutoResetEvent JobQueueNotification { get; }

    [NotNull]
    public IFetchedJob Dequeue(string[] queues, CancellationToken cancellationToken)
    {
        return this.storage.Options.UseNativeDatabaseTransactions
          ? this.Dequeue_Transaction(queues, cancellationToken)
          : this.Dequeue_UpdateCount(queues, cancellationToken);
    }

    public void Enqueue(IDbConnection connection, string queue, string jobId)
    {
        var enqueueJobSql = $@"
        INSERT INTO ""{this.storage.Options.SchemaName}"".""jobqueue"" (""jobid"", ""queue"") 
        VALUES (@JobId, @Queue);
      ";

        connection.Execute(enqueueJobSql,
          new { JobId = Guid.Parse(jobId), Queue = queue });

        //if (_storage.Options.EnableLongPolling)
        //{
        //  //connection.Execute($"NOTIFY {JobNotificationChannel}");
        //}
    }

    /// <summary>
    ///   Signal the waiting Thread to lookup a new Job
    /// </summary>
    public void FetchNextJob()
    {
        this.SignalDequeue.Set();
    }


    [NotNull]
    internal IFetchedJob Dequeue_Transaction(string[] queues, CancellationToken cancellationToken)
    {
        if (queues == null)
        {
            throw new ArgumentNullException(nameof(queues));
        }

        if (queues.Length == 0)
        {
            throw new ArgumentException("Queue array must be non-empty.", nameof(queues));
        }

        var timeoutSeconds = (long)this.storage.Options.InvisibilityTimeout.Negate().TotalSeconds;
        FetchedJob fetchedJob;

        var fetchJobSql = $@"
        UPDATE ""{this.storage.Options.SchemaName}"".""jobqueue"" 
        SET ""fetchedat"" = NOW()
        WHERE ""id"" = (
          SELECT ""id"" 
          FROM ""{this.storage.Options.SchemaName}"".""jobqueue"" 
          WHERE ""queue"" = ANY (@Queues)
          AND (""fetchedat"" IS NULL OR ""fetchedat"" < NOW() + INTERVAL '{timeoutSeconds.ToString(CultureInfo.InvariantCulture)} SECONDS')
          ORDER BY ""fetchedat"" NULLS FIRST, ""queue"", ""serialid""
          FOR UPDATE SKIP LOCKED
          LIMIT 1
        )
        RETURNING ""id"" AS ""Id"", ""jobid"" AS ""JobId"", ""queue"" AS ""Queue"", ""fetchedat"" AS ""FetchedAt"";
      ";

        WaitHandle[] nextFetchIterationWaitHandles = [
          cancellationToken.WaitHandle,
            this.SignalDequeue,
            this.JobQueueNotification,
            .. queueEventRegistry.GetWaitHandles(queues),
        ];

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            Utils.Utils.TryExecute(() =>
            {
                NpgsqlConnection connection = this.storage.CreateAndOpenConnection();

                try
                {
                    using var trx = connection.BeginTransaction(IsolationLevel.ReadCommitted);

                    var jobToFetch = connection.Query<FetchedJob>(fetchJobSql, new { Queues = queues.ToList() }, trx).SingleOrDefault();

                    trx.Commit();

                    return jobToFetch;
                }
                catch (InvalidOperationException)
                {
                    // thrown by .SingleOrDefault(): stop the exception propagation if the fetched job was concurrently fetched by another worker
                }
                finally
                {
                    this.storage.ReleaseConnection(connection);
                }

                return null;
            },
              out fetchedJob,
              ex => ex is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure });

            if (fetchedJob == null)
            {
                WaitHandle.WaitAny(nextFetchIterationWaitHandles, this.storage.Options.QueuePollInterval);
            }
        }
        while (fetchedJob == null);

        return new CockroachFetchedJob(this.storage, fetchedJob.Id, fetchedJob.JobId.ToString(), fetchedJob.Queue);
    }

    [NotNull]
    internal IFetchedJob Dequeue_UpdateCount(string[] queues, CancellationToken cancellationToken)
    {
        if (queues == null)
        {
            throw new ArgumentNullException(nameof(queues));
        }

        if (queues.Length == 0)
        {
            throw new ArgumentException("Queue array must be non-empty.", nameof(queues));
        }

        var timeoutSeconds = (long)this.storage.Options.InvisibilityTimeout.Negate().TotalSeconds;
        FetchedJob markJobAsFetched = null;


        var jobToFetchSql = $@"
        SELECT ""id"" AS ""Id"", ""jobid"" AS ""JobId"", ""queue"" AS ""Queue"", ""fetchedat"" AS ""FetchedAt"", ""updatecount"" AS ""UpdateCount""
        FROM ""{this.storage.Options.SchemaName}"".""jobqueue"" 
        WHERE ""queue"" = ANY (@Queues)
        AND (""fetchedat"" IS NULL OR ""fetchedat"" < NOW() + INTERVAL '{timeoutSeconds.ToString(CultureInfo.InvariantCulture)} SECONDS')
        ORDER BY ""fetchedat"" NULLS FIRST, ""queue"", ""serialid""
        LIMIT 1;
        ";

        var markJobAsFetchedSql = $@"
        UPDATE ""{this.storage.Options.SchemaName}"".""jobqueue"" 
        SET ""fetchedat"" = NOW(), 
            ""updatecount"" = (""updatecount"" + 1) % 2000000000
        WHERE ""id"" = @Id 
        AND ""updatecount"" = @UpdateCount
        RETURNING ""id"" AS ""Id"", ""jobid"" AS ""JobId"", ""queue"" AS ""Queue"", ""fetchedat"" AS ""FetchedAt"";
      ";

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            FetchedJob jobToFetch = this.storage.UseConnection(null, connection => connection.Query<FetchedJob>(jobToFetchSql,
                new { Queues = queues.ToList() })
              .SingleOrDefault());

            if (jobToFetch == null)
            {
                WaitHandle.WaitAny([
                    cancellationToken.WaitHandle,
                    this.SignalDequeue,
                    this.JobQueueNotification,
                ],
                  this.storage.Options.QueuePollInterval);

                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                markJobAsFetched = this.storage.UseConnection(null, connection => connection.Query<FetchedJob>(markJobAsFetchedSql,
                    jobToFetch)
                  .SingleOrDefault());
            }
        }
        while (markJobAsFetched == null);

        return new CockroachFetchedJob(this.storage,
          markJobAsFetched.Id,
          markJobAsFetched.JobId.ToString(),
          markJobAsFetched.Queue);
    }

    private Task ListenForNotificationsAsync(CancellationToken cancellationToken)
    {
        var connection = this.storage.CreateAndOpenConnection();

        try
        {
            if (!connection.SupportsNotifications())
            {
                return Task.CompletedTask;
            }

            // CreateAnOpenConnection can return the same connection over and over if an existing connection
            //  is passed in the constructor of PostgreSqlStorage. We must use a separate dedicated
            //  connection to listen for notifications.
            var clonedConnection = connection.CloneWith(connection.ConnectionString);

            return Task.Run(async () =>
            {
                try
                {
                    if (clonedConnection.State != ConnectionState.Open)
                    {
                        await clonedConnection.OpenAsync(cancellationToken); // Open so that Dapper doesn't auto-close.
                    }

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await clonedConnection.ExecuteAsync($"LISTEN {JobNotificationChannel}");
                        await clonedConnection.WaitAsync(cancellationToken);
                        this.JobQueueNotification.Set();
                    }
                }
                catch (TaskCanceledException)
                {
                    // Do nothing, cancellation requested so just end.
                }
                finally
                {
                    this.storage.ReleaseConnection(clonedConnection);
                }

            }, cancellationToken);

        }
        finally
        {
            this.storage.ReleaseConnection(connection);
        }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class FetchedJob
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string Queue { get; set; }
        public DateTime? FetchedAt { get; set; }
        public int UpdateCount { get; set; }
    }
}
