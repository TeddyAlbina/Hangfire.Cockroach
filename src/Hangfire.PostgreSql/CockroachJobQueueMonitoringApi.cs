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
using System.Collections.Generic;
using System.Linq;

using Dapper;

namespace Hangfire.Cockroach;

internal sealed class CockroachJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
{
    private readonly CockroachStorage storage;

    public CockroachJobQueueMonitoringApi(CockroachStorage storage)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public IEnumerable<string> GetQueues()
    {
        var sqlQuery = $@"SELECT DISTINCT ""queue"" FROM ""{this.storage.Options.SchemaName}"".""jobqueue""";
        return this.storage.UseConnection(null, connection => connection.Query<string>(sqlQuery).ToList());
    }

    public IEnumerable<Guid> GetEnqueuedJobIds(string queue, int from, int perPage) => this.GetQueuedOrFetchedJobIds(queue, false, from, perPage);

    public IEnumerable<Guid> GetFetchedJobIds(string queue, int from, int perPage) => this.GetQueuedOrFetchedJobIds(queue, true, from, perPage);

    public EnqueuedAndFetchedCountDto GetEnqueuedAndFetchedCount(string queue)
    {
        var sqlQuery = $@"
        SELECT (
            SELECT COUNT(*) 
            FROM ""{this.storage.Options.SchemaName}"".""jobqueue"" 
            WHERE ""fetchedat"" IS NULL 
            AND ""queue"" = @Queue
        ) ""EnqueuedCount"", 
        (
          SELECT COUNT(*) 
          FROM ""{this.storage.Options.SchemaName}"".""jobqueue"" 
          WHERE ""fetchedat"" IS NOT NULL 
          AND ""queue"" = @Queue
        ) ""FetchedCount"";
      ";

        (var enqueuedCount, var fetchedCount) = this.storage.UseConnection(null, connection => connection.QuerySingle<(long EnqueuedCount, long FetchedCount)>(sqlQuery, new { Queue = queue }));

        return new EnqueuedAndFetchedCountDto
        {
            EnqueuedCount = enqueuedCount,
            FetchedCount = fetchedCount,
        };
    }

    private IEnumerable<Guid> GetQueuedOrFetchedJobIds(string queue, bool fetched, int from, int perPage)
    {
        var sqlQuery = $@"
        SELECT j.""id"" 
        FROM ""{this.storage.Options.SchemaName}"".""jobqueue"" jq
        LEFT JOIN ""{this.storage.Options.SchemaName}"".""job"" j ON jq.""jobid"" = j.""id""
        WHERE jq.""queue"" = @Queue 
        AND jq.""fetchedat"" {(fetched ? "IS NOT NULL" : "IS NULL")}
        AND j.""id"" IS NOT NULL
        ORDER BY jq.""fetchedat"", jq.""serialid""
        LIMIT @Limit OFFSET @Offset;
      ";

        return this.storage.UseConnection(null, connection => connection.Query<Guid>(sqlQuery,
            new { Queue = queue, Offset = from, Limit = perPage })
          .ToList());
    }
}
