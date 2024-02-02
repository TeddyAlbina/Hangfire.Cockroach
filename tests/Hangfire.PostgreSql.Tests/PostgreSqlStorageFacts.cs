﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Hangfire.Cockroach.Factories;
using Hangfire.Cockroach.Tests.Utils;
using Hangfire.Server;
using Hangfire.Storage;
using Npgsql;
using Xunit;

namespace Hangfire.Cockroach.Tests
{
  public class PostgreSqlStorageFacts
  {
    private readonly PostgreSqlStorageOptions _options;

    public PostgreSqlStorageFacts()
    {
      _options = new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = false, EnableTransactionScopeEnlistment = true };
    }

    [Fact]
    [CleanDatabase]
    public void Ctor_CanCreateSqlServerStorage_WithExistingConnection()
    {
      NpgsqlConnection connection = ConnectionUtils.CreateConnection();
      PostgreSqlStorage storage = new(new ExistingNpgsqlConnectionFactory(connection, _options), _options);

      Assert.NotNull(storage);
    }

    [Fact]
    [CleanDatabase]
    public void Ctor_InitializesDefaultJobQueueProvider_AndPassesCorrectOptions()
    {
      PostgreSqlStorage storage = CreateStorage();
      PersistentJobQueueProviderCollection providers = storage.QueueProviders;

      PostgreSqlJobQueueProvider provider = (PostgreSqlJobQueueProvider)providers.GetProvider("default");

      Assert.Same(_options, provider.Options);
    }

    [Fact]
    [CleanDatabase]
    public void GetMonitoringApi_ReturnsNonNullInstance()
    {
      PostgreSqlStorage storage = CreateStorage();
      IMonitoringApi api = storage.GetMonitoringApi();
      Assert.NotNull(api);
    }

    [Fact]
    [CleanDatabase]
    public void GetComponents_ReturnsAllNeededComponents()
    {
      PostgreSqlStorage storage = CreateStorage();

#pragma warning disable CS0618 // Type or member is obsolete
      IEnumerable<IServerComponent> components = storage.GetComponents();
#pragma warning restore CS0618 // Type or member is obsolete

      Type[] componentTypes = components.Select(x => x.GetType()).ToArray();
      Assert.Contains(typeof(ExpirationManager), componentTypes);
    }

    [Fact]
    public void Ctor_ThrowsAnException_WhenConnectionFactoryIsNull()
    {
      ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new PostgreSqlStorage(connectionFactory: null, new PostgreSqlStorageOptions()));
      Assert.Equal("connectionFactory", exception.ParamName);
    }

    [Fact]
    [CleanDatabase]
    public void Ctor_CanCreateSqlServerStorage_WithExistingConnectionFactory()
    {
      PostgreSqlStorage storage = new(new DefaultConnectionFactory(), _options);
      Assert.NotNull(storage);
    }

    [Fact]
    [CleanDatabase]
    public void CanCreateAndOpenConnection_WithExistingConnectionFactory()
    {
      PostgreSqlStorage storage = new(new DefaultConnectionFactory(), _options);
      NpgsqlConnection connection = storage.CreateAndOpenConnection();
      Assert.NotNull(connection);
    }

    [Fact]
    public void CreateAndOpenConnection_ThrowsAnException_WithExistingConnectionFactoryAndInvalidOptions()
    {
      PostgreSqlStorageOptions option = new() {
        EnableTransactionScopeEnlistment = false,
        PrepareSchemaIfNecessary = false,
      };
      Assert.Throws<ArgumentException>(() => new PostgreSqlStorage(ConnectionUtils.GetDefaultConnectionFactory(option), option));
    }

    [Fact]
    public void CanUseTransaction_WithDifferentTransactionIsolationLevel()
    {
      using TransactionScope scope = new(TransactionScopeOption.Required,
        new TransactionOptions() { IsolationLevel = IsolationLevel.Serializable });
      
      PostgreSqlStorage storage = new(new DefaultConnectionFactory(), _options);
      NpgsqlConnection connection = storage.CreateAndOpenConnection();
      
      bool success = storage.UseTransaction(connection, (_, _) => true);
      
      Assert.True(success);
    }

    private PostgreSqlStorage CreateStorage()
    {
      return new PostgreSqlStorage(ConnectionUtils.GetDefaultConnectionFactory(), _options);
    }
  }
}
