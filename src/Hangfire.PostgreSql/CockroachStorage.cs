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
using System.Data;
using System.Data.Common;
using System.Text;
using System.Transactions;

using Hangfire.Annotations;
using Hangfire.Cockroach.Factories;
using Hangfire.Cockroach.Utils;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;

using Npgsql;

using IsolationLevel = System.Transactions.IsolationLevel;

namespace Hangfire.Cockroach;

public sealed class CockroachStorage : JobStorage
{
    private readonly IConnectionFactory connectionFactory;

    private readonly Dictionary<string, bool> features = new(StringComparer.OrdinalIgnoreCase)
    {
        { JobStorageFeatures.JobQueueProperty, true },
    };


    public CockroachStorage(IConnectionFactory connectionFactory) : this(connectionFactory, new CockroachStorageOptions()) { }

    public CockroachStorage(IConnectionFactory connectionFactory, CockroachStorageOptions options)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        
        this.Options = options ?? throw new ArgumentNullException(nameof(options));

        if (options.PrepareSchemaIfNecessary)
        {
            var connection = this.CreateAndOpenConnection();
            try
            {
                CockroachObjectsInstaller.Install(connection, options.SchemaName);
            }
            finally
            {
                if (connectionFactory is not ExistingNpgsqlConnectionFactory)
                {
                    connection.Dispose();
                }
            }
        }

        this.InitializeQueueProviders();
    }

    public PersistentJobQueueProviderCollection QueueProviders { get; internal set; }

    internal CockroachStorageOptions Options { get; }

    public override IMonitoringApi GetMonitoringApi()
    {
        return new CockroachMonitoringApi(this, this.QueueProviders);
    }

    public override IStorageConnection GetConnection()
    {
        return new CockroachConnection(this);
    }

#pragma warning disable CS0618
    public override IEnumerable<IServerComponent> GetComponents()
#pragma warning restore CS0618
    {
        yield return new ExpirationManager(this);
        yield return new CountersAggregator(this, this.Options.CountersAggregateInterval);
    }

    public override void WriteOptionsToLog(ILog logger)
    {
        logger.Info("Using the following options for SQL Server job storage:");
        logger.InfoFormat("    Queue poll interval: {0}.", this.Options.QueuePollInterval);
        logger.InfoFormat("    Invisibility timeout: {0}.", this.Options.InvisibilityTimeout);
    }

    public override string ToString()
    {
        const string canNotParseMessage = "<Connection string can not be parsed>";

        try
        {
            StringBuilder builder = new();

            if (this.connectionFactory is NpgsqlInstanceConnectionFactoryBase connectionFactory)
            {
                var connectionStringBuilder = connectionFactory.ConnectionString;
                builder.Append("Host: ");
                builder.Append(connectionStringBuilder.Host);
                builder.Append(", DB: ");
                builder.Append(connectionStringBuilder.Database);
                builder.Append(", ");
            }

            builder.Append("Schema: ");
            builder.Append(this.Options.SchemaName);

            return builder.Length != 0 ? $"PostgreSQL Server: {builder}" : canNotParseMessage;
        }
        catch (Exception)
        {
            return canNotParseMessage;
        }
    }

    internal NpgsqlConnection CreateAndOpenConnection()
    {
        var connection = this.connectionFactory.GetOrCreateConnection();

        try
        {
            if (connection.State == ConnectionState.Closed)
            {
                connection.Open();
            }

            if (this.Options.EnableLongPolling && !connection.SupportsNotifications())
            {
                throw new InvalidOperationException("Long polling is supported only with PostgreSQL version 11 or higher.");
            }

            return connection;
        }
        catch
        {
            this.ReleaseConnection(connection);
            throw;
        }
    }

    internal void UseTransaction(DbConnection dedicatedConnection, [InstantHandle] Action<DbConnection, IDbTransaction> action, IsolationLevel? isolationLevel = null)
    {
        this.UseTransaction(dedicatedConnection, (connection, transaction) =>
        {
            action(connection, transaction);
            return true;
        }, isolationLevel);
    }

    internal T UseTransaction<T>(DbConnection dedicatedConnection, [InstantHandle] Func<DbConnection, IDbTransaction, T> func, IsolationLevel? isolationLevel = null)
    {
        // Use isolation level of an already opened transaction in order to avoid isolation level conflict
        isolationLevel ??= Transaction.Current?.IsolationLevel ?? IsolationLevel.ReadCommitted;

        if (!EnvironmentHelpers.IsMono())
        {
            T result = this.UseConnection(dedicatedConnection, connection =>
            {

                using var transaction = this.CreateTransactionScope(isolationLevel);
                
                connection.EnlistTransaction(Transaction.Current);
                
                var result = func(connection, null);
                
                transaction.Complete();
               
                return result;

            });

            return result;
        }

        return this.UseConnection(dedicatedConnection, connection =>
        {
            var transactionIsolationLevel = ConvertIsolationLevel(isolationLevel) ?? System.Data.IsolationLevel.ReadCommitted;
            
            using var transaction = connection.BeginTransaction(transactionIsolationLevel);
            
            T result;

            try
            {
                result = func(connection, transaction);
                transaction.Commit();
            }
            catch
            {
                if (transaction.Connection != null)
                {
                    // Don't rely on implicit rollback when calling the Dispose
                    // method, because some implementations may throw the
                    // NullReferenceException, although it's prohibited to throw
                    // any exception from a Dispose method, according to the
                    // .NET Framework Design Guidelines:
                    // https://github.com/dotnet/efcore/issues/12864
                    // https://github.com/HangfireIO/Hangfire/issues/1494
                    transaction.Rollback();
                }

                throw;
            }

            return result;
        });
    }

    internal void UseTransaction(DbConnection dedicatedConnection, Action<DbConnection, DbTransaction> action, Func<TransactionScope> transactionScopeFactory)
    {
        this.UseTransaction(dedicatedConnection, (connection, transaction) =>
        {
            action(connection, transaction);
            return true;
        }, transactionScopeFactory);
    }

    internal T UseTransaction<T>(DbConnection dedicatedConnection, Func<DbConnection, DbTransaction, T> func, Func<TransactionScope> transactionScopeFactory)
    {
        return this.UseConnection(dedicatedConnection, connection =>
        {
            using var transaction = transactionScopeFactory();
            connection.EnlistTransaction(Transaction.Current);

            var result = func(connection, null);

            transaction.Complete();

            return result;
        });
    }

    internal TransactionScope CreateTransactionScope(IsolationLevel? isolationLevel, TimeSpan? timeout = null)
    {
        return TransactionHelpers.CreateTransactionScope(isolationLevel, this.Options.EnableTransactionScopeEnlistment, timeout);
    }

    private static System.Data.IsolationLevel? ConvertIsolationLevel(IsolationLevel? isolationLevel)
    {
        return isolationLevel switch
        {
            IsolationLevel.Chaos => System.Data.IsolationLevel.Chaos,
            IsolationLevel.ReadCommitted => System.Data.IsolationLevel.ReadCommitted,
            IsolationLevel.ReadUncommitted => System.Data.IsolationLevel.ReadUncommitted,
            IsolationLevel.RepeatableRead => System.Data.IsolationLevel.RepeatableRead,
            IsolationLevel.Serializable => System.Data.IsolationLevel.Serializable,
            IsolationLevel.Snapshot => System.Data.IsolationLevel.Snapshot,
            IsolationLevel.Unspecified => System.Data.IsolationLevel.Unspecified,
            null => null,
            var _ => throw new ArgumentOutOfRangeException(nameof(isolationLevel), isolationLevel, null),
        };
    }

    internal void UseConnection(DbConnection dedicatedConnection, [InstantHandle] Action<DbConnection> action)
    {
        this.UseConnection(dedicatedConnection, connection =>
        {
            action(connection);
            return true;
        });
    }

    internal T UseConnection<T>(DbConnection dedicatedConnection, Func<DbConnection, T> func)
    {
        DbConnection connection = null;

        try
        {
            connection = dedicatedConnection ?? this.CreateAndOpenConnection();
            return func(connection);
        }
        finally
        {
            if (dedicatedConnection == null)
            {
                this.ReleaseConnection(connection);
            }
        }
    }

    internal void ReleaseConnection(DbConnection connection)
    {
        if (connection != null && !this.IsExistingConnection(connection))
        {
            connection.Dispose();
        }
    }

    private bool IsExistingConnection(IDbConnection connection) => connection != null && this.connectionFactory is ExistingNpgsqlConnectionFactory && ReferenceEquals(connection, this.connectionFactory.GetOrCreateConnection());

    private void InitializeQueueProviders() => this.QueueProviders = new PersistentJobQueueProviderCollection(new CockroachJobQueueProvider(this, this.Options));

    public override bool HasFeature(string featureId)
    {
        if (featureId == null)
        {
            throw new ArgumentNullException(nameof(featureId));
        }

        return this.features.TryGetValue(featureId, out var isSupported)
          ? isSupported
          : base.HasFeature(featureId);
    }
}
