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

namespace Hangfire.Cockroach;

public sealed class CockroachStorageOptions
{
    private static readonly TimeSpan _minimumQueuePollInterval = TimeSpan.FromMilliseconds(50);

    private int deleteExpiredBatchSize;
    private TimeSpan distributedLockTimeout;
    private TimeSpan invisibilityTimeout;
    private TimeSpan jobExpirationCheckInterval;
    private TimeSpan queuePollInterval;
    private TimeSpan transactionSerializationTimeout;
    private TimeSpan countersAggregateInterval;

    public CockroachStorageOptions()
    {
        this.QueuePollInterval = TimeSpan.FromSeconds(15);
        this.InvisibilityTimeout = TimeSpan.FromMinutes(30);
        this.DistributedLockTimeout = TimeSpan.FromMinutes(10);
        this.TransactionSynchronisationTimeout = TimeSpan.FromMilliseconds(500);
        this.JobExpirationCheckInterval = TimeSpan.FromHours(1);
        this.CountersAggregateInterval = TimeSpan.FromMinutes(5);
        this.SchemaName = "hangfire";
        this.AllowUnsafeValues = false;
        this.UseNativeDatabaseTransactions = true;
        this.PrepareSchemaIfNecessary = true;
        this.EnableTransactionScopeEnlistment = true;
        this.DeleteExpiredBatchSize = 1000;
    }

    public TimeSpan QueuePollInterval
    {
        get => this.queuePollInterval;
        set
        {
            this.ThrowIfValueIsLowerThan(_minimumQueuePollInterval, value, nameof(this.QueuePollInterval));
            this.queuePollInterval = value;
        }
    }

    public TimeSpan InvisibilityTimeout
    {
        get => this.invisibilityTimeout;
        set
        {
            ThrowIfValueIsNotPositive(value, nameof(this.InvisibilityTimeout));
            this.invisibilityTimeout = value;
        }
    }

    public TimeSpan DistributedLockTimeout
    {
        get => this.distributedLockTimeout;
        set
        {
            ThrowIfValueIsNotPositive(value, nameof(this.DistributedLockTimeout));
            this.distributedLockTimeout = value;
        }
    }

    // ReSharper disable once IdentifierTypo
    public TimeSpan TransactionSynchronisationTimeout
    {
        get => this.transactionSerializationTimeout;
        set
        {
            ThrowIfValueIsNotPositive(value, nameof(this.TransactionSynchronisationTimeout));
            this.transactionSerializationTimeout = value;
        }
    }

    public TimeSpan JobExpirationCheckInterval
    {
        get => this.jobExpirationCheckInterval;
        set
        {
            ThrowIfValueIsNotPositive(value, nameof(this.JobExpirationCheckInterval));
            this.jobExpirationCheckInterval = value;
        }
    }

    public TimeSpan CountersAggregateInterval
    {
        get => this.countersAggregateInterval;
        set
        {
            ThrowIfValueIsNotPositive(value, nameof(this.CountersAggregateInterval));
            this.countersAggregateInterval = value;
        }
    }

    /// <summary>
    ///   Gets or sets the number of records deleted in a single batch in expiration manager
    /// </summary>
    public int DeleteExpiredBatchSize
    {
        get => this.deleteExpiredBatchSize;
        set
        {
            ThrowIfValueIsNotPositive(value, nameof(this.DeleteExpiredBatchSize));
            this.deleteExpiredBatchSize = value;
        }
    }

    public bool AllowUnsafeValues { get; set; }
    public bool UseNativeDatabaseTransactions { get; set; }
    public bool PrepareSchemaIfNecessary { get; set; }
    public string SchemaName { get; set; }
    public bool EnableTransactionScopeEnlistment { get; set; }
    public bool EnableLongPolling { get; set; }

    private static void ThrowIfValueIsNotPositive(TimeSpan value, string fieldName)
    {
        var message = $"The {fieldName} property value should be positive. Given: {value}.";

        if (value == TimeSpan.Zero)
        {
            throw new ArgumentException(message, nameof(value));
        }

        if (value != value.Duration())
        {
            throw new ArgumentException(message, nameof(value));
        }
    }

    private void ThrowIfValueIsLowerThan(TimeSpan minValue, TimeSpan value, string fieldName)
    {
        if (!this.AllowUnsafeValues)
        {
            var message = $"The {fieldName} property value seems to be too low ({value}, lower than suggested minimum of {minValue}). Consider increasing it. If you really need to have such a low value, please set {nameof(CockroachStorageOptions)}.{nameof(this.AllowUnsafeValues)} to true.";

            if (value < minValue)
            {
                throw new ArgumentException(message, nameof(value));
            }
        }

        ThrowIfValueIsNotPositive(value, fieldName);
    }

    private static void ThrowIfValueIsNotPositive(int value, string fieldName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"The {fieldName} property value should be positive. Given: {value}.");
        }
    }
}
