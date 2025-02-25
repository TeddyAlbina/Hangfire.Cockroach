﻿using System;
using Hangfire.Annotations;
using Npgsql;

namespace Hangfire.Cockroach.Factories;

/// <summary>
/// Connection factory that creates a new <see cref="NpgsqlConnection"/> based on the connection string.
/// </summary>
public sealed class NpgsqlConnectionFactory : NpgsqlInstanceConnectionFactoryBase
{
    private readonly string connectionString;
    
    [CanBeNull] 
    private readonly Action<NpgsqlConnection> connectionSetup;

    /// <summary>
    /// Instantiates the factory using specified <paramref name="connectionString"/>.
    /// </summary>
    /// <param name="connectionString">Connection string.</param>
    /// <param name="options"><see cref="CockroachStorageOptions"/> used for connection string verification.</param>
    /// <param name="connectionSetup">Optional additional connection setup action to be performed on the created <see cref="NpgsqlConnection"/>.</param>
    /// <exception cref="ArgumentNullException">Throws if <paramref name="connectionString"/> is null.</exception>
    public NpgsqlConnectionFactory(string connectionString, CockroachStorageOptions options, [CanBeNull] Action<NpgsqlConnection> connectionSetup = null) : base(options)
    {
        this.connectionString = this.SetupConnectionStringBuilder(connectionString ?? throw new ArgumentNullException(nameof(connectionString))).ConnectionString;
        this.connectionSetup = connectionSetup;
    }

    /// <inheritdoc />
    public override NpgsqlConnection GetOrCreateConnection()
    {
        NpgsqlConnection connection = new(this.connectionString);
        this.connectionSetup?.Invoke(connection);
        return connection;
    }
}
