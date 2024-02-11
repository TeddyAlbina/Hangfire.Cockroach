using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;

using Hangfire.Cockroach.Factories;
using Hangfire.Cockroach.Tests.Utils;
using Hangfire.Server;
using Hangfire.Storage;

using Npgsql;

using Xunit;

namespace Hangfire.Cockroach.Tests;

public class PostgreSqlStorageFacts
{
    private readonly CockroachStorageOptions _options;

    public PostgreSqlStorageFacts()
    {
        _options = new CockroachStorageOptions { PrepareSchemaIfNecessary = false, EnableTransactionScopeEnlistment = true };
    }

    [Fact]
    [CleanDatabase]
    public void Ctor_CanCreateSqlServerStorage_WithExistingConnection()
    {
        NpgsqlConnection connection = ConnectionUtils.CreateConnection();
        CockroachStorage storage = new(new ExistingNpgsqlConnectionFactory(connection, _options), _options);

        Assert.NotNull(storage);
    }

    [Fact]
    [CleanDatabase]
    public void Ctor_InitializesDefaultJobQueueProvider_AndPassesCorrectOptions()
    {
        CockroachStorage storage = CreateStorage();
        PersistentJobQueueProviderCollection providers = storage.QueueProviders;

        CockroachJobQueueProvider provider = (CockroachJobQueueProvider)providers.GetProvider("default");

        Assert.Same(_options, provider.Options);
    }

    [Fact]
    [CleanDatabase]
    public void GetMonitoringApi_ReturnsNonNullInstance()
    {
        CockroachStorage storage = CreateStorage();
        IMonitoringApi api = storage.GetMonitoringApi();
        Assert.NotNull(api);
    }

    [Fact]
    [CleanDatabase]
    public void GetComponents_ReturnsAllNeededComponents()
    {
        CockroachStorage storage = CreateStorage();

#pragma warning disable CS0618 // Type or member is obsolete
        IEnumerable<IServerComponent> components = storage.GetComponents();
#pragma warning restore CS0618 // Type or member is obsolete

        Type[] componentTypes = components.Select(x => x.GetType()).ToArray();
        Assert.Contains(typeof(ExpirationManager), componentTypes);
    }

    [Fact]
    public void Ctor_ThrowsAnException_WhenConnectionFactoryIsNull()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CockroachStorage(connectionFactory: null, new CockroachStorageOptions()));
        Assert.Equal("connectionFactory", exception.ParamName);
    }

    [Fact]
    [CleanDatabase]
    public void Ctor_CanCreateSqlServerStorage_WithExistingConnectionFactory()
    {
        CockroachStorage storage = new(new DefaultConnectionFactory(), _options);
        Assert.NotNull(storage);
    }

    [Fact]
    [CleanDatabase]
    public void CanCreateAndOpenConnection_WithExistingConnectionFactory()
    {
        CockroachStorage storage = new(new DefaultConnectionFactory(), _options);
        NpgsqlConnection connection = storage.CreateAndOpenConnection();
        Assert.NotNull(connection);
    }

    [Fact]
    public void CreateAndOpenConnection_ThrowsAnException_WithExistingConnectionFactoryAndInvalidOptions()
    {
        CockroachStorageOptions option = new()
        {
            EnableTransactionScopeEnlistment = false,
            PrepareSchemaIfNecessary = false,
        };
        Assert.Throws<ArgumentException>(() => new CockroachStorage(ConnectionUtils.GetDefaultConnectionFactory(option), option));
    }

    [Fact]
    public void CanUseTransaction_WithDifferentTransactionIsolationLevel()
    {
        using TransactionScope scope = new(TransactionScopeOption.Required,
          new TransactionOptions() { IsolationLevel = IsolationLevel.Serializable });

        CockroachStorage storage = new(new DefaultConnectionFactory(), _options);
        NpgsqlConnection connection = storage.CreateAndOpenConnection();

        bool success = storage.UseTransaction(connection, (_, _) => true);

        Assert.True(success);
    }

    private CockroachStorage CreateStorage()
    {
        return new CockroachStorage(ConnectionUtils.GetDefaultConnectionFactory(), _options);
    }
}
