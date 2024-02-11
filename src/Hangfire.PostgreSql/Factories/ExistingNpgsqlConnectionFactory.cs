using System;
using Npgsql;

namespace Hangfire.Cockroach.Factories;

/// <summary>
/// Connection factory that utilizes an already-existing <see cref="NpgsqlConnection"/>.
/// </summary>
public sealed class ExistingNpgsqlConnectionFactory : NpgsqlInstanceConnectionFactoryBase
{
    private readonly NpgsqlConnection connection;

    /// <summary>
    /// Instantiates the factory using specified <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection"><see cref="NpgsqlConnection"/> to use.</param>
    /// <param name="options"><see cref="CockroachStorageOptions"/> used for connection string verification.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ExistingNpgsqlConnectionFactory(NpgsqlConnection connection, CockroachStorageOptions options) : base(options)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        // To ensure valid connection string - throws internally
        this.SetupConnectionStringBuilder(this.connection.ConnectionString);
    }

    /// <inheritdoc />
    public override NpgsqlConnection GetOrCreateConnection() => this.connection;
}
