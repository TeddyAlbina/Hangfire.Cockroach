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

using Dapper;

using Hangfire.Storage;

namespace Hangfire.Cockroach;

public sealed class CockroachFetchedJob : IFetchedJob
{
    private readonly CockroachStorage storage;
    private bool disposed;
    private bool removedFromQueue;
    private bool requeued;

    public CockroachFetchedJob(CockroachStorage storage, Guid id, string jobId, string queue)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.Id = id;
        this.JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        this.Queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public Guid Id { get; }
    public string Queue { get; }
    public string JobId { get; }

    public void RemoveFromQueue()
    {
        this.storage.UseConnection(null, connection => connection.Execute($@"
        DELETE FROM ""{this.storage.Options.SchemaName}"".""jobqueue"" WHERE ""id"" = @Id;
      ",
          new { this.Id }));

        this.removedFromQueue = true;
    }

    public void Requeue()
    {
        this.storage.UseConnection(null, connection => connection.Execute($@"
        UPDATE ""{this.storage.Options.SchemaName}"".""jobqueue"" 
        SET ""fetchedat"" = NULL 
        WHERE ""id"" = @Id;
      ",
        new { this.Id }));

        this.requeued = true;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        if (!this.removedFromQueue && !this.requeued)
        {
            this.Requeue();
        }

        this.disposed = true;
    }
}
