using System;
using Hangfire.Annotations;
using Hangfire.Cockroach.Factories;
using Npgsql;

namespace Hangfire.Cockroach;

/// <summary>
/// Bootstrapper options.
/// </summary>
public sealed class CockroachBootstrapperOptions
{
    private readonly CockroachStorageOptions options;

    internal CockroachBootstrapperOptions(CockroachStorageOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    [CanBeNull] internal IConnectionFactory ConnectionFactory { get; private set; }

    /// <summary>
    /// Configures the bootstrapper to use a custom <see cref="IConnectionFactory"/> to use for each database action.
    /// </summary>
    /// <param name="connectionFactory">Instance of <see cref="IConnectionFactory"/>.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentNullException">Throws if <paramref name="connectionFactory"/> is null.</exception>
    public CockroachBootstrapperOptions UseConnectionFactory(IConnectionFactory connectionFactory)
    {
        this.ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        return this;
    }

    /// <summary>
    /// Configures the bootstrapper to create a new <see cref="NpgsqlConnection"/> for each database action.
    /// </summary>
    /// <param name="connectionString">Connection string.</param>
    /// <param name="connectionSetup">Optional additional connection setup action to be performed on the created <see cref="NpgsqlConnection"/>.</param>
    /// <returns>This instance.</returns>
    public CockroachBootstrapperOptions UseNpgsqlConnection(string connectionString, [CanBeNull] Action<NpgsqlConnection> connectionSetup = null)
        => this.UseConnectionFactory(new NpgsqlConnectionFactory(connectionString, this.options, connectionSetup));

    /// <summary>
    /// Configures the bootstrapper to use the existing <see cref="NpgsqlConnection"/> for each database action.
    /// </summary>
    /// <param name="connection"><see cref="NpgsqlConnection"/> to use.</param>
    /// <returns>This instance.</returns>
    public CockroachBootstrapperOptions UseExistingNpgsqlConnection(NpgsqlConnection connection)
        => this.UseConnectionFactory(new ExistingNpgsqlConnectionFactory(connection, this.options));
}
